using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.World.Knownlist;

/// <summary>Java parity: world/knownlist/PlayerAwareKnownList (ATracer).</summary>
public class PlayerAwareKnownList : KnownList
{
    public PlayerAwareKnownList(VisibleObject owner)
        : base(owner)
    {
    }

    protected sealed override bool IsAwareOf(VisibleObject newObject)
    {
        return base.IsAwareOf(newObject) && newObject is Aion.GameServer.Model.GameObjects.Player.Player;
    }
}
