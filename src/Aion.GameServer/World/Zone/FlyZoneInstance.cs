using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Zone;

namespace Aion.GameServer.World.Zone;

/// <summary>Java parity: world/zone/FlyZoneInstance (MrPoke) : ZoneInstance. synchronized onEnter/onLeave→lock(this); super→base; instanceof→cast/is. ZoneType.FLY.</summary>
public class FlyZoneInstance : ZoneInstance
{
    public FlyZoneInstance(int mapId, ZoneInfo template) : base(mapId, template)
    {
    }

    public override bool OnEnter(Creature creature)
    {
        lock (this)
        {
            if (base.OnEnter(creature))
            {
                creature.SetInsideZoneType(ZoneType.FLY);
                if (creature is Player)
                {
                    ((Player)creature).GetController().OnEnterFlyArea();
                }
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public override bool OnLeave(Creature creature)
    {
        lock (this)
        {
            if (base.OnLeave(creature))
            {
                creature.UnsetInsideZoneType(ZoneType.FLY);
                if (!creature.IsInsideZoneType(ZoneType.FLY) && creature is Player)
                    ((Player)creature).GetController().OnLeaveFlyArea();
                return true;
            }
            else
                return false;
        }
    }
}
