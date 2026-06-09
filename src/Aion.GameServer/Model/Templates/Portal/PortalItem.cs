using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Portal;

/// <summary>Java parity: model/templates/portal/PortalItem (AionChs Master, Schattenlilie).</summary>
[XmlType("PortalItem")]
public class PortalItem
{
    [XmlAttribute("id")] protected int id;
    [XmlAttribute("itemid")] protected int itemid;
    [XmlAttribute("quantity")] protected int quantity;

    public int GetId()
    {
        return id;
    }

    public int GetItemid()
    {
        return itemid;
    }

    public int GetQuantity()
    {
        return quantity;
    }
}
