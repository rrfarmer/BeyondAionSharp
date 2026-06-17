using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/stonespearReach/AethericFieldBlaststoneAI (Yeats).</summary>
[AIName("atheric_field_blaststone")]
public class AethericFieldBlaststoneAI : NpcAI
{
    public AethericFieldBlaststoneAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (GetNpcId() == 856305)
        {
            GetOwner().GetSpawn().SetWalkerId("301500000_clown_path");
            WalkManager.StartWalking(this);
            GetOwner().SetState(CreatureState.WALK_MODE);
            PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetOwner().GetObjectId()));
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        GetOwner().GetController().Delete();
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        MoveEventHandler.OnMoveArrived(this);
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_RESPAWN:
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
            case AIQuestion.REWARD_LOOT:
            case AIQuestion.ALLOW_DECAY:
                return false;
            default:
                return base.Ask(question);
        }
    }
}
