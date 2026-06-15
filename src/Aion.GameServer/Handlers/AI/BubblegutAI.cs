using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/BubblegutAI (Luzien).</summary>
[AIName("bubblegut")]
public class BubblegutAI : GeneralNpcAI
{
    public BubblegutAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        AIActions.UseSkill(this, 16447);
    }
}
