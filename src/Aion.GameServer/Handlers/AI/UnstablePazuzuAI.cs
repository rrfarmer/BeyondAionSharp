using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/unstableSplinterpath/UnstablePazuzuAI (@author Luzien, Cheatkiller).</summary>
[AIName("unstablepazuzu")]
public class UnstablePazuzuAI : AggressiveNpcAI
{
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private ScheduledTask? task;

    public UnstablePazuzuAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 342219);
            StartTask();
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        CancelTask();
        isHome.Set(true);
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500003);
    }

    private void StartTask()
    {
        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19145, 55, GetOwner()).UseNoAnimationSkill();
            if (GetPosition().GetWorldMapInstance().GetNpc(283206) == null)
            {
                Spawn(283206, 651.351990f, 326.425995f, 465.523987f, (sbyte)8);
                Spawn(283206, 666.604980f, 314.497009f, 465.394012f, (sbyte)27);
                Spawn(283206, 685.588989f, 342.955994f, 465.908997f, (sbyte)68);
                Spawn(283206, 651.322021f, 346.554993f, 465.563995f, (sbyte)111);
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(5000), System.TimeSpan.FromMilliseconds(70000));
    }

    private void CancelTask()
    {
        if (task != null && !task.IsCancelled)
        {
            task.Cancel(true);
        }
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        base.HandleDespawned();
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.REWARD_LOOT:
            case AIQuestion.REWARD_AP:
                return false;
            default:
                return base.Ask(question);
        }
    }
}
