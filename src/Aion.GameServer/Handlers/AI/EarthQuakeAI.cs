using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/EarthQuakeAI (@author Cheatkiller).</summary>
[AIName("earthquake")]
public class EarthQuakeAI : NpcAI
{
    public EarthQuakeAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        CheckDistance(creature);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        CheckDistance(creature);
    }

    private void CheckDistance(Creature creature)
    {
        if (creature is Player)
        {
            if (PositionUtil.IsInRange(GetOwner(), creature, 5) && !creature.GetEffectController().HasAbnormalEffect(20718))
            {
                AIActions.UseSkill(this, 20718);
            }
        }
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Despawn();
    }

    /// <summary>
    /// Retail's <c>live_time</c> on <c>EarthQuakeDMG</c> is five seconds; this class used nine.
    /// </summary>
    /// <remarks>
    /// It mattered more than a lifetime usually does. The FX drops one of these every two seconds, so at
    /// nine seconds four of them overlap and the patch of ground is continuous; at five, three do. The
    /// number only became visible once the FX existed to drop them in a train.
    /// </remarks>
    private const long DamageLifeMillis = 5000L;

    private void Despawn()
    {
        ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetController().Delete(); return ValueTask.CompletedTask; }, DamageLifeMillis);
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
