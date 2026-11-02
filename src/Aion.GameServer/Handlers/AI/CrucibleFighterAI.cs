using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/CrucibleFighterAI.
/// Arena fighter whose fight the instance also starts on the first hit, not only when the fighter notices a player itself: much stronger
/// players are never aggroed, and a player may strike first from outside the aggro range.
/// </summary>
[AIName("empyrean_fighter")]
public class CrucibleFighterAI : AggressiveNpcAI
{
    private readonly AtomicBoolean engaged = new AtomicBoolean();

    public CrucibleFighterAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (engaged.CompareAndSet(false, true))
            GetPosition().GetWorldMapInstance().GetInstanceHandler().OnAggro(GetOwner());
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        engaged.Set(false); // a new fight counts its time again
    }
}
