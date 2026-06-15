using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Cheatkiller
/// </summary>
[AIName("tahabataaltar")]
public class TahabataAltarAI : NpcAI
{
    public TahabataAltarAI(Npc owner) : base(owner)
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
        int owner = GetNpcId();
        int debuff = owner switch
        {
            283116 => 20970,
            283118 => 20971,
            _ => 0,
        };
        if (creature is Player)
        {
            if (GetNpcId() == 283253 && PositionUtil.IsInRangeLimited(GetOwner(), creature, 25, 37)
                || GetNpcId() == 283255 && PositionUtil.IsInRangeLimited(GetOwner(), creature, 20, 25))
            {
                if (!creature.GetEffectController().HasAbnormalEffect(debuff))
                {
                    AIActions.UseSkill(this, debuff);
                }
            }
        }
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
