using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Model.Enchants;

/// <summary>Java parity: model/enchants/TemperingStat.</summary>
[XmlType("tempering_stat")]
public class TemperingStat
{
    [XmlAttribute("stat")] public StatEnum stat;
    [XmlAttribute("value")] public int value;

    public StatEnum GetStat()
    {
        return stat;
    }

    public int GetValue()
    {
        return value;
    }
}
