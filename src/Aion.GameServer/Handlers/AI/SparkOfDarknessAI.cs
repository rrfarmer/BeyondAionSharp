using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/SparkOfDarknessAI (@author Luzien).</summary>
[AIName("spark_of_darkness")]
public class SparkOfDarknessAI : GeneralNpcAI
{
    public SparkOfDarknessAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        StartEventTask();
        StartLifeTask();
    }

    private void StartEventTask()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19554, 1, GetOwner()).UseNoAnimationSkill();
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 500L);
    }

    private void StartLifeTask()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.DeleteOwner(this);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 6500L);
    }
}
