using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Ai;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/BombAI (@author xTz, Sykra).</summary>
[AIName("bomb")]
public class BombAI : AggressiveNpcAI
{
    private BombTemplate template;

    private readonly List<ScheduledTask> tasks = new();

    public BombAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        template = DataManager.AI_DATA.GetAiTemplate(GetNpcId()).GetBombs().GetBombTemplate();
        AddTask(ThreadPoolManager.GetInstance().Schedule(_ => { UseSkill(template.GetSkillId()); return ValueTask.CompletedTask; }, template.GetCd() + 2000L));
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTasks();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTasks();
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_DECAY:
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
            case AIQuestion.REWARD_LOOT:
                return false;
            default:
                return base.Ask(question);
        }
    }

    private void AddTask(ScheduledTask task)
    {
        if (task == null)
            return;
        lock (tasks)
        {
            tasks.Add(task);
        }
    }

    private void CancelTasks()
    {
        lock (tasks)
        {
            foreach (ScheduledTask task in tasks)
                if (task != null && !task.IsDone())
                    task.Cancel(true);
            tasks.Clear();
        }
    }

    private void UseSkill(int skill)
    {
        AIActions.TargetSelf(this);
        AIActions.UseSkill(this, skill);
        int duration = DataManager.SKILL_DATA.GetSkillTemplate(skill).GetDuration();
        AddTask(ThreadPoolManager.GetInstance().Schedule(_ => { AIActions.DeleteOwner(this); return ValueTask.CompletedTask; }, duration != 0 ? duration + 4000L : 0L));
    }
}
