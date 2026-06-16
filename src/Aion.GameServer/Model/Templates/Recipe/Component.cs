using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Recipe;

/// <summary>Java parity: model/templates/recipe/Component (ATracer).</summary>
[XmlType("Component")]
public class Component
{
    [XmlAttribute("itemid")] public int itemid;
    [XmlAttribute("quantity")] public int quantity;

    public int GetItemId()
    {
        return itemid;
    }

    public int GetQuantity()
    {
        return quantity;
    }
}
