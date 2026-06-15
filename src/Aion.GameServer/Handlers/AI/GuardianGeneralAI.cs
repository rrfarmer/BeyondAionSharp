using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/GuardianGeneralAI (@author Estrayl).</summary>
[AIName("guardian_general")]
public class GuardianGeneralAI : SiegeNpcAI
{
    public GuardianGeneralAI(Npc owner)
        : base(owner)
    {
    }
    // TODO: Working AI for BA
}
