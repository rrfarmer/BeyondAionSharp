using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Expand;

namespace Aion.GameServer.Model.Templates;

/// <summary>Java parity: model/templates/StorageExpansionTemplate (Simple).</summary>
[XmlRoot("expansion_npc")]
public class StorageExpansionTemplate
{
    [XmlElement("expand")] private List<Expand.Expand> expansions;

    // Java parity: @XmlAttribute(name="ids") int[] — space-separated.
    // C#: string attribute parsed to int[] via property setter.
    private int[] ids;

    [XmlAttribute("ids")]
    public string IdsRaw
    {
        get => ids == null ? null : string.Join(" ", ids);
        set => ids = value == null
            ? null
            : value.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
    }

    public int[] GetNpcIds()
    {
        return ids;
    }

    public int GetMinExpansionLevel()
    {
        return expansions.Select(e => e.GetLevel()).DefaultIfEmpty(0).Min();
    }

    public int GetMaxExpansionLevel()
    {
        return expansions.Select(e => e.GetLevel()).DefaultIfEmpty(0).Max();
    }

    public int? GetPrice(int level)
    {
        foreach (Expand.Expand expand in expansions)
        {
            if (expand.GetLevel() == level)
                return expand.GetPrice();
        }
        return null;
    }
}
