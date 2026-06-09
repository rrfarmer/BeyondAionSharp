using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Drop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/CustomDrop (ViAl, Neon). @XmlRootElement(custom_drop); putIfAbsent→TryAdd; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("custom_drop")]
public class CustomDrop
{
    private static readonly ILogger log = NullLogger.Instance;

    [XmlElement("npc_drop")] private List<NpcDrop> npcDrop;

    [XmlIgnore] private Dictionary<int, NpcDrop> dropById = new();

    public NpcDrop GetNpcDrop(int npcId)
    {
        return dropById.TryGetValue(npcId, out var v) ? v : null;
    }

    public void AfterUnmarshal(object parent)
    {
        foreach (NpcDrop drop in npcDrop)
        {
            if (!dropById.TryAdd(drop.GetNpcId(), drop))
                log.LogWarning("Tried to set custom drop for npc " + drop.GetNpcId() + " twice!");
        }
        npcDrop = null;
    }

    public int Size()
    {
        return dropById.Count;
    }
}
