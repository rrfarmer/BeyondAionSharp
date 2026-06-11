using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Skillengine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/OutpostSiege (SoulKeeper, Estrayl) extends Siege&lt;OutpostLocation&gt;. Field-abyss outpost (agent boss) siege: onSiegeStart (vulnerable, respawn siege npcs, boss, spawn announce), onSiegeFinish (agent defeated rewards or empty rewards, peace respawn), onAgentDefeated (top-damager announce + winner-race buff), despawnSiegeNpcs, AP accrual when vulnerable+inside. keySet().iterator().next()->Keys.First(); forEachPlayer lambda; Map->Dictionary; loc 2111=light/else dark. OutpostLocation/SiegeNpc/SiegeResult/SM_ red-tolerated.</summary>
public class OutpostSiege : Siege<OutpostLocation>
{
    public OutpostSiege(OutpostLocation siegeLocation)
        : base(siegeLocation)
    {
    }

    protected override void OnSiegeStart()
    {
        GetSiegeLocation().SetVulnerable(true);
        DespawnNpcs(GetSiegeLocationId());
        SpawnNpcs(GetSiegeLocationId(), GetSiegeLocation().GetRace(), SiegeModType.SIEGE);
        InitSiegeBoss();

        PacketSendUtility.BroadcastToWorld(
            GetSiegeLocationId() == 2111 ? SM_SYSTEM_MESSAGE.STR_FIELDABYSS_LIGHTBOSS_SPAWN() : SM_SYSTEM_MESSAGE.STR_FIELDABYSS_DARKBOSS_SPAWN());
        BroadcastUpdate(GetSiegeLocation());
    }

    protected override void OnSiegeFinish()
    {
        GetSiegeLocation().SetVulnerable(false);
        DespawnSiegeNpcs();

        if (IsBossKilled())
        {
            OnAgentDefeated();
            SendRewardsToParticipants(GetWinnerRaceCounter(), SiegeResult.OCCUPY);
            SendRewardsToParticipants(GetSiegeCounter().GetRaceCounter(GetSiegeLocationId() == 2111 ? SiegeRace.ELYOS : SiegeRace.ASMODIANS),
                SiegeResult.FAIL);
        }
        else
        {
            PacketSendUtility.BroadcastToWorld(
                GetSiegeLocationId() == 2111 ? SM_SYSTEM_MESSAGE.STR_FIELDABYSS_LIGHTBOSS_DESPAWN() : SM_SYSTEM_MESSAGE.STR_FIELDABYSS_DARKBOSS_DESPAWN());
            SendRewardsToParticipants(GetSiegeCounter().GetRaceCounter(SiegeRace.ELYOS), SiegeResult.EMPTY);
            SendRewardsToParticipants(GetSiegeCounter().GetRaceCounter(SiegeRace.ASMODIANS), SiegeResult.EMPTY);
        }
        BroadcastUpdate(GetSiegeLocation());
        SpawnNpcs(GetSiegeLocationId(), GetSiegeLocation().GetRace(), SiegeModType.PEACE);
    }

    private void OnAgentDefeated()
    {
        SiegeRaceCounter winnerCounter = GetWinnerRaceCounter();
        Dictionary<int, long> topPlayerDamages = winnerCounter.GetPlayerDamageCounter();
        if (topPlayerDamages.Count != 0)
        {
            int topPlayerId = topPlayerDamages.Keys.First();
            PlayerCommonData pcd = PlayerService.GetOrLoadPlayerCommonData(topPlayerId);
            string playerName = pcd.GetName();
            string playerRace = pcd.GetRace().GetL10n();
            PacketSendUtility.BroadcastToWorld(GetSiegeLocationId() == 2111 ? SM_SYSTEM_MESSAGE.STR_FIELDABYSS_LIGHTBOSS_KILLED(playerName, playerRace)
                : SM_SYSTEM_MESSAGE.STR_FIELDABYSS_DARKBOSS_KILLED(playerName, playerRace));
            Race winnerRace = winnerCounter.GetSiegeRace() == SiegeRace.ELYOS ? Race.ELYOS : Race.ASMODIANS;

            World.GetInstance().ForEachPlayer(p =>
            {
                if (p.GetRace().Equals(winnerRace))
                    SkillEngine.GetInstance().ApplyEffectDirectly(winnerRace == Race.ELYOS ? 12120 : 12119, p, p);
            });
        }
    }

    public void DespawnSiegeNpcs()
    {
        ICollection<SiegeNpc> npcs = World.GetInstance().GetLocalSiegeNpcs(GetSiegeLocationId());
        foreach (SiegeNpc npc in npcs)
        {
            if (npc != null)
                npc.GetController().DeleteIfAliveOrCancelRespawn();
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
}
