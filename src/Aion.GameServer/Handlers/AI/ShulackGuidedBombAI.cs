using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/aturamSkyFortress/ShulackGuidedBombAI (xTz).</summary>
[AIName("shulack_guided_bomb")]
public class ShulackGuidedBombAI : AggressiveNpcAI
{
    private bool isDestroyed;
    private bool isHome = true;
    private ScheduledTask task;

    public ShulackGuidedBombAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        if (task != null)
            task.Cancel(true);
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        StarLifeTask();
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
        base.HandleCreatureAggro(creature);
        if (isHome)
        {
            isHome = false;
            DoSchedule(creature);
        }
    }

    private void StarLifeTask()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead() && !isDestroyed)
                Despawn();
            return ValueTask.CompletedTask;
        }, 10000L);
    }

    private void DoSchedule(Creature creature)
    {
        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (!IsDead() && !isDestroyed)
            {
                Destroy(creature);
            }
            else
            {
                if (task != null)
                {
                    task.Cancel(true);
                }
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(1000), System.TimeSpan.FromMilliseconds(1000));
    }

    private void Despawn()
    {
        if (!IsDead())
        {
            AIActions.DeleteOwner(this);
        }
    }

    private void Destroy(Creature creature)
    {
        if (!isDestroyed && !IsDead())
        {
            if (creature != null && PositionUtil.GetDistance(GetOwner(), creature) <= 4)
            {
                isDestroyed = true;
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19415, 49, GetOwner()).UseNoAnimationSkill();
                ThreadPoolManager.GetInstance().Schedule(_ => { Despawn(); return ValueTask.CompletedTask; }, 3200L);
            }
        }
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_DECAY:
            case AIQuestion.ALLOW_RESPAWN:
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
                return false;
            case AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES:
                return true;
            default:
                return base.Ask(question);
        }
    }
}
