using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Custom.Pvpmap;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Event;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Templates.Bounty;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Services.ConquerorAndProtectorSystem;
using Aion.GameServer.Services.Event;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.Utils.Collections;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/PvpService (Sarynth, Estrayl). PvP kill reward distribution. **TemporaryPlayerTeam&lt;?&gt;/&lt;? extends TeamMember&lt;Player&gt;&gt;→TemporaryPlayerTeam&lt;TeamMember&lt;Player&gt;&gt;** (codebase invariance bound); putIfAbsent→lock+TryGetValue; instanceof X x→is X x; Math.round(float)→(int)Math.Floor(+0.5f); removeIf→RemoveAll; stream/map/collect→LINQ; PersistentState→IPersistable.PersistentState; enum.name()→ToString(); equalsIgnoreCase→OrdinalIgnoreCase; currentTimeMillis→UtcNow. Many services/SM_*/DAO red-tolerated.</summary>
public class PvpService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("KILL_LOG");
    private readonly List<KillBountyTemplate> killBounties;
    private readonly IDictionary<int, Headhunter> headhunters;

    private PvpService()
    {
        killBounties = DataManager.KILL_BOUNTY_DATA.GetKillBounties();
        headhunters = HeadhuntingDAO.LoadHeadhunters();
    }

    public static PvpService GetInstance()
    {
        return SingletonHolder.INSTANCE;
    }

    private void SendBountyReward(Player player, BountyType type, int killScore)
    {
        foreach (KillBountyTemplate template in killBounties)
        {
            if (template.GetBountyType() != type || template.GetKillCount() != killScore)
                continue;
            if (template.GetRaceCondition() != Race.PC_ALL && template.GetRaceCondition() != player.GetRace())
                continue;
            List<BountyTemplate> bounties = new List<BountyTemplate>();
            if (template.IsRandomReward())
                bounties.Add(Rnd.Get(template.GetBounties()));
            else
                bounties.AddRange(template.GetBounties());

            foreach (BountyTemplate bounty in bounties)
                ItemService.AddItem(player, bounty.GetItemId(), bounty.GetCount(), true,
                    new ItemService.ItemUpdatePredicate(ItemPacketService.ItemAddType.ITEM_COLLECT, ItemPacketService.ItemUpdateType.INC_CASH_ITEM));
        }
    }

    public void FinalizeHeadhuntingSeason()
    {
        headhunters.Clear();
    }

    public void DoReward(Player victim)
    {
        DoReward(victim, 1);
    }

    public Headhunter GetHeadhunterById(int objId)
    {
        lock (this)
        {
            Headhunter created = new Headhunter(objId, 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), IPersistable.PersistentState.UPDATE_REQUIRED);
            // Java putIfAbsent: returns existing if present, else inserts created and returns it.
            if (headhunters.TryGetValue(objId, out Headhunter existing))
                return existing;
            headhunters[objId] = created;
            return created;
        }
    }

    public void DoReward(Player victim, float apWinMulti)
    {
        DamageList damageList = victim.GetAggroList().GetFinalDamageList();
        DamageInfo<Creature> mostDamage = damageList.GetMostDamage();
        if (mostDamage == null || !(mostDamage.GetAttacker() is Player winner))
        {
            PacketSendUtility.SendPacket(victim, SM_SYSTEM_MESSAGE.STR_MSG_COMBAT_MY_DEATH());
            TemporaryPlayerTeam<ITeamMember<Player>> team = victim.GetCurrentTeam();
            if (team != null)
                team.SendPacket(Predicates.Players.AllExcept(victim), SM_SYSTEM_MESSAGE.STR_MSG_COMBAT_FRIENDLY_DEATH(victim.GetName()));
            AbyssService.AnnounceHighRankedDeath(victim);
            return;
        }

        List<Player> killers = FindMembersToCountKillFor(winner, victim);
        if (killers.Count != 0)
        {
            foreach (Player killer in killers)
            {
                killer.GetAbyssRank().IncrementAllKills();
                if (CustomConfig.ENABLE_KILL_REWARD)
                {
                    int kills = killer.GetAbyssRank().GetAllKill();
                    foreach (KillBountyTemplate template in killBounties)
                    {
                        if (template.GetBountyType() == BountyType.PER_X_KILLS)
                        {
                            int killStep = template.GetKillCount();
                            if (kills % killStep == 0)
                                SendBountyReward(killer, BountyType.PER_X_KILLS, killStep);
                        }
                    }
                }
                if (EventsConfig.ENABLE_HEADHUNTING && EventsConfig.HEADHUNTING_MAPS.Contains(victim.GetWorldId()))
                {
                    int kills = GetHeadhunterById(killer.GetObjectId()).IncrementAndGetKills();
                    SendBountyReward(killer, BountyType.SEASONAL_KILLS, kills);
                }
            }
            UpdateKillQuests(killers, victim);
            if (killers.Contains(winner)) // rewards for winner only (group members are ignored)
            {
                ConquerorAndProtectorService.GetInstance().OnKill(winner, victim);
                EventService.GetInstance().OnPvpKill(winner, victim);
            }
        }

        LogKill(winner, victim, killers);

        // track how much of the total damage actually generated AP (ignoring Duels, Arena, NPCs), so the victim loses his AP based on that fraction
        int apRelevantDamage = 0;
        int totalDamage = damageList.GetTotalDamage();

        // Distribute AP to groups and players that had damage.
        foreach (DamageInfo<AionObject> damageInfo in damageList.ToTeamDamages().GetCreatureOrTeamDamages())
        {
            ICollection<Player> teamMembers = new List<Player>();
            AionObject attacker = damageInfo.GetAttacker();
            if (attacker is Player player && player.GetRace() != victim.GetRace())
                teamMembers.Add(player);
            else if (attacker is TemporaryPlayerTeam<ITeamMember<Player>> team && team.GetLeaderObject().GetRace() != victim.GetRace())
                teamMembers = team.GetMembers();

            // Add damage last, so we don't include damage from same race. (Duels, Arena)
            if (RewardPlayerTeam(teamMembers, victim, damageInfo.GetDamage(), totalDamage, apWinMulti))
                apRelevantDamage += damageInfo.GetDamage();
        }

        // Apply lost AP to defeated player
        int apLost = StatFunctions.CalculatePvPApLost(victim, winner);
        int apActuallyLost = apLost * apRelevantDamage / totalDamage;

        if (apActuallyLost > 0)
            AbyssPointsService.AddAp(victim, -apActuallyLost);

        // Announce that player has died.
        if (victim.IsInInstance() && !PvpMapService.GetInstance().IsOnPvPMap(victim))
        {
            PacketSendUtility.BroadcastPacketAndReceive(victim, SM_SYSTEM_MESSAGE.STR_MSG_COMBAT_FRIENDLY_DEATH_TO_B(victim.GetName(), winner.GetName()));
            PacketSendUtility.SendPacket(victim, SM_SYSTEM_MESSAGE.STR_MSG_COMBAT_MY_DEATH());
        }
        else
        {
            PacketSendUtility.SendPacket(winner, SM_SYSTEM_MESSAGE.STR_MSG_COMBAT_HOSTILE_DEATH_TO_ME(victim.GetName()));
            PacketSendUtility.SendPacket(victim, SM_SYSTEM_MESSAGE.STR_MSG_COMBAT_MY_DEATH_TO_B(winner.GetName()));
            PacketSendUtility.BroadcastPacket(victim, SM_SYSTEM_MESSAGE.STR_MSG_COMBAT_FRIENDLY_DEATH_TO_B(victim.GetName(), winner.GetName()), false,
                player => !player.IsEnemy(victim));
            PacketSendUtility.BroadcastPacket(winner, SM_SYSTEM_MESSAGE.STR_MSG_COMBAT_HOSTILE_DEATH_TO_B(winner.GetName(), victim.GetName()), false,
                player => player.IsEnemy(victim));
            AbyssService.AnnounceHighRankedDeath(victim);
        }
    }

    private List<Player> FindMembersToCountKillFor(Player winner, Player victim)
    {
        TemporaryPlayerTeam<ITeamMember<Player>> group = winner.GetCurrentGroup();
        List<Player> killers;
        if (group == null)
            killers = new List<Player> { winner };
        else
            killers = group.GetMembers();
        killers.RemoveAll(m => !m.IsOnline() || m.GetRace() == victim.GetRace() || !m.Equals(winner) && !PositionUtil.IsInRange(m, victim, 50));
        return killers;
    }

    private void LogKill(Player winner, Player victim, List<Player> assistedGroup)
    {
        if (LoggingConfig.LOG_KILL)
        {
            if (assistedGroup.Count > 1 || assistedGroup.Count == 1 && !assistedGroup.Contains(winner))
                log.LogInformation("[KILL] " + winner + " killed " + victim + " assisted by "
                    + string.Join(",", assistedGroup.Where(p => !p.Equals(winner)).Select(p => p.ToString())));
            else
                log.LogInformation("[KILL] " + winner + " killed " + victim);
        }

        if (LoggingConfig.LOG_PL)
        {
            string ip1 = winner.GetClientConnection().GetIP();
            string mac1 = winner.GetClientConnection().GetMacAddress();
            string ip2 = victim.GetClientConnection().GetIP();
            string mac2 = victim.GetClientConnection().GetMacAddress();
            if (mac1 != null && mac2 != null)
            {
                if (string.Equals(ip1, ip2, StringComparison.OrdinalIgnoreCase) && string.Equals(mac1, mac2, StringComparison.OrdinalIgnoreCase))
                {
                    AuditLogger.Log(winner, "possibly practicing AP sharing with " + victim + " same ip=" + ip1 + " and mac=" + mac1 + ".");
                }
                else if (string.Equals(mac1, mac2, StringComparison.OrdinalIgnoreCase))
                {
                    AuditLogger.Log(winner, "possibly practicing AP sharing with " + victim + " same mac=" + mac1 + ".");
                }
            }
        }
    }

    private bool RewardPlayerTeam(ICollection<Player> teamMember, Player victim, int damage, int totalDamage, float apWinMulti)
    {
        List<Player> players = new List<Player>();
        int maxRank = 1;
        int maxLevel = 0;

        foreach (Player member in teamMember)
        {
            if (!member.IsOnline() || member.IsDead() || !PositionUtil.IsInRange(member, victim, GroupConfig.GROUP_MAX_DISTANCE))
                continue;
            players.Add(member);
            if (member.GetLevel() > maxLevel)
                maxLevel = member.GetLevel();
            if (member.GetAbyssRank().GetRank().GetId() > maxRank)
                maxRank = member.GetAbyssRank().GetRank().GetId();
        }
        // They are all dead or out of range.
        if (players.Count == 0)
            return false;

        float baseApReward = StatFunctions.CalculatePvpApGained(victim, maxRank, maxLevel) * apWinMulti;
        int baseXpReward = StatFunctions.CalculatePvpXpGained(victim, maxRank, maxLevel);
        int baseDpReward = StatFunctions.CalculatePvpDpGained(victim, maxRank, maxLevel);
        float groupDamagePercentage = (float)damage / totalDamage;
        int apRewardPerMember = (int)Math.Floor(baseApReward * groupDamagePercentage / players.Count + 0.5f);
        int xpRewardPerMember = (int)Math.Floor(baseXpReward * groupDamagePercentage / players.Count + 0.5f);
        int dpRewardPerMember = (int)Math.Floor(baseDpReward * groupDamagePercentage / players.Count + 0.5f);

        foreach (Player member in players)
        {
            int memberApGain = 1;
            int memberXpGain = 1;
            int memberDpGain = 1;
            if (KillCounter.AddKillFor(member.GetObjectId(), victim.GetObjectId()) < CustomConfig.MAX_DAILY_PVP_KILLS)
            {
                if (apRewardPerMember > 0)
                    memberApGain = Rates.AP_PVP.CalcResult(member, apRewardPerMember);
                if (xpRewardPerMember > 0)
                    memberXpGain = xpRewardPerMember; // rates are applied in addExp()
                if (dpRewardPerMember > 0)
                {
                    memberDpGain = StatFunctions.AdjustPvpDpGained(dpRewardPerMember, victim.GetLevel(), member.GetLevel());
                    memberDpGain = Rates.DP_PVP.CalcResult(member, memberDpGain);
                }

            }
            AbyssPointsService.AddAp(member, victim, memberApGain);
            member.GetCommonData().AddExp(memberXpGain, Rates.XP_PVP, victim.GetName());
            member.GetCommonData().AddDp(memberDpGain);
        }
        return true;
    }

    private void UpdateKillQuests(List<Player> killers, Player victim)
    {
        List<ZoneInstance> zones = victim.FindZones();
        foreach (Player p in killers)
        {
            foreach (ZoneInstance zone in zones)
                Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnKillInZone(new QuestEnv(victim, p, 0), zone.GetAreaTemplate().GetZoneName().ToString());
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnKillInWorld(new QuestEnv(victim, p, 0), victim.GetWorldId());
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnKillRanked(new QuestEnv(victim, p, 0), victim.GetAbyssRank().GetRank());
        }
    }

    public IDictionary<int, Headhunter> GetAllHeadhunters()
    {
        return headhunters;
    }

    public Headhunter GetHeadhunter(int hunterId)
    {
        return headhunters.TryGetValue(hunterId, out var v) ? v : null;
    }

    private static class SingletonHolder
    {
        internal static readonly PvpService INSTANCE = new PvpService();
    }
}
