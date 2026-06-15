using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/aturamSkyFortress/DredgionGeneratorAI (@author Tibald :)).</summary>
[AIName("dredgion_generator")]
public class DredgionGeneratorAI : GeneralNpcAI
{
    public DredgionGeneratorAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => true,
            _ => base.Ask(question),
        };
    }
}
