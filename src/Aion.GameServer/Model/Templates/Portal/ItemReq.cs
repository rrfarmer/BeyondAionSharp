using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Portal;

/// <summary>Java parity: model/templates/portal/ItemReq (xTz).</summary>
[XmlType("ItemReq")]
public class ItemReq
{
    [XmlAttribute("item_id")] public int itemId;
    [XmlAttribute("item_count")] public int itemCount;

    public int GetItemId()
    {
        return itemId;
    }

    public void SetItemId(int value)
    {
        this.itemId = value;
    }

    public int GetItemCount()
    {
        return itemCount;
    }

    public void SetItemCount(int value)
    {
        this.itemCount = value;
    }
}
