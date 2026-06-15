using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/unstableSplinterpath/PieceOfSplendorAI (@author Cheatkiller).</summary>
[AIName("pieceofsplendor")]
public class PieceOfSplendorAI : AggressiveNpcAI
{
    public PieceOfSplendorAI(Npc owner)
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
        if (creature is Npc)
        {
            Npc ebonsoul = GetPosition().GetWorldMapInstance().GetNpc(219552);
            if (ebonsoul != null && PositionUtil.IsInRange(GetOwner(), ebonsoul, 5) && ebonsoul.GetEffectController().HasAbnormalEffect(19159))
            {
                ebonsoul.GetEffectController().RemoveEffect(19159);
                Npc rukril = GetPosition().GetWorldMapInstance().GetNpc(219551);
                if (rukril != null && rukril.GetEffectController().HasAbnormalEffect(19266))
                    rukril.GetEffectController().RemoveEffect(19266);
            }
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP => false,
            _ => base.Ask(question),
        };
    }
}
