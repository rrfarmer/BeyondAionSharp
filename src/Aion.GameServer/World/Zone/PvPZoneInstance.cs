using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Zone;

namespace Aion.GameServer.World.Zone;

/// <summary>Java parity: world/zone/PvPZoneInstance (MrPoke) : ZoneInstance. synchronized onEnter/onLeave→lock(this); super→base. ZoneType.PVP.</summary>
public class PvPZoneInstance : ZoneInstance
{
    public PvPZoneInstance(int mapId, ZoneInfo template) : base(mapId, template)
    {
    }

    public override bool OnEnter(Creature creature)
    {
        lock (this)
        {
            if (base.OnEnter(creature))
            {
                creature.SetInsideZoneType(ZoneType.PVP);
                return true;
            }
            return false;
        }
    }

    public override bool OnLeave(Creature creature)
    {
        lock (this)
        {
            if (base.OnLeave(creature))
            {
                creature.UnsetInsideZoneType(ZoneType.PVP);
                return true;
            }
            return false;
        }
    }
}
