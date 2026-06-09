using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Portal;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/InstanceExitData. @XmlRootElement(instance_exits); computeIfAbsent→TryGetValue+init; getOrDefault→TryGetValue else empty; stream mapToInt.sum→Sum.</summary>
[XmlRoot("instance_exits")]
public class InstanceExitData
{
    [XmlElement("instance_exit")] protected List<InstanceExit> instanceExit;

    [XmlIgnore] private readonly Dictionary<int, List<InstanceExit>> instanceExitByWorldId = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (InstanceExit exit in instanceExit)
        {
            int key = exit.GetInstanceId().Value;
            if (!instanceExitByWorldId.TryGetValue(key, out var list))
            {
                list = new List<InstanceExit>();
                instanceExitByWorldId[key] = list;
            }
            list.Add(exit);
        }
        instanceExit = null;
    }

    public InstanceExit GetInstanceExit(int worldId, Race race)
    {
        List<InstanceExit> instanceExits = instanceExitByWorldId.TryGetValue(worldId, out var v) ? v : new List<InstanceExit>();
        if (instanceExits.Count == 0)
            return null;
        foreach (InstanceExit instanceExit in instanceExits)
            if (instanceExit.GetRace() == Race.PC_ALL || instanceExit.GetRace() == race)
                return instanceExit;
        return null;
    }

    public int Size()
    {
        return instanceExitByWorldId.Values.Sum(l => l.Count);
    }
}
