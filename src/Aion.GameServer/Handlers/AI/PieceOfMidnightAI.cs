using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/unstableSplinterpath/PieceOfMidnightAI (@author Cheatkiller).</summary>
[AIName("pieceofmidnight")]
public class PieceOfMidnightAI : AggressiveNpcAI
{
    public PieceOfMidnightAI(Npc owner)
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
            Npc rukril = GetPosition().GetWorldMapInstance().GetNpc(219551);
            if (rukril != null && PositionUtil.IsInRange(GetOwner(), rukril, 5) && rukril.GetEffectController().HasAbnormalEffect(19266))
            {
                rukril.GetEffectController().RemoveEffect(19266);
                Npc ebonsoul = GetPosition().GetWorldMapInstance().GetNpc(219552);
                if (ebonsoul != null && ebonsoul.GetEffectController().HasAbnormalEffect(19159))
                    ebonsoul.GetEffectController().RemoveEffect(19159);
            }
        }
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.REWARD_LOOT:
            case AIQuestion.REWARD_AP:
                return false;
            default:
                return base.Ask(question);
        }
    }
}
