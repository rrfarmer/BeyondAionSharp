using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.QuestEngine.Task.Checker;
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
    private readonly Dictionary<int, Creature> Creatures = new();
    private readonly List<IZoneHandler> Handlers;
    private bool ignoreRegularNpcs = true;

    public ZoneInstance(int mapId, ZoneInfo template)
        : this(mapId, template, new List<IZoneHandler>())
    {
    }

    public ZoneInstance(int mapId, ZoneInfo template, List<IZoneHandler> handlers)
    {
        _template = template;
        _mapId = mapId;
        Handlers = handlers;
    }

    public bool Matches(string zoneName) => _template.Matches(zoneName);

    // Java parity: getAreaTemplate()
    public Area GetAreaTemplate() => _template.GetArea();

    // Java parity: getZoneTemplate()
    public ZoneTemplate GetZoneTemplate() => _template.GetZoneTemplate();

    // Java parity: revalidate(Creature)
    public bool Revalidate(Creature creature) =>
        _mapId == creature.GetWorldId() && _template.GetArea().IsInside3D(creature.GetX(), creature.GetY(), creature.GetZ());

    /// <summary>Whether this zone is irrelevant to the creature and should not track it.</summary>
    // Java reads the AI name directly; this port can reach the zone check before an npc has its AI, so a missing
    // one counts as not the following AI, which is how the npc was treated before this check existed.
    public virtual bool IsIgnored(Creature creature) =>
        ignoreRegularNpcs && creature is Npc npc && creature is not SummonedObject
        && !ZoneChecker.FOLLOWING_AI_NAME.Equals(npc.GetAi()?.GetName());

    // Java parity: synchronized onEnter(Creature) — non-final in Java (Fly/NoFly/PvP ZoneInstance override), so virtual.
    public virtual bool OnEnter(Creature creature)
    {
        lock (this)
        {
            if (!Creatures.TryAdd(creature.ObjectId, creature))
                return false;
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
            if (!Creatures.Remove(creature.ObjectId))
                return false;
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

    // Java parity: isInsideCoordinate(float, float, float)
    public bool IsInsideCoordinate(float x, float y, float z) => _template.GetArea().IsInside3D(x, y, z);

    // Java parity: addHandler(IZoneHandler)
    public void AddHandler(IZoneHandler handler)
    {
        if (handler.HandlesAllCreatures())
            ignoreRegularNpcs = false;
        Handlers.Add(handler);
    }

    // Java parity: getTownId()
    public int GetTownId() => GetZoneTemplate().GetTownId();

    // Java parity: forEach(Consumer<Creature>)
    public void ForEach(Action<Creature> action) => CollectionUtil.ForEach(Creatures.Values, action);

    // Java parity: isDominionZone()
    public bool IsDominionZone() => GetZoneTemplate().GetZoneType() == ZoneClassName.DOMINION;
}
