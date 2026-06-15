using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz
/// </summary>
[AIName("arminos_draky")]
public class ArminosDrakyAI : GeneralNpcAI
{
    private string walkerId = "300300001";
    private bool isStart = true;

    public ArminosDrakyAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        GetSpawnTemplate().SetWalkerId(walkerId);
        WalkManager.StartWalking(this);
        GetOwner().SetState(CreatureState.ACTIVE, true);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (GetOwner().GetMoveController().GetCurrentStep().IsLastStep()) // circle twice
        {
            if (!isStart)
            {
                GetSpawnTemplate().SetWalkerId(null);
                WalkManager.StopWalking(this);
                AIActions.DeleteOwner(this);
            }
            else
                isStart = false;
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES or AIQuestion.REWARD_AP => true,
            _ => base.Ask(question),
        };
    }

    public override bool CanThink()
    {
        return false;
    }
}
