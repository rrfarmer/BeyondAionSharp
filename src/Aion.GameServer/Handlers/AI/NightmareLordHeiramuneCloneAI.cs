using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/nightmareCircus/NightmareLordHeiramuneCloneAI (Ritsu).</summary>
[AIName("nightmarelordheiramuneclone")]
public class NightmareLordHeiramuneCloneAI : AggressiveNpcAI
{
    public NightmareLordHeiramuneCloneAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Heal();
    }

    private void Heal()
    {
        ScheduledTask task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            Npc boss = GetPosition().GetWorldMapInstance().GetNpc(233467);
            if (boss != null && !boss.IsDead())
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21342, 1, boss).UseSkill();
            }
            else
                AIActions.DeleteOwner(this);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(5000), TimeSpan.FromMilliseconds(10000));
        GetOwner().GetController().AddTask(TaskId.SKILL_USE, task);
    }
}
