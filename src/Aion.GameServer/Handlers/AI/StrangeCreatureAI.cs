using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/StrangeCreatureAI (@author Luzien).</summary>
[AIName("strange_creature")]
public class StrangeCreatureAI : GeneralNpcAI
{
    public StrangeCreatureAI(Npc owner)
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
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 341444);
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 17914, 34, GetOwner()).UseNoAnimationSkill();
            }

            return ValueTask.CompletedTask;
        }, 500L);
    }

    private void StartLifeTask()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.DeleteOwner(this);
            return ValueTask.CompletedTask;
        }, 6500L);
    }
}
