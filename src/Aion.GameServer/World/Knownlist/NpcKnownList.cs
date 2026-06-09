using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.World.Knownlist;

/// <summary>Java parity: world/knownlist/NpcKnownList.</summary>
public class NpcKnownList : CreatureAwareKnownList
{
    public NpcKnownList(VisibleObject owner)
        : base(owner)
    {
    }

    public override void Update()
    {
        if (Owner.GetPosition().IsMapRegionActive())
            base.Update();
        else
            Clear(ObjectDeleteAnimation.FADE_OUT);
    }
}
