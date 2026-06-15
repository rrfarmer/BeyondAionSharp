using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/KiskAI (ATracer, Source).</summary>
[AIName("kisk")]
public class KiskAI : NpcAI
{
    public KiskAI(Npc owner)
        : base(owner)
    {
    }

    public new Kisk GetOwner()
    {
        return (Kisk)base.GetOwner();
    }

    protected override void HandleAttack(Creature creature)
    {
        if (GetLifeStats().IsFullyRestoredHp())
            GetOwner().BroadcastPacket(SM_SYSTEM_MESSAGE.STR_BINDSTONE_IS_ATTACKED());
    }

    protected override void HandleDespawned()
    {
        KiskService.GetInstance().RemoveKisk(GetOwner());
        if (IsDead())
            GetOwner().BroadcastPacket(SM_SYSTEM_MESSAGE.STR_BINDSTONE_IS_DESTROYED());
        else
            GetOwner().BroadcastPacket(SM_SYSTEM_MESSAGE.STR_BINDSTONE_IS_REMOVED());
        base.HandleDespawned();
    }

    protected override void HandleDialogStart(Player player)
    {
        if (player.GetKisk() == GetOwner())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_BINDSTONE_ALREADY_REGISTERED());
            return;
        }

        if (GetOwner().CanBind(player))
        {
            AIActions.AddRequest(this, player, SM_QUESTION_WINDOW.STR_ASK_REGISTER_BINDSTONE, new RegisterBindstoneRequest(this));
        }
        else if (GetOwner().GetCurrentMemberCount() >= GetOwner().GetMaxMembers())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_REGISTER_BINDSTONE_FULL());
        }
        else
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_REGISTER_BINDSTONE_HAVE_NO_AUTHORITY());
        }
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_DECAY:
            case AIQuestion.ALLOW_RESPAWN:
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
            case AIQuestion.REMOVE_EFFECTS_ON_MAP_REGION_DEACTIVATE:
                return false;
            case AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES:
                return true;
            default:
                return base.Ask(question);
        }
    }

    private sealed class RegisterBindstoneRequest : AIRequest
    {
        private readonly KiskAI ai;
        private bool decisionTaken = false;

        public RegisterBindstoneRequest(KiskAI ai)
        {
            this.ai = ai;
        }

        public override void AcceptRequest(Creature requester, Player responder, int requestId)
        {
            if (!decisionTaken)
            {
                // Check again if it's full (If they waited to press OK)
                if (!ai.GetOwner().CanBind(responder))
                {
                    PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_CANNOT_REGISTER_BINDSTONE_HAVE_NO_AUTHORITY());
                    return;
                }
                KiskService.GetInstance().OnBind(ai.GetOwner(), responder);
            }
        }

        public override void DenyRequest(Creature requester, Player responder)
        {
            decisionTaken = true;
        }
    }
}
