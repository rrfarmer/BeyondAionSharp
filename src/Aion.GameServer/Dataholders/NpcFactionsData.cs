using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Factions;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/NpcFactionsData (vlog). @XmlRootElement(npc_factions); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("npc_factions")]
public class NpcFactionsData
{
    [XmlElement("npc_faction")] public List<NpcFactionTemplate> npcFactionsData;

    [XmlIgnore] private readonly Dictionary<int, NpcFactionTemplate> factionsById = new();
    [XmlIgnore] private readonly Dictionary<int, NpcFactionTemplate> factionsByNpcId = new();

    public void AfterUnmarshal(object parent)
    {
        factionsById.Clear();
        foreach (NpcFactionTemplate template in npcFactionsData)
        {
            factionsById[template.GetId()] = template;
            if (template.GetNpcIds() != null)
            {
                foreach (int npcId in template.GetNpcIds())
                    factionsByNpcId[npcId] = template;
            }
        }
        npcFactionsData = null;
    }

    public NpcFactionTemplate GetNpcFactionById(int id)
    {
        return factionsById.TryGetValue(id, out var v) ? v : null;
    }

    public NpcFactionTemplate GetNpcFactionByNpcId(int id)
    {
        return factionsByNpcId.TryGetValue(id, out var v) ? v : null;
    }

    public ICollection<NpcFactionTemplate> GetNpcFactionsData()
    {
        return factionsById.Values;
    }

    public int Size()
    {
        return factionsById.Count;
    }
}
