using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Cp;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/ConquerorAndProtectorData. @XmlRootElement(conqueror_protector_ranks).</summary>
[XmlRoot("conqueror_protector_ranks")]
public class ConquerorAndProtectorData
{
    [XmlElement("rank")] public List<CPRank> ranks;

    public CPRank GetRank(CPType type, int rank)
    {
        foreach (CPRank template in ranks)
        {
            if (template.GetType_() == type && template.GetRankNum() == rank)
                return template;
        }
        return null;
    }

    public int Size()
    {
        return ranks.Count;
    }
}
