using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Siegelocation;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Players;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/FortressSiege (SoulKeeper) extends Siege&lt;FortressLocation&gt;. Full fortress siege lifecycle: onSiegeStart (vulnerable, spawn siege npcs, boss, mercenary zones, balance buffs, faction-troop assault, balaur assault), onSiegeFinish (capture/defend, reward players + legion, outpost update, persist, quest onKill), onCapture/onDefended (race/legion transfer, announce, world buffs), faction-balance adjustment, legion GP + reward distribution. ConcurrentHashMap->ConcurrentDictionary; schedule(Runnable,ms)->Schedule ct-lambda; forEachPlayer lambda; Integer legionId->int?(??0); Math.round->(int/long)Floor(x+0.5f); Math.toRadians->x*PI/180; retainAll->IntersectWith; switch-on-locationId/race; nested FortressLocation.SiegeBuffAction. FortressLocation/Legion/SiegeRace/ItemId red-tolerated.</summary>
public class FortressSiege : Siege<FortressLocation>
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("SIEGE_LOG");
    private readonly ConcurrentDictionary<int, MercenaryLocation> activeMercenaryLocs = new ConcurrentDictionary<int, MercenaryLocation>();
    private int oldLegionId;

    public FortressSiege(FortressLocation fortress)
        : base(fortress)
    {
    }

    protected override void OnSiegeStart()
    {
        if (LoggingConfig.LOG_SIEGE)
            log.LogInformation(this + ": Siege started. Race: " + GetSiegeLocation().GetRace() + ", legion ID: " + GetSiegeLocation().GetLegionId());
        // Mark fortress as vulnerable
        GetSiegeLocation().SetVulnerable(true);

        // Let the world know where the siege is
        BroadcastState(GetSiegeLocation());

        // Clear fortress from enemys
        GetSiegeLocation().ClearLocation();

        // Remove all and spawn siege NPCs
        DespawnNpcs(GetSiegeLocationId());
        SpawnNpcs(GetSiegeLocationId(), GetSiegeLocation().GetRace(), SiegeModType.SIEGE);
        InitSiegeBoss();
        this.oldLegionId = GetSiegeLocation().GetLegionId();
        if (GetSiegeLocation().GetRace() != SiegeRace.BALAUR)
        {
            InitMercenaryZones();
            GetSiegeLocation().ForEachPlayer(p => GetSiegeLocation().CheckForBalanceBuff(p, FortressLocation.SiegeBuffAction.ADD));
            if (GetBoss().GetLevel() == 65)
            {
                SiegeRace oppositeRace = GetSiegeLocation().GetRace() == SiegeRace.ELYOS ? SiegeRace.ASMODIANS : SiegeRace.ELYOS;
                ThreadPoolManager.GetInstance().Schedule(ct => { SpawnFactionTroopAssault(oppositeRace); return System.Threading.Tasks.ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(Rnd.Get(600, 1800) * 1000)); // Faction Balance NPCs
            }
        }
        // Check for Balaur Assault
        if (SiegeConfig.BALAUR_AUTO_ASSAULT)
            BalaurAssaultService.GetInstance().OnSiegeStart(this);
    }

    /// <summary>
    /// Handles an additional assault of race-specific troops (asmo/ely only), to ensure players glory point rewards
    /// </summary>
    private void SpawnFactionTroopAssault(SiegeRace race)
    {
        if (!GetSiegeLocation().IsVulnerable())
            return;

        int worldId = GetSiegeLocation().GetWorldId();
        foreach (SiegeNpc sn in World.GetInstance().GetLocalSiegeNpcs(GetSiegeLocationId()))
        {
            if (sn.GetAbyssNpcType() == AbyssNpcType.ARTIFACT || sn.GetRating() == NpcRating.LEGENDARY
                || sn.GetSpawn().GetSiegeModType() == SiegeModType.ASSAULT || Rnd.Chance() < 35)
                continue;
            int amount = Rnd.Get(1, 2);
            for (int i = 0; i < amount; i++)
            {
                double angleRadians = Rnd.NextFloat(180f) * Math.PI / 180;
                float x1 = (float)(sn.GetX() + Math.Cos(angleRadians) * Rnd.Get(1, 2));
                float y1 = (float)(sn.GetY() + Math.Sin(angleRadians) * Rnd.Get(1, 2));
                SpawnTemplate temp = SpawnEngine.NewSiegeSpawn(worldId, race == SiegeRace.ELYOS ? Rnd.Get(252408, 252412) : Rnd.Get(252413, 252417),
                    GetSiegeLocationId(), race, SiegeModType.ASSAULT, x1, y1, sn.GetZ(), (byte)0);
                SpawnEngine.SpawnObject(temp, 1);
            }
        }
    }

    private void InitMercenaryZones()
    {
        List<SiegeMercenaryZone> mercs = GetSiegeLocation().GetSiegeMercenaryZones(); // can be null if not implemented
        if (mercs == null)
            return;
        foreach (SiegeMercenaryZone zone in mercs)
        {
            MercenaryLocation mLoc = new MercenaryLocation(zone, GetSiegeLocation().GetRace(), GetSiegeLocationId());
            activeMercenaryLocs[zone.GetId()] = mLoc;
        }
    }

    protected override void OnSiegeFinish()
    {
        if (LoggingConfig.LOG_SIEGE)
        {
            SiegeRace oldRace = GetSiegeLocation().GetRace();
            int oldLegionId = GetSiegeLocation().GetLegionId();
            if (IsBossKilled())
            {
                SiegeRaceCounter winner = GetWinnerRaceCounter();
                log.LogInformation(this + ": Siege finished. Old race: " + oldRace + ", legion ID: " + oldLegionId + " -> New race: " + winner.GetSiegeRace()
                    + ", legion ID: " + (winner.GetWinnerLegionId() == null ? 0 : winner.GetWinnerLegionId()));
            }
            else
            {
                log.LogInformation(this + ": Siege finished. No winner found. Race: " + oldRace + ", legion ID: " + oldLegionId);
            }
        }

        // despawn protectors and make fortress invulnerable
        SiegeService.GetInstance().DeSpawnNpcs(GetSiegeLocationId());
        // need to remove balance buff before vulnerability is set to false
        GetSiegeLocation().ForEachPlayer(p => GetSiegeLocation().CheckForBalanceBuff(p, FortressLocation.SiegeBuffAction.SIEGE_END_REMOVE));
        GetSiegeLocation().SetVulnerable(false);
        GetSiegeLocation().SetUnderShield(false);

        // Guardian deity general was not killed, fortress stays with previous
        if (IsBossKilled())
        {
            OnCapture();
            BroadcastUpdate(GetSiegeLocation());
        }
        else
        {
            OnDefended();
            BroadcastState(GetSiegeLocation());
        }
        GetSiegeLocation().AdjustFactionBalance(GetFactionBalanceAdjustment());

        SiegeService.GetInstance().SpawnNpcs(GetSiegeLocationId(), GetSiegeLocation().GetRace(), SiegeModType.PEACE);

        // Reward players and owning legion
        SiegeRace winnerRace = GetSiegeLocation().GetRace();
        if (winnerRace != SiegeRace.BALAUR)
        {
            SiegeRace loserRace = winnerRace == SiegeRace.ASMODIANS ? SiegeRace.ELYOS : SiegeRace.ASMODIANS;
            SiegeRaceCounter winnerRaceCounter = GetSiegeCounter().GetRaceCounter(winnerRace);
            SiegeRaceCounter loserRaceCounter = GetSiegeCounter().GetRaceCounter(loserRace);
            SendRewardsToParticipants(winnerRaceCounter, IsBossKilled() ? SiegeResult.OCCUPY : SiegeResult.DEFENDER);
            SendRewardsToParticipants(loserRaceCounter, IsBossKilled() ? SiegeResult.FAIL : SiegeResult.EMPTY);
            DistributeLegionRewards(winnerRaceCounter);
        }
        else if (SiegeConfig.SIEGE_REWARD_BALAUR_VICTORY)
        {
            SendRewardsToParticipants(GetSiegeCounter().GetRaceCounter(SiegeRace.ASMODIANS), IsBossKilled() ? SiegeResult.FAIL : SiegeResult.EMPTY);
            SendRewardsToParticipants(GetSiegeCounter().GetRaceCounter(SiegeRace.ELYOS), IsBossKilled() ? SiegeResult.FAIL : SiegeResult.EMPTY);
        }

        // Update outpost status
        // Certain fortresses are changing outpost ownership
        UpdateOutpostStatusByFortress(GetSiegeLocation());

        // Update data in the DB
        SiegeDAO.UpdateSiegeLocation(GetSiegeLocation());
        if (IsBossKilled())
        {
            GetSiegeLocation().ForEachPlayer(p =>
            {
                if (SiegeRace.GetByRace(p.GetRace()) == GetSiegeLocation().GetRace())
                    QuestEngine.GetInstance().OnKill(new QuestEnv(GetBoss(), p, 0));
            });
        }
    }

    private void OnCapture()
    {
        SiegeRaceCounter winner = GetWinnerRaceCounter();
        SiegeRace winnerRace = winner.GetSiegeRace();
        SiegeRace oldRace = GetSiegeLocation().GetRace();
        Legion oldLegion = oldLegionId == 0 ? null : LegionService.GetInstance().GetLegion(oldLegionId);

        // Players gain buffs on capture of some fortresses
        ApplyWorldBuffs(winnerRace, false);
        // Set new fortress and artifact owner race
        GetSiegeLocation().SetRace(winnerRace);
        GetArtifact().SetRace(winnerRace);

        // reset occupy count
        GetSiegeLocation().SetOccupiedCount(winnerRace == SiegeRace.BALAUR ? 0 : 1);

        // If new race is balaur
        if (SiegeRace.BALAUR == winnerRace)
        {
            GetSiegeLocation().SetLegionId(0);
            GetArtifact().SetLegionId(0);
        }
        else
        {
            int? topLegionId = winner.GetWinnerLegionId();
            GetSiegeLocation().SetLegionId(topLegionId ?? 0);
            GetArtifact().SetLegionId(topLegionId ?? 0);
        }

        // announce
        string locL10n = GetSiegeLocation().GetTemplate().GetL10n();
        Legion winnerLegion = GetSiegeLocation().GetLegionId() == 0 ? null : LegionService.GetInstance().GetLegion(GetSiegeLocation().GetLegionId());
        SM_SYSTEM_MESSAGE loserMsg = GetLoserMsg(oldRace, oldLegion, locL10n);
        SM_SYSTEM_MESSAGE winnerMsg = GetWinnerMsg(winnerRace, winnerLegion, locL10n);
        World.GetInstance().ForEachPlayer(player =>
        {
            if (player.GetRace().GetRaceId() == oldRace.GetRaceId())
                PacketSendUtility.SendPacket(player, loserMsg);
            else
                PacketSendUtility.SendPacket(player, winnerMsg);
        });
    }

    private SM_SYSTEM_MESSAGE GetWinnerMsg(SiegeRace winnerRace, Legion winnerLegion, string locationName)
    {
        if (winnerLegion == null)
            return SM_SYSTEM_MESSAGE.STR_ABYSS_WIN_CASTLE(winnerRace.GetL10n(), locationName);
        else
            return SM_SYSTEM_MESSAGE.STR_ABYSS_GUILD_WIN_CASTLE(winnerLegion.GetName(), locationName);
    }

    private SM_SYSTEM_MESSAGE GetLoserMsg(SiegeRace loserRace, Legion oldLegion, string locationName)
    {
        if (oldLegion == null)
            return SM_SYSTEM_MESSAGE.STR_ABYSS_CASTLE_TAKEN(loserRace.GetL10n(), locationName);
        else
            return SM_SYSTEM_MESSAGE.STR_ABYSS_GUILD_CASTLE_TAKEN(oldLegion.GetName(), locationName);
    }

    private int GetFactionBalanceAdjustment()
    {
        switch (GetSiegeLocation().GetRace())
        {
            case SiegeRace.ELYOS:
                return 1;
            case SiegeRace.ASMODIANS:
                return -1;
            case SiegeRace.BALAUR:
                int b = GetSiegeLocation().GetFactionBalance();
                if (b > 0)
                    return -1;
                else if (b < 0)
                    return 1;
                break;
        }
        return 0;
    }

    private void OnDefended()
    {
        // Increase fortress occupied count
        if (GetSiegeLocation().GetRace() != SiegeRace.BALAUR && GetSiegeLocation().GetTemplate().GetMaxOccupyCount() > 0)
        {
            GetSiegeLocation().IncreaseOccupiedCount();
        }

        // Players gain buffs for successfully defense / failed capture the fortress
        ApplyWorldBuffs(GetSiegeLocation().GetRace(), true);
    }

    private void ApplyWorldBuffs(SiegeRace winner, bool isDefense)
    {
        int skillId;

        switch (GetSiegeLocation().GetLocationId())
        {
            case 1131:
                skillId = 12147;
                break;
            case 1132:
                skillId = 12148;
                break;
            case 1141:
                skillId = 12149;
                break;
            case 1221:
                skillId = 12075;
                break;
            case 1231:
                skillId = 12076;
                break;
            case 1241:
                skillId = 12077;
                break;
            case 1251:
                skillId = 12074;
                break;
            case 2011:
                skillId = 12155;
                break;
            case 2021:
                skillId = 12156;
                break;
            case 3011:
                skillId = 12157;
                break;
            case 3021:
                skillId = 12158;
                break;
            default:
                return;
        }

        string skillL10n = DataManager.SKILL_DATA.GetSkillTemplate(skillId).GetL10n();
        SM_SYSTEM_MESSAGE notification = isDefense
            ? SM_SYSTEM_MESSAGE.STR_CASTLE_DEFENCE_WIN_BUFF_ON(winner.GetL10n(), GetSiegeLocation().GetTemplate().GetL10n(), skillL10n)
            : SM_SYSTEM_MESSAGE.STR_CASTLE_WIN_BUFF_ON(winner.GetL10n(), GetSiegeLocation().GetTemplate().GetL10n(), skillL10n);
        World.GetInstance().ForEachPlayer(player =>
        {
            if (player.GetRace().GetRaceId() == winner.GetRaceId())
            {
                SkillEngine.GetInstance().ApplyEffectDirectly(skillId, player, player);
                PacketSendUtility.SendPacket(player, notification);
            }
        });
    }

    private void DistributeLegionRewards(SiegeRaceCounter winnerRaceCounter)
    {
        int legionId = GetSiegeLocation().GetLegionId();
        Legion legion = legionId == 0 ? null : LegionService.GetInstance().GetLegion(legionId);
        if (legion == null)
        {
            if (LoggingConfig.LOG_SIEGE)
                log.LogInformation(this + ": Skipped sending legion rewards because the fortress is not owned by any legion (owner race: "
                    + GetSiegeLocation().GetRace() + ").");
            return;
        }
        DistributeLegionGp(legion, winnerRaceCounter);
        DistributeLegionRewards(legion);
    }

    private void DistributeLegionGp(Legion legion, SiegeRaceCounter src)
    {
        int legionGp = GetSiegeLocation().GetLegionGp();
        if (legionGp <= 0)
            return;
        try
        {
            HashSet<int> participatedLegionMembers = new HashSet<int>(src.GetPlayerAbyssPoints().Keys);
            participatedLegionMembers.IntersectWith(legion.GetMemberIds());

            if (participatedLegionMembers.Count == 0)
            {
                if (LoggingConfig.LOG_SIEGE)
                    log.LogInformation(this + ": Distributed no GP to the members of " + legion + " because no one made AP");
            }
            else
            {
                int gp = Math.Min((int)Math.Floor(legionGp / (float)participatedLegionMembers.Count + 0.5f), SiegeConfig.LEGION_GP_CAP_PER_MEMBER);
                foreach (int participant in participatedLegionMembers)
                    GloryPointsService.AddGp(participant, gp);
                if (LoggingConfig.LOG_SIEGE)
                    log.LogInformation(this + ": Distributed " + gp + " GP each, to the following members of " + legion + ": " + participatedLegionMembers);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while distributing legion GP for " + this);
        }
    }

    private void DistributeLegionRewards(Legion legion)
    {
        List<SiegeLegionReward> legionRewards = GetSiegeLocation().GetLegionRewards();
        if (legionRewards == null || legionRewards.Count == 0)
            return;
        try
        {
            long totalKinah = 0;
            int nonKinahItems = 0;
            PlayerCommonData brigadeGeneral = PlayerService.GetOrLoadPlayerCommonData(legion.GetBrigadeGeneral().GetObjectId());
            foreach (SiegeLegionReward item in legionRewards)
            {
                if (item.GetItemId() == ItemId.KINAH)
                {
                    long kinah = IsBossKilled() ? item.GetItemCount() : (long)Math.Floor(item.GetItemCount() * 0.7f + 0.5f);
                    legion.GetLegionWarehouse().IncreaseKinah(kinah);
                    LegionService.GetInstance().AddRewardHistory(legion, kinah, IsBossKilled() ? LegionHistoryAction.OCCUPATION : LegionHistoryAction.DEFENSE,
                        GetSiegeLocationId());
                    totalKinah += kinah;
                }
                else
                {
                    nonKinahItems++;
                    MailFormatter.SendAbyssRewardMail(GetSiegeLocation(), brigadeGeneral, AbyssSiegeLevel.NONE, SiegeResult.PROTECT, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        item.GetItemId(), item.GetItemCount(), 0);
                }
            }
            if (LoggingConfig.LOG_SIEGE)
            {
                string msg = "";
                if (totalKinah > 0)
                    msg += "Added " + totalKinah + " kinah to the legion warehouse";
                if (nonKinahItems > 0)
                    msg += (msg.Length == 0 ? "Sent " : " and sent ") + nonKinahItems + " legion rewards to brigade general " + brigadeGeneral.GetName() + " of "
                        + legion + " (see sysmail.log)";
                log.LogInformation(this + ": " + msg);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while distributing legion rewards for " + this);
        }
    }

    public override bool IsEndless()
    {
        return false;
    }

    public override void OnAbyssPointsAdded(Player player, int abyssPoints)
    {
        if (GetSiegeLocation().IsVulnerable() && GetSiegeLocation().IsInsideLocation(player))
            GetSiegeCounter().AddAbyssPoints(player, abyssPoints);
    }

    protected ArtifactLocation GetArtifact()
    {
        return SiegeService.GetInstance().GetFortressArtifact(GetSiegeLocationId());
    }

    protected bool HasArtifact()
    {
        return GetArtifact() != null;
    }

    public MercenaryLocation GetMercenaryLocationByZoneId(int zoneId)
    {
        return activeMercenaryLocs.GetValueOrDefault(zoneId);
    }
}
