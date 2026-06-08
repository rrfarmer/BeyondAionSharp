using System;
using System.Collections.Generic;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Event;

namespace Aion.GameServer.GeoEngine.Scene;

/// <summary>
/// Java parity: geoEngine/scene/DespawnableNode.
/// Java's <c>BitSet instances</c> (a sparse set of active instanceIds) → C# HashSet&lt;int&gt;
/// (set/get/or → Add-or-Remove/Contains/UnionWith; behaviorally identical).
/// </summary>
public class DespawnableNode : Node
{
    public DespawnableType type = DespawnableType.NONE;
    public int id = 0; //
    public sbyte levelBitMask = 0;
    private readonly HashSet<int> instances = new HashSet<int>();

    public void SetActive(int instanceId, bool active)
    {
        lock (instances)
        {
            if (active)
                instances.Add(instanceId);
            else
                instances.Remove(instanceId);
        }
    }

    public bool IsActive(int instanceId)
    {
        lock (instances)
        {
            return instances.Contains(instanceId);
        }
    }

    public void CopyFrom(Node node)
    {
        name = node.name;
        collisionIntentions = node.collisionIntentions;
        materialId = node.materialId;
        foreach (Spatial spatial in node.GetChildren())
        {
            if (spatial is Geometry)
            {
                Geometry geom = new Geometry(spatial.GetName(), ((Geometry)spatial).GetMesh());
                AttachChild(geom);
            }
            else if (spatial is Node n)
            {
                AttachChild(n.Clone());
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }

    public override int CollideWith(Collidable other, CollisionResults results)
    {
        if (type == DespawnableType.EVENT)
        {
            if (EventService.GetInstance().GetEventTheme().GetId() != id)
                return 0;
        }
        else if (type == DespawnableType.SHIELD)
        {
            IgnoreProperties? ignoreProperties = results.GetIgnoreProperties();
            if (ignoreProperties == IgnoreProperties.ANY_RACE)
                return 0;
            SiegeLocation loc = SiegeService.GetInstance().GetSiegeLocation(id);
            if (loc != null)
            {
                if (!loc.IsUnderShield())
                    return 0;
                if (ignoreProperties != null)
                {
                    if (loc.GetRace() != SiegeRace.BALAUR && ignoreProperties.GetRace()!.Value.GetRaceId() == loc.GetRace().GetRaceId())
                        return 0;
                    if (loc.GetRace() == SiegeRace.BALAUR && ignoreProperties.GetRace() == IgnoreProperties.BALAUR.GetRace())
                        return 0;
                }
            }
        }
        else if (type != DespawnableType.HOUSE && !IsActive(results.GetInstanceId()))
        {
            return 0;
        }
        else if (results.GetIgnoreProperties() != null)
        {
            if (results.GetIgnoreProperties()!.GetStaticId() > 0 && results.GetIgnoreProperties()!.GetStaticId() == id)
            {
                return 0;
            }
        }
        return base.CollideWith(other, results);
    }

    public override Spatial Clone()
    {
        DespawnableNode node = new DespawnableNode();
        node.type = type;
        node.id = id;
        node.levelBitMask = levelBitMask;
        node.instances.UnionWith(instances);
        node.name = name;
        node.collisionIntentions = collisionIntentions;
        node.materialId = materialId;
        foreach (Spatial spatial in GetChildren())
        {
            if (spatial is Geometry)
            {
                Geometry geom = new Geometry(spatial.GetName(), ((Geometry)spatial).GetMesh());
                node.AttachChild(geom);
            }
            else if (spatial is Node n)
            {
                node.AttachChild(n.Clone());
            }
            else
            {
                throw new NotSupportedException();
            }
        }
        return node;
    }

    public void SetType(DespawnableType type)
    {
        this.type = type;
    }

    public void SetId(int id)
    {
        this.id = id;
    }

    public enum DespawnableType
    {
        NONE = 0,
        EVENT = 1,
        PLACEABLE = 2,
        HOUSE = 3,
        HOUSE_DOOR = 4,
        TOWN_OBJECT = 5,
        DOOR_STATE1 = 6,
        DOOR_STATE2 = 7,
        SHIELD = 8,
    }
}

/// <summary>Static helpers for <see cref="DespawnableNode.DespawnableType"/> (Java enum statics).</summary>
public static class DespawnableTypes
{
    // Java parity: getId() (signed byte).
    public static sbyte GetId(this DespawnableNode.DespawnableType type) => (sbyte)(int)type;

    // Java parity: getById(byte id).
    public static DespawnableNode.DespawnableType GetById(sbyte id)
    {
        foreach (DespawnableNode.DespawnableType type in Enum.GetValues<DespawnableNode.DespawnableType>())
        {
            if (id == type.GetId())
                return type;
        }
        throw new ArgumentException("Invalid ID " + id);
    }
}
