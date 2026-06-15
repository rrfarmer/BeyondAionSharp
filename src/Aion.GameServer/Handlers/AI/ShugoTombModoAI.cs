using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Ritsu
/// </summary>
[AIName("shugo_tomb_modo")]
public class ShugoTombModoAI : ShugoTombAttackerAI
{
    public ShugoTombModoAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        HandleHate();
    }
}
