using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Cheatkiller
/// </summary>
[AIName("chantrarings")]
public class ChantraRingsAI : NpcAI
{
    public ChantraRingsAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        CheckDistance(this, creature);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        CheckDistance(this, creature);
    }

    private void CheckDistance(NpcAI ai, Creature creature)
    {
        int debuff = GetOwner().GetNpcId() == 283172 ? 20735 : 20734; // 4.0
        if (creature is Player)
        {
            if (GetOwner().GetNpcId() == 283172 && PositionUtil.IsInRangeLimited(GetOwner(), creature, 10, 18) // 4.0
                || GetOwner().GetNpcId() == 283171 && PositionUtil.IsInRangeLimited(GetOwner(), creature, 18, 25) // 4.0
                || GetOwner().GetNpcId() == 283171 && PositionUtil.IsInRangeLimited(GetOwner(), creature, 0, 10)) // 4.0
            {
                if (!creature.GetEffectController().HasAbnormalEffect(debuff))
                    AIActions.UseSkill(this, debuff);
            }
        }
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Despawn();
    }

    private void Despawn()
    {
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            GetOwner().GetController().Delete();
            return ValueTask.CompletedTask;
        }, 20000L);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
