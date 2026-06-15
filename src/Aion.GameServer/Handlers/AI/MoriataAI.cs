using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/sauroBase/MoriataAI (Estrayl).</summary>
[AIName("moriata")]
public class MoriataAI : AggressiveNpcAI
{
    private readonly AtomicBoolean hasAggro = new AtomicBoolean();
    private ScheduledTask task;
    private long lastSkillUse;
    private int kiteCount;

    public MoriataAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (hasAggro.CompareAndSet(false, true))
            task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { ObserveKiting(); return ValueTask.CompletedTask; }, System.TimeSpan.FromMilliseconds(12000), System.TimeSpan.FromMilliseconds(12000));
    }

    private void ObserveKiting()
    {
        if ((System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastSkillUse) / 1000 >= 10)
        {
            SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(20181, GetOwner(), GetOwner()); // Temporary Speed Buff
            SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21417, GetOwner(), GetOwner()); // Raging Fury
            if (++kiteCount % 3 == 0)
                SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(22661, GetOwner(), GetOwner()); // Permanent Speed Buff
        }
    }

    protected override void HandleAttackComplete()
    {
        lastSkillUse = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        base.HandleAttackComplete();
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        lastSkillUse = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private void CancelTask()
    {
        if (task != null && !task.IsCancelled)
            task.Cancel(true);
    }

    protected override void HandleBackHome()
    {
        CancelTask();
        hasAggro.Set(false);
        base.HandleBackHome();
    }

    protected override void HandleDied()
    {
        CancelTask();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        base.HandleDespawned();
    }
}
