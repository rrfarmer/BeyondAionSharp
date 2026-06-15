using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/abyssal_splinter/GatesSummonedAI (Ritsu).</summary>
[AIName("gatessummoned")]
public class GatesSummonedAI : GeneralNpcAI
{
    private ScheduledTask eventTask;
    private bool canThink = true;

    public GatesSummonedAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        StartMove();
    }

    public override bool CanThink()
    {
        return canThink;
    }

    protected override void HandleDied()
    {
        CancelEventTask();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelEventTask();
        base.HandleDespawned();
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        StartEventTask();
    }

    private void StartMove()
    {
        canThink = false;
        EmoteManager.EmoteStopAttacking(GetOwner());
        SetStateIfNot(AIState.FOLLOWING);
        GetOwner().SetState(CreatureState.ACTIVE, true);
        AIActions.TargetCreature(this, GetPosition().GetWorldMapInstance().GetNpc(216960));
        GetMoveController().MoveToTargetObject();
    }

    private void CancelEventTask()
    {
        if (eventTask != null && !eventTask.IsDone())
            eventTask.Cancel(true);
    }

    private void StartEventTask()
    {
        eventTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            Npc boss = GetPosition().GetWorldMapInstance().GetNpc(216960);
            if (boss == null || boss.IsDead())
            {
                CancelEventTask();
                return ValueTask.CompletedTask;
            }

            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), Rnd.NextBoolean() ? 19257 : 19281, 55, boss).UseNoAnimationSkill();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(5000), System.TimeSpan.FromMilliseconds(30000));
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
