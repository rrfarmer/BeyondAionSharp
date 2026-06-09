using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Siegelocation;

/// <summary>Java parity: model/templates/siegelocation/SiegeRelatedBases (Estrayl).</summary>
[XmlType("SiegeRelatedBases")]
public class SiegeRelatedBases
{
    // Java parity: @XmlList @XmlAttribute(name="ids") List<Integer> — space-separated.
    private List<int> baseIds;

    [XmlAttribute("ids")]
    public string BaseIdsRaw
    {
        get => baseIds == null ? null : string.Join(" ", baseIds);
        set => baseIds = value == null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    public List<int> GetBaseIds()
    {
        return baseIds;
    }
}
