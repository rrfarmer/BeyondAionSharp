using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Npcshout;

/// <summary>Java parity: model/templates/npcshout/ShoutList (Rolandas).</summary>
[XmlType("ShoutList")]
public class ShoutList
{
    [XmlElement("shout")] public List<NpcShout> npcShouts;

    // Java parity: @XmlAttribute List<Integer> npc_ids — space-separated.
    [XmlIgnore] protected List<int> npcIds;

    [XmlAttribute("restrict_world")] public int restrictWorld;

    [XmlAttribute("npc_ids")]
    public string NpcIdsXml
    {
        get => npcIds == null ? null : string.Join(" ", npcIds);
        set
        {
            if (value == null) { npcIds = null; return; }
            string[] parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            npcIds = new List<int>(parts.Length);
            foreach (string p in parts)
                npcIds.Add(int.Parse(p));
        }
    }

    public List<NpcShout> GetNpcShouts()
    {
        return npcShouts == null ? new List<NpcShout>() : npcShouts;
    }

    public List<int> GetNpcIds()
    {
        return npcIds == null ? new List<int>() : npcIds;
    }

    public int GetRestrictWorld()
    {
        return restrictWorld;
    }
}
