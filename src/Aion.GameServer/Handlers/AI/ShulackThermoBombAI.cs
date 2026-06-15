using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/aturamSkyFortress/ShulackThermoBombAI (@author xTz).</summary>
[AIName("shulack_thermo_bomb")]
public class ShulackThermoBombAI : AggressiveNpcAI
{
    public ShulackThermoBombAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        DoSchedule();
    }

    private void DoSchedule()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19416, 49, GetOwner()).UseNoAnimationSkill();
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    if (!IsDead())
                        Despawn();
                    return ValueTask.CompletedTask;
                }, 4000L);
            }

            return ValueTask.CompletedTask;
        }, 2000L);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => true,
            _ => base.Ask(question),
        };
    }

    private void Despawn()
    {
        AIActions.DeleteOwner(this);
    }
}
