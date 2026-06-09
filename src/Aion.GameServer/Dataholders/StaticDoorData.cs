using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Staticdoor;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/StaticDoorData (Wakizashi). @XmlRootElement(staticdoor_templates); putIfAbsent→!TryAdd+throw; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("staticdoor_templates")]
public class StaticDoorData
{
    [XmlElement("world")] private List<StaticDoorWorld> staticDoorWorlds;

    [XmlIgnore] private Dictionary<int, StaticDoorWorld> doorWorlds;

    public void AfterUnmarshal(object parent)
    {
        doorWorlds = new Dictionary<int, StaticDoorWorld>();
        foreach (StaticDoorWorld world in staticDoorWorlds)
        {
            if (!doorWorlds.TryAdd(world.GetWorldId(), world))
                throw new ArgumentException("Duplicate static door world " + world.GetWorldId());
        }
        staticDoorWorlds = null;
    }

    public int Size()
    {
        return doorWorlds.Count;
    }

    public ICollection<StaticDoorTemplate> GetStaticDoors(int worldId)
    {
        StaticDoorWorld doorWorld = doorWorlds.TryGetValue(worldId, out var v) ? v : null;
        return doorWorld == null ? new List<StaticDoorTemplate>() : doorWorld.GetStaticDoors();
    }

    public StaticDoorTemplate GetStaticDoor(int worldId, int staticId)
    {
        StaticDoorWorld doorWorld = doorWorlds.TryGetValue(worldId, out var v) ? v : null;
        return doorWorld == null ? null : doorWorld.GetStaticDoor(staticId);
    }
}
