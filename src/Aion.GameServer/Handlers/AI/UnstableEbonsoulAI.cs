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

    /// <summary>
    /// Retail <c>bidabre_core_02</c>: both summons carry <c>live_time</c> 70 against a branch timer of
    /// the same seventy seconds.
    /// </summary>
    /// <remarks>
    /// <b>The partner's summon below had no guard at all</b>, so where this class's own summon ran once
    /// per fight and stopped, the partner's accumulated a fresh pair every seventy seconds for the whole
    /// fight. One missing lifetime produced opposite failures on two adjacent lines.
    /// </remarks>
    private const int SummonLife = 70;

    private void StartSkillTask()
    {
        Npc rukril = GetPosition().GetWorldMapInstance().GetNpc(219551);
        skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
                CancelTask();
            else
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19159, 55, GetOwner()).UseNoAnimationSkill();
                SpawnFor(283205, GetOwner().GetX() + 2, GetOwner().GetY() - 2, GetOwner().GetZ(), (sbyte)0, SummonLife);
                if (rukril != null && !rukril.IsDead())
                {
                    SkillEngine.SkillEngine.GetInstance().GetSkill(rukril, 19266, 55, rukril).UseNoAnimationSkill();
                    SpawnFor(283204, rukril.GetX() + 2, rukril.GetY() - 2, rukril.GetZ(), (sbyte)0, SummonLife);
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
