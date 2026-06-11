using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Utils.Collections;
using Aion.GameServer.World.Zone.Handler;

namespace Aion.GameServer.World.Zone;

/// <summary>
/// A live zone within a map region; tracks creatures inside and dispatches zone handlers.
/// Java parity: world/zone/ZoneInstance.
/// </summary>
public class ZoneInstance
{
    private readonly ZoneInfo _template;
    private readonly int _mapId;
    protected readonly Dictionary<int, Creature> Creatures = new();
    protected List<IZoneHandler> Handlers = new();

    public ZoneInstance(int mapId, ZoneInfo template)
    {
        _template = template;
        _mapId = mapId;
    }

    // Java parity: getAreaTemplate()
    public Area GetAreaTemplate() => _template.GetArea();

    // Java parity: getZoneTemplate()
    public ZoneTemplate GetZoneTemplate() => _template.GetZoneTemplate();

    // Java parity: revalidate(Creature)
    public bool Revalidate(Creature creature) =>
        _mapId == creature.GetWorldId() && _template.GetArea().IsInside3D(creature.GetX(), creature.GetY(), creature.GetZ());

    // Java parity: synchronized onEnter(Creature) — non-final in Java (Fly/NoFly/PvP ZoneInstance override), so virtual.
    public virtual bool OnEnter(Creature creature)
    {
        lock (this)
        {
            if (Creatures.ContainsKey(creature.ObjectId))
                return false;
            Creatures[creature.ObjectId] = creature;
            if (creature is Player)
                creature.GetController().OnEnterZone(this);
            foreach (IZoneHandler handler in Handlers)
                handler.OnEnterZone(creature, this);
            return true;
        }
    }

    // Java parity: synchronized onLeave(Creature) — non-final in Java (Fly/NoFly/PvP ZoneInstance override), so virtual.
    public virtual bool OnLeave(Creature creature)
    {
        lock (this)
        {
            if (!Creatures.ContainsKey(creature.ObjectId))
                return false;
            Creatures.Remove(creature.ObjectId);
            creature.GetController().OnLeaveZone(this);
            foreach (IZoneHandler handler in Handlers)
                handler.OnLeaveZone(creature, this);
            return true;
        }
    }

    // Java parity: onDie(Creature, Creature)
    public bool OnDie(Creature attacker, Creature target)
    {
        if (!Creatures.ContainsKey(target.ObjectId))
            return false;
        foreach (IZoneHandler handler in Handlers)
        {
            if (handler is IAdvancedZoneHandler advancedZoneHandler)
            {
                if (advancedZoneHandler.OnDie(attacker, target, this))
                    return true;
            }
        }
        return false;
    }

    // Java parity: isInsideCreature(Creature)
    public bool IsInsideCreature(Creature creature) => Creatures.ContainsKey(creature.ObjectId);

    // Java parity: isInsideCordinate(float, float, float)
    public bool IsInsideCordinate(float x, float y, float z) => _template.GetArea().IsInside3D(x, y, z);

    // Java parity: addHandler(IZoneHandler)
    public void AddHandler(IZoneHandler handler) => Handlers.Add(handler);

    private WorldMap Map => World.GetInstance().GetWorldMap(_mapId);
    private int Flags => _template.GetZoneTemplate().GetFlags();

    // Java parity: canFly()
    public bool CanFly()
    {
        if (Flags == -1 || Flags == 0 || Map.HasOverridenOption(ZoneAttributes.Fly))
            return Map.IsFlightAllowed();
        return (Flags & (int)ZoneAttributes.Fly) != 0;
    }

    // Java parity: canGlide()
    public bool CanGlide()
    {
        if (Flags == -1 || Flags == 0 || Map.HasOverridenOption(ZoneAttributes.Glide))
            return Map.CanGlide();
        return (Flags & (int)ZoneAttributes.Glide) != 0;
    }

    // Java parity: canPutKisk()
    public bool CanPutKisk()
    {
        if (Flags == -1 || Flags == 0 || Map.HasOverridenOption(ZoneAttributes.Bind))
            return Map.CanPutKisk();
        return (Flags & (int)ZoneAttributes.Bind) != 0;
    }

    // Java parity: canRecall()
    public bool CanRecall()
    {
        if (Flags == -1 || Flags == 0 || Map.HasOverridenOption(ZoneAttributes.Recall))
            return Map.CanRecall();
        return (Flags & (int)ZoneAttributes.Recall) != 0;
    }

    // Java parity: canReturnToBattle()
    public bool CanReturnToBattle() => Map.CanReturnToBattle();

    // Java parity: canRide()
    public bool CanRide()
    {
        if (Flags == -1 || Flags == 0 || Map.HasOverridenOption(ZoneAttributes.Ride))
            return Map.CanRide();
        return (Flags & (int)ZoneAttributes.Ride) != 0;
    }

    // Java parity: canFlyRide()
    public bool CanFlyRide()
    {
        if (Flags == -1 || Flags == 0 || Map.HasOverridenOption(ZoneAttributes.FlyRide))
            return Map.CanFlyRide();
        return (Flags & (int)ZoneAttributes.FlyRide) != 0;
    }

    // Java parity: isPvpAllowed()
    public bool IsPvpAllowed()
    {
        if (GetZoneTemplate().GetZoneType() != ZoneClassName.Pvp)
            return Map.IsPvpAllowed();
        return (Flags & (int)ZoneAttributes.PvpEnabled) != 0;
    }

    // Java parity: isSameRaceDuelsAllowed()
    public bool IsSameRaceDuelsAllowed()
    {
        if (GetZoneTemplate().GetZoneType() != ZoneClassName.Duel || Flags == 0 || Map.HasOverridenOption(ZoneAttributes.DuelSameRaceEnabled))
            return Map.IsSameRaceDuelsAllowed();
        return (Flags & (int)ZoneAttributes.DuelSameRaceEnabled) != 0;
    }

    // Java parity: isOtherRaceDuelsAllowed()
    public bool IsOtherRaceDuelsAllowed()
    {
        if (GetZoneTemplate().GetZoneType() != ZoneClassName.Duel || Flags == 0 || Map.HasOverridenOption(ZoneAttributes.DuelOtherRaceEnabled))
            return Map.IsOtherRaceDuelsAllowed();
        return (Flags & (int)ZoneAttributes.DuelOtherRaceEnabled) != 0;
    }

    // Java parity: getTownId()
    public int GetTownId() => GetZoneTemplate().GetTownId();

    // Java parity: forEach(Consumer<Creature>)
    public void ForEach(Action<Creature> action) => CollectionUtil.ForEach(Creatures.Values, action);

    // Java parity: isDominionZone()
    public bool IsDominionZone() => GetZoneTemplate().GetZoneType() == ZoneClassName.Dominion;
}
