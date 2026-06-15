using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/theShugoEmperorsVault/IDSweep_Bosses (Yeats).</summary>
[AIName("IDSweep_Boss")]
public class IDSweep_Bosses : IDSweep_Shugos
{
    public IDSweep_Bosses(Npc owner)
        : base(owner)
    {
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
