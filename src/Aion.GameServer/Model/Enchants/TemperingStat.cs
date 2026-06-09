using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Model.Enchants;

/// <summary>Java parity: model/enchants/TemperingStat.</summary>
[XmlType("tempering_stat")]
public class TemperingStat
{
    [XmlAttribute("stat")] protected StatEnum stat;
    [XmlAttribute("value")] protected int value;

    public StatEnum GetStat()
    {
        return stat;
    }

    public int GetValue()
    {
        return value;
    }
}
