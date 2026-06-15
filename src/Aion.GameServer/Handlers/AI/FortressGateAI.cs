using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Siegelocation;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/FortressGateAI (@author Source).</summary>
[AIName("fortressgate")]
public class FortressGateAI : NpcAI
{
    public FortressGateAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        AIActions.AddRequest(this, player, SM_QUESTION_WINDOW.STR_ASK_PASS_BY_GATE, new FortressGateRequest());
    }

    private sealed class FortressGateRequest : AIRequest
    {
        public override void AcceptRequest(Creature fortressGate, Player responder, int requestId)
        {
            if (PositionUtil.IsInTalkRange(responder, (Npc)fortressGate))
            {
                TeleportService.MoveToTargetWithDistance(fortressGate, responder, PositionUtil.IsBehind(responder, fortressGate) ? 0 : 1, 3);
            }
            else
            {
                PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_FAR_FROM_NPC());
            }
        }
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
            case AIQuestion.REWARD_AP:
                return true;
            case AIQuestion.REWARD_LOOT:
            case AIQuestion.ALLOW_RESPAWN:
                return false;
            default:
                return base.Ask(question);
        }
    }

    protected override void HandleDied()
    {
        if (GetSpawnTemplate() is SiegeSpawnTemplate spawnTemplate)
        {
            DoorRepairData repairData = SiegeService.GetInstance().GetDoorRepairData(spawnTemplate.GetSiegeId());
            if (repairData != null)
            {
                foreach (DoorRepairStone stone in repairData.GetRepairStones())
                {
                    if (stone.GetDoorId() == GetSpawnTemplate().GetStaticId())
                    {
                        VisibleObject obj = GetPosition().GetWorldMapInstance().GetObjectByStaticId(stone.GetStaticId());
                        if (obj != null)
                            obj.GetController().Delete();
                        break;
                    }
                }
            }
        }

        base.HandleDied();
    }
}
