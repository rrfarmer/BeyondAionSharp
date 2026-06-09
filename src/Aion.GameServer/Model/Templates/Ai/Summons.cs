using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Ai;

/// <summary>Java parity: model/templates/ai/Summons (xTz).</summary>
[XmlType("Summons")]
public class Summons
{
    [XmlElement("percentage")] private List<Percentage> percentage;

    public List<Percentage> GetPercentage()
    {
        return percentage;
    }
}
