using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Panesterra;
using Aion.GameServer.Services.Panesterra.Ahserion;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/panesterra/AdvanceCorridorAI (Estrayl).</summary>
[AIName("panesterra_advance_corridor")]
public class AdvanceCorridorAI : GeneralNpcAI
{
    protected int despawnInMin = 10;

    public AdvanceCorridorAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        GetOwner().GetController().AddTask(TaskId.DESPAWN,
            ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetController().DeleteIfAliveOrCancelRespawn(); return ValueTask.CompletedTask; }, TimeSpan.FromMinutes(despawnInMin)));
    }

    protected override void HandleDialogStart(Player player)
    {
        if ((int)player.GetAbyssRank().GetRank() < (int)AbyssRankEnum.STAR1_OFFICER)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_TELEPOTER_GAB1_USER07());
            return;
        }
        if (player.GetLevel() < 65)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_SVS_DIRECT_PORTAL_LEVEL_LIMIT());
            return;
        }
        PanesterraFaction faction = GetFactionToAssign(player);
        AIActions.AddRequest(this, player, SM_QUESTION_WINDOW.STR_ASK_PASS_BY_SVS_DIRECT_PORTAL, new AdvanceCorridorRequest(player, faction));
    }

    private sealed class AdvanceCorridorRequest : AIRequest
    {
        private readonly Player player;
        private readonly PanesterraFaction faction;

        public AdvanceCorridorRequest(Player player, PanesterraFaction faction)
        {
            this.player = player;
            this.faction = faction;
        }

        public override void AcceptRequest(Creature requester, Player responder, int requestId)
        {
            if (PanesterraService.GetInstance().GetTeamMemberCount(faction) >= SiegeConfig.PANESTERRA_MAX_PLAYERS_PER_TEAM)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_SVS_DIRECT_PORTAL_USE_COUNT_LIMIT());
                return;
            }
            PanesterraTeam team = PanesterraService.GetInstance().GetTeam(faction);
            team.AddTeamMemberIfAbsent(player.GetObjectId());
            team.MovePlayerToStartPosition(player);
        }
    }

    /// <summary>
    /// Hard-coded for now
    /// TODO: Faction selection should be moved into a dedicated matchmaking service
    /// </summary>
    private PanesterraFaction GetFactionToAssign(Player player)
    {
        return player.GetRace() == Race.ELYOS ? PanesterraFaction.IVY_TEMPLE : PanesterraFaction.ALPINE_TEMPLE;
    }
}
