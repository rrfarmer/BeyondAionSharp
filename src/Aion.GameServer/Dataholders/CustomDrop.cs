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

    [XmlElement("npc_drop")] public List<NpcDrop> npcDrop;

    [XmlIgnore] private Dictionary<int, NpcDrop> dropById = new();

    public NpcDrop GetNpcDrop(int npcId)
    {
        return dropById.TryGetValue(npcId, out var v) ? v : null;
    }

    public void AfterUnmarshal(object parent)
    {
        // Java parity: JAXB fires each nested Drop.afterUnmarshal during unmarshal (validates chance/minAmount and
        // defaults maxAmount=minAmount when 0 — load-bearing for drop counts). XmlSerializer fires no nested JAXB
        // callbacks, so cascade Drop.AfterUnmarshal() children-first before indexing the npc drops.
        foreach (NpcDrop drop in npcDrop)
        {
            if (drop.GetDropGroup() != null)
                foreach (DropGroup dg in drop.GetDropGroup())
                    if (dg.GetDrop() != null)
                        foreach (Drop d in dg.GetDrop())
                            d.AfterUnmarshal();
        }
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
