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

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Yeats, Estrayl
/// </summary>
[AIName("ahserion_registration_corridor")]
public class AhserionRegistrationCorridor : GeneralNpcAI
{
    private PanesterraFaction faction;

    public AhserionRegistrationCorridor(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_SVS_INVADE_DIRECT_PORTAL_OPEN());
        switch (GetNpcId())
        {
            case 802219:
                faction = PanesterraFaction.BELUS;
                break;
            case 802221:
                faction = PanesterraFaction.ASPIDA;
                break;
            case 802223:
                faction = PanesterraFaction.ATANATOS;
                break;
            case 802225:
                faction = PanesterraFaction.DISILLON;
                break;
        }
        GetOwner().GetController().AddTask(TaskId.DESPAWN,
            ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetController().DeleteIfAliveOrCancelRespawn(); return ValueTask.CompletedTask; }, TimeSpan.FromMinutes(10)));
    }

    protected override void HandleDialogStart(Player player)
    {
        if (player.GetLevel() < 65)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_SVS_DIRECT_PORTAL_LEVEL_LIMIT());
            return;
        }
        AIActions.AddRequest(this, player, SM_QUESTION_WINDOW.STR_ASK_PASS_BY_SVS_DIRECT_PORTAL, new RegistrationRequest(this, player));
    }

    private sealed class RegistrationRequest : AIRequest
    {
        private readonly AhserionRegistrationCorridor _ai;
        private readonly Player _player;

        public RegistrationRequest(AhserionRegistrationCorridor ai, Player player)
        {
            _ai = ai;
            _player = player;
        }

        public override void AcceptRequest(Creature requester, Player responder, int requestId)
        {
            if (!AhserionRaid.GetInstance().IsStarted())
            {
                PacketSendUtility.SendPacket(_player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_READY_PANGAEA());
                return;
            }
            else if (PanesterraService.GetInstance().GetTeamMemberCount(_ai.faction) >= SiegeConfig.AHSERION_MAX_PLAYERS_PER_TEAM)
            {
                PacketSendUtility.SendPacket(_player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_SVS_DIRECT_PORTAL_USE_COUNT_LIMIT());
                return;
            }
            PanesterraTeam team = PanesterraService.GetInstance().GetTeam(_ai.faction);
            team.AddTeamMemberIfAbsent(_player.GetObjectId());
            team.MovePlayerToStartPosition(_player);
            _player.SetPanesterraFaction(team.GetFaction());
        }
    }
}
