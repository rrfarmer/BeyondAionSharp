using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/DistortedSpaceAI (@author Cheatkiller).</summary>
[AIName("distortedspace")]
public class DistortedSpaceAI : NpcAI
{
    private ScheduledTask task;

    public DistortedSpaceAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        UseSkill();
    }

    /// <summary>
    /// Retail <c>IDTiamat_Sardha</c> gives this ten seconds; Java closed it at eight.
    /// </summary>
    /// <remarks>
    /// <b>The bound was never missing, only short.</b> An audit pass added a ten-second lifetime to
    /// Terath's spawn call to fix an add that "never expired" — but this add has always killed itself,
    /// two seconds early, and the summoner-side lifetime was dead code because the shorter clock always
    /// won. Corrected here, where the clock actually is.
    /// </remarks>
    private const long BlackHoleLifeMillis = 10000L;

    private void UseSkill()
    {
        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (GetOwner().GetNpcId() == 283097)
                AIActions.UseSkill(this, 20740);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(2000));

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            CancelTask();
            if (GetOwner().GetNpcId() == 283097)
                AIActions.UseSkill(this, 20742);
            GetOwner().GetController().Die();
            return ValueTask.CompletedTask;
        }, BlackHoleLifeMillis);
    }

    private void CancelTask()
    {
        if (task != null && !task.IsCancelled)
        {
            task.Cancel(true);
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
        AIActions.DeleteOwner(this);
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask();
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_DECAY:
            case AIQuestion.ALLOW_RESPAWN:
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
                return false;
            default:
                return base.Ask(question);
        }
    }
}
