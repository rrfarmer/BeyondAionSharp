using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Restriction;

/// <summary>Java parity: model/templates/restriction/ItemCleanupTemplate (KID).</summary>
[XmlRoot("item_restriction_cleanups")]
public class ItemCleanupTemplate
{
    [XmlAttribute("id")] public int id;

    [XmlAttribute("trade")] public sbyte trade = -1;
    [XmlAttribute("sell")] public sbyte sell = -1;
    [XmlAttribute("wh")] public sbyte wh = -1;
    [XmlAttribute("awh")] public sbyte awh = -1;
    [XmlAttribute("lwh")] public sbyte lwh = -1;

    public sbyte ResultTrade()
    {
        return trade;
    }

    public sbyte ResultSell()
    {
        return sell;
    }

    public sbyte ResultWH()
    {
        return wh;
    }

    public sbyte ResultAccountWH()
    {
        return awh;
    }

    public sbyte ResultLegionWH()
    {
        return lwh;
    }

    public int GetId()
    {
        return id;
    }
}
