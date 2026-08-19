using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz
/// </summary>
[AIName("greenfingers")]
public class GreenfingersAI : AggressiveNpcAI
{
    private readonly AtomicBoolean isDestroyed = new AtomicBoolean();
    private int walkPosition;
    private int helperSkill;

    public GreenfingersAI(Npc owner) : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        switch (GetNpcId())
        {
            case 282176:
                walkPosition = 24;
                helperSkill = 19271;
                break;
            case 282177:
                walkPosition = 26;
                helperSkill = 18751;
                break;
            case 282178:
                walkPosition = 40;
                helperSkill = 16634;
                break;
        }
    }

    /// <remarks>
    /// <b>The step index has to be read before <c>base</c>.</b> The base handler runs
    /// <c>WalkManager.ChooseNextRouteStep</c>, which advances the move controller, so after it the index is
    /// the point being left for and not the one reached. These three routes carry no <c>loop_type</c>, so
    /// they default to <c>NORMAL</c> and wrap: arriving at the genuine last step read back as index zero
    /// and never matched, while arriving at the second-to-last read back as the last and did. The helper
    /// therefore cast its skill and despawned <b>one waypoint early</b>, every run.
    /// <para>
    /// The three <c>walkPosition</c> values are 24, 26 and 40 against routes of 25, 27 and 41 steps -- each
    /// one the final index, so "the end of the route" is unambiguously what was meant.
    /// <c>ReianBomberAI</c> reads it in the right order already.
    /// </para>
    /// </remarks>
    protected override void HandleMoveArrived()
    {
        int point = GetOwner().GetMoveController().GetCurrentStep().GetStepIndex();
        base.HandleMoveArrived();
        if (walkPosition == point)
        {
            if (isDestroyed.CompareAndSet(false, true))
            {
                GetSpawnTemplate().SetWalkerId(null);
                WalkManager.StopWalking(this);
                Npc boss = GetPosition().GetWorldMapInstance().GetNpc(217185);
                if (boss != null)
                {
                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), helperSkill, 55, boss).UseNoAnimationSkill();
                }
                StartDespawnTask();
            }
        }
    }

    private void StartDespawnTask()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
                AIActions.DeleteOwner(this);
            return ValueTask.CompletedTask;
        }, 3000L);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => true,
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
