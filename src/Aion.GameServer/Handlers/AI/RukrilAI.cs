using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/abyssal_splinter/RukrilAI (Ritsu, Luzien).</summary>
[AIName("rukril")]
public class RukrilAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(95);
    private ScheduledTask skillTask;

    public RukrilAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        StartSkillTask();
    }

    private void StartSkillTask()
    {
        skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
            {
                CancelTask();
            }
            else
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19266, 55, GetOwner()).UseNoAnimationSkill();
                if (GetPosition().GetWorldMapInstance().GetNpc(281907) == null)
                {
                    Spawn(281907, 447.3828f, 675.9968f, 433.95636f, (sbyte)19);
                    Spawn(281907, 441.49512f, 680.38495f, 434.02753f, (sbyte)19);
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
