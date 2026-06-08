using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropExcludedNpcs.</summary>
[XmlType("GlobalDropExcludedNpcs")]
public class GlobalDropExcludedNpcs
{
    // Java parity: @XmlList @XmlAttribute(name="npc_ids") — space-separated int list in XML attribute.
    // C# XmlSerializer does not support XmlList on XmlAttribute. Port as a string property
    // that serializes to space-separated integers.
    private HashSet<int>? _npcIds;

    [XmlAttribute("npc_ids")]
    public string? NpcIdsRaw
    {
        get => _npcIds == null ? null : string.Join(" ", _npcIds);
        set => _npcIds = value == null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                   .Select(int.Parse)
                   .ToHashSet();
    }

    // Java parity: getNpcIds()
    public HashSet<int>? GetNpcIds() => _npcIds;
}
