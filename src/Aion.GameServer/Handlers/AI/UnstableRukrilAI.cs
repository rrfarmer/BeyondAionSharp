using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/unstableSplinterpath/UnstableRukrilAI (Ritsu, Luzien, Cheatkiler).</summary>
[AIName("unstablerukril")]
public class UnstableRukrilAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(95);
    private ScheduledTask skillTask;

    public UnstableRukrilAI(Npc owner)
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
        Npc ebonsoul = GetPosition().GetWorldMapInstance().GetNpc(219552);
        skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
            {
                CancelTask();
            }
            else
            {
                if (GetPosition().GetWorldMapInstance().GetNpc(283204) == null)
                {
                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19266, 55, GetOwner()).UseNoAnimationSkill();
                    Spawn(283204, GetOwner().GetX() + 2, GetOwner().GetY() - 2, GetOwner().GetZ(), (sbyte)0);
                }

                if (ebonsoul != null && !ebonsoul.IsDead())
                {
                    SkillEngine.SkillEngine.GetInstance().GetSkill(ebonsoul, 19159, 55, ebonsoul).UseNoAnimationSkill();
                    Spawn(283205, ebonsoul.GetX() + 2, ebonsoul.GetY() - 2, ebonsoul.GetZ(), (sbyte)0);
                }
            }

            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(5000), System.TimeSpan.FromMilliseconds(70000));
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
        Npc ebonsoul = GetPosition().GetWorldMapInstance().GetNpc(219552);
        if (ebonsoul != null && !ebonsoul.IsDead() && PositionUtil.IsInRange(GetOwner(), ebonsoul, 5))
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
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP => false,
            _ => base.Ask(question),
        };
    }
}
