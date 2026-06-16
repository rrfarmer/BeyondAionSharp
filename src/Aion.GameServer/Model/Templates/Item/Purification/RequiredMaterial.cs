using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items.Purification;

/// <summary>Java parity: model/templates/item/purification/RequiredMaterial (Ranastic, Navyan, Estrayl).</summary>
[XmlRoot("RequiredMaterial")]
public class RequiredMaterial
{
    [XmlAttribute("item_id")] public int itemId;
    [XmlAttribute("item_count")] public int itemCount;

    public int GetItemId()
    {
        return itemId;
    }

    public int GetItemCount()
    {
        return itemCount;
    }
}
