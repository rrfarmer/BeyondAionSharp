using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/unstableSplinterpath/UnstableEbonsoulAI (Ritsu, Luzien, Cheatkiller).</summary>
[AIName("unstableebonsoul")]
public class UnstableEbonsoulAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(95);
    private ScheduledTask skillTask;

    public UnstableEbonsoulAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
        TryRegen();
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        StartSkillTask();
    }

    private void StartSkillTask()
    {
        Npc rukril = GetPosition().GetWorldMapInstance().GetNpc(219551);
        skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
                CancelTask();
            else
            {
                if (GetPosition().GetWorldMapInstance().GetNpc(283205) == null)
                {
                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19159, 55, GetOwner()).UseNoAnimationSkill();
                    Spawn(283205, GetOwner().GetX() + 2, GetOwner().GetY() - 2, GetOwner().GetZ(), (sbyte)0);
                }
                if (rukril != null && !rukril.IsDead())
                {
                    SkillEngine.SkillEngine.GetInstance().GetSkill(rukril, 19266, 55, rukril).UseNoAnimationSkill();
                    Spawn(283204, rukril.GetX() + 2, rukril.GetY() - 2, rukril.GetZ(), (sbyte)0);
                }
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(5000), TimeSpan.FromMilliseconds(70000)); // re-check delay
    }

    private void CancelTask()
    {
        if (skillTask != null && !skillTask.IsCancelled)
        {
            skillTask.Cancel(true);
        }
    }

    private void TryRegen()
    {
        Npc rukril = GetPosition().GetWorldMapInstance().GetNpc(219551);
        if (rukril != null && !rukril.IsDead() && PositionUtil.IsInRange(GetOwner(), rukril, 5))
            if (!GetOwner().GetLifeStats().IsFullyRestoredHp())
                GetOwner().GetLifeStats().IncreaseHp(TYPE.HP, 10000);
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        CancelTask();
        hpPhases.Reset();
        GetEffectController().RemoveEffect(19266);
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
