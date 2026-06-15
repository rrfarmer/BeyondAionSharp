using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rentusBase/DrakanBarricadeAI (Sykra).</summary>
[AIName("drakanbarricade")]
public class DrakanBarricadeAI : NoActionAI
{
    public DrakanBarricadeAI(Npc owner)
        : base(owner)
    {
    }

    public override bool Ask(AIQuestion question)
    {
        if (question == AIQuestion.REWARD_LOOT)
            return false;
        return base.Ask(question);
    }
}
