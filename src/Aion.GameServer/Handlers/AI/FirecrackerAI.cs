using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/FirecrackerAI (Rolandas).</summary>
[AIName("firecracker")]
public class FirecrackerAI : GeneralNpcAI
{
    public FirecrackerAI(Npc owner)
        : base(owner)
    {
    }
}
