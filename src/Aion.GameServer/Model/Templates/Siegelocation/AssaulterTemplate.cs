using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Siege;

namespace Aion.GameServer.Model.Templates.Siegelocation;

/// <summary>Java parity: model/templates/siegelocation/AssaulterTemplate (Estrayl).</summary>
[XmlType("AssaulterTemplate")]
public class AssaulterTemplate
{
    [XmlAttribute("type")] public AssaulterType assaulterType;
    [XmlAttribute("heading_offset")] public int headingOffset = 60;
    [XmlAttribute("distance_offset")] public int distanceOffset;

    // Java parity: @XmlList @XmlAttribute(name="npc_ids") List<Integer> — space-separated.
    private List<int> npcIds;

    [XmlAttribute("npc_ids")]
    public string NpcIdsRaw
    {
        get => npcIds == null ? null : string.Join(" ", npcIds);
        set => npcIds = value == null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    public AssaulterType GetAssaulterType()
    {
        return assaulterType;
    }

    public List<int> GetNpcIds()
    {
        return npcIds;
    }

    public int GetHeadingOffset()
    {
        return headingOffset;
    }

    public int GetDistanceOffset()
    {
        return distanceOffset;
    }
}
