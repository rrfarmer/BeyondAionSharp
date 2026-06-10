using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Zone;

namespace Aion.GameServer.World.Zone;

/// <summary>Java parity: world/zone/NoFlyZoneInstance : ZoneInstance. synchronized onEnter/onLeave→lock(this); super→base; instanceof-pattern→is-pattern. ZoneType.NO_FLY/FLY fly-area transitions.</summary>
public class NoFlyZoneInstance : ZoneInstance
{
    public NoFlyZoneInstance(int mapId, ZoneInfo template) : base(mapId, template)
    {
    }

    public override bool OnEnter(Creature creature)
    {
        lock (this)
        {
            if (!base.OnEnter(creature))
                return false;
            bool wasInNoFlyZone = creature.IsInsideZoneType(ZoneType.NO_FLY);
            creature.SetInsideZoneType(ZoneType.NO_FLY);
            if (!wasInNoFlyZone && creature.IsInsideZoneType(ZoneType.FLY) && creature is Player player)
                player.GetController().OnLeaveFlyArea();
            return true;
        }
    }

    public override bool OnLeave(Creature creature)
    {
        lock (this)
        {
            if (!base.OnLeave(creature))
                return false;
            creature.UnsetInsideZoneType(ZoneType.NO_FLY);
            if (!creature.IsInsideZoneType(ZoneType.NO_FLY) && creature.IsInsideZoneType(ZoneType.FLY) && creature is Player player)
                player.GetController().OnEnterFlyArea();
            return true;
        }
    }
}
