using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Recipe;

/// <summary>Java parity: model/templates/recipe/ComboProduct (ATracer).</summary>
[XmlType("ComboProduct")]
public class ComboProduct
{
    [XmlAttribute("itemid")] protected int itemid;

    /// <returns>the itemid</returns>
    public int GetItemId()
    {
        return itemid;
    }
}
