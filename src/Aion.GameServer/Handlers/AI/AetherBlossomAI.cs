using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/events/AetherBlossomAI (Estrayl). Part of 'Blessing Of The Magic Fountain' event.</summary>
[AIName("aether_blossom")]
public class AetherBlossomAI : ActionItemNpcAI
{
    private readonly AtomicBoolean hasRewarded = new AtomicBoolean();

    public AetherBlossomAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (hasRewarded.CompareAndSet(false, true)) // just in case
            ItemService.AddItem(player, 186000406, 1); // [Event] Aether Blossom
        base.HandleUseItemFinish(player);
        AIActions.Die(this);
    }
}
