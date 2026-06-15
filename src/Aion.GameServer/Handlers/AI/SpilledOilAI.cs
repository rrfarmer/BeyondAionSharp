using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rentusBase/SpilledOilAI (@author xTz).</summary>
[AIName("spilled_oil")]
public class SpilledOilAI : GeneralNpcAI
{
    private int count;

    public SpilledOilAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        StartEventTask();
    }

    private void StartEventTask()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                count++;
                if (count < 7)
                {
                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19658, 60, GetOwner()).UseNoAnimationSkill();
                    StartEventTask();
                }
                else
                {
                    Delete();
                }
            }

            return ValueTask.CompletedTask;
        }, 4000L);
    }

    private void Delete()
    {
        AIActions.DeleteOwner(this);
    }
}
