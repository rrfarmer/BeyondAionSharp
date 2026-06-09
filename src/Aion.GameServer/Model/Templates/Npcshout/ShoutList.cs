using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Npcshout;

/// <summary>Java parity: model/templates/npcshout/ShoutList (Rolandas).</summary>
[XmlType("ShoutList")]
public class ShoutList
{
    [XmlElement("shout")] protected List<NpcShout> npcShouts;

    // Java parity: @XmlAttribute List<Integer> npc_ids — space-separated.
    [XmlIgnore] protected List<int> npcIds;

    // Java parity: nullable Integer; getter collapses null→0, so plain int (attribute absent→0) is behaviorally faithful.
    [XmlAttribute("restrict_world")] protected int restrictWorld;

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
        if (npcShouts == null)
        {
            npcShouts = new List<NpcShout>();
        }
        return this.npcShouts;
    }

    public List<int> GetNpcIds()
    {
        if (npcIds == null)
        {
            npcIds = new List<int>();
        }
        return this.npcIds;
    }

    public int GetRestrictWorld()
    {
        return restrictWorld;
    }

    public void MakeNull()
    {
        this.npcIds = null;
        this.npcShouts = null;
        this.restrictWorld = 0;
    }
}
