using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/classNpc/EnemyServantAI (@author Cheatkiller).</summary>
[AIName("enemyservant")]
public class EnemyServantAI : NpcAI
{
    public EnemyServantAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (GetCreator() == null || GetCreator().GetTarget() == null)
            {
                return;
            }
            AIActions.TargetCreature(this, (Creature)GetCreator().GetTarget());
            Attack();
        }, 2000L);
    }

    private void Attack()
    {
        ScheduledTask task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ => { GetOwner().GetController().UseSkill(16907, 55); return System.Threading.Tasks.ValueTask.CompletedTask; }, System.TimeSpan.FromMilliseconds(1000), System.TimeSpan.FromMilliseconds(6000));
        GetOwner().GetController().AddTask(TaskId.SKILL_USE, task);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
