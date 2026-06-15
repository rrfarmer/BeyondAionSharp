using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/SuramaTheTraitorAI (Cheatkiller).</summary>
[AIName("suramathetraitor")]
public class SuramaTheTraitorAI : GeneralNpcAI
{
    public SuramaTheTraitorAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        MoveToRaksha();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        PacketSendUtility.BroadcastMessage(GetOwner(), 390845, 2000);
    }

    private void MoveToRaksha()
    {
        SetStateIfNot(AIState.WALKING);
        GetOwner().SetState(CreatureState.ACTIVE, true);
        GetMoveController().MoveToPoint(651, 1319, 487);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetOwner().GetObjectId()));
        ThreadPoolManager.GetInstance().Schedule(_ => { StartDialog(); return ValueTask.CompletedTask; }, 10000L);
    }

    private void StartDialog()
    {
        Npc raksha = GetPosition().GetWorldMapInstance().GetNpc(219356);
        PacketSendUtility.BroadcastMessage(GetOwner(), 390841);
        PacketSendUtility.BroadcastMessage(GetOwner(), 390842, 3000);
        PacketSendUtility.BroadcastMessage(raksha, 390843, 6000);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            raksha.SetTarget(GetOwner());
            SkillEngine.SkillEngine.GetInstance().GetSkill(raksha, 20952, 60, GetOwner()).UseNoAnimationSkill();
            raksha.OverrideNpcType(CreatureType.ATTACKABLE);
            return ValueTask.CompletedTask;
        }, 8000L);
    }
}
