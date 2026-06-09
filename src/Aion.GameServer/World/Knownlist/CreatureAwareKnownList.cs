using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.World.Knownlist;

/// <summary>Java parity: world/knownlist/CreatureAwareKnownList.</summary>
public class CreatureAwareKnownList : KnownList
{
    public CreatureAwareKnownList(VisibleObject owner)
        : base(owner)
    {
    }

    protected sealed override bool IsAwareOf(VisibleObject newObject)
    {
        return base.IsAwareOf(newObject) && newObject is Creature;
    }
}
