using System;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.World.Knownlist;

/// <summary>Java parity: world/knownlist/FlagKnownList. Java pre-super() validation → static Validate in base(...).</summary>
public class FlagKnownList : PlayerAwareKnownList
{
    public FlagKnownList(Npc owner)
        : base(Validate(owner))
    {
    }

    private static Npc Validate(Npc owner)
    {
        if (!owner.IsFlag())
            throw new ArgumentException();
        return owner;
    }

    public override void Update()
    {
        lock (this)
        {
            WorldMapInstance worldMapInstance = Owner.GetPosition().GetWorldMapInstance();
            foreach (var entry in KnownObjects.ToArray())
            {
                if (entry.Value.Get().GetWorldMapInstance() != worldMapInstance)
                    KnownObjects.TryRemove(entry.Key, out _);
            }
            worldMapInstance.ForEachPlayer(player =>
            {
                if (player.GetKnownList().Add(Owner))
                    Add(player);
            });
        }
    }

    protected override float GetVisibleDistance()
    {
        return float.MaxValue;
    }
}
