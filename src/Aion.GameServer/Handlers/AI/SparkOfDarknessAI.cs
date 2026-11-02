using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/SparkOfDarknessAI (@author Luzien, w4terbomb).</summary>
[AIName("spark_of_darkness")]
public class SparkOfDarknessAI : GeneralNpcAI
{
    /// <summary>Retail <c>S8_Summon_Fire_55_Ae</c> gives this five seconds; Java used six and a half.</summary>
    private const long SparkLifeMillis = 5000L;

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
                GetOwner().QueueSkill(19554, 1);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 500L);
    }

    private void StartLifeTask()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.DeleteOwner(this);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, SparkLifeMillis);
    }

    public override bool IsMoveSupported()
    {
        return false;
    }
}
