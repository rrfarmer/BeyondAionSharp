using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/WaveEventExecutorAI (Estrayl).</summary>
[AIName("wave_event_executor")]
public class WaveEventExecutorAI : GeneralNpcAI
{
    private AtomicBoolean isDestinationReached = new AtomicBoolean();

    public WaveEventExecutorAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (GetOwner().GetMoveController().IsStop() && isDestinationReached.CompareAndSet(false, true))
        {
            GetOwner().GetSpawn().SetWalkerId("");
            PacketSendUtility.BroadcastMessage(GetOwner(), 1501318);
            ScheduleSummon();
        }
    }

    private void ScheduleSummon()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20839, 1, GetOwner()).UseSkill();
            PacketSendUtility.BroadcastMessage(GetOwner(), 1501317, 2000);
            return ValueTask.CompletedTask;
        }, 3500L);
    }
}
