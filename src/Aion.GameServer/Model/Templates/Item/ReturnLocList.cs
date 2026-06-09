using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Item;

/// <summary>Java parity: model/templates/item/ReturnLocList (ginho1).</summary>
[XmlType("ReturnLocList")]
public class ReturnLocList : ResultedItemsCollection
{
    [XmlAttribute("index")] protected int index;
    [XmlAttribute("worldid")] protected int worldid;
    [XmlAttribute("desc")] protected string desc;
    [XmlAttribute("alias")] protected string alias;

    public float GetIndex()
    {
        return index;
    }

    public int GetWorldid()
    {
        return worldid;
    }

    public string GetDesc()
    {
        return desc;
    }

    public string GetAlias()
    {
        return alias;
    }
}
