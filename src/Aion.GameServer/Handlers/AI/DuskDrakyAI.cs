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
/// Java parity: ai/instance/empyreanCrucible/DuskDrakyAI.
/// Lightning drakie of the stage 9 bonus: circles its route, ignoring attackers, until the instance removes it.
/// </summary>
[AIName("empyrean_dusk_draky")]
public class DuskDrakyAI : GeneralNpcAI
{
    public DuskDrakyAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        GetSpawnTemplate().SetWalkerId("300300002");
        WalkManager.StartWalking(this);
        GetOwner().SetState(CreatureState.ACTIVE, true);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
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
