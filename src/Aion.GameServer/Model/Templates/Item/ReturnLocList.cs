using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>Java parity: model/templates/item/ReturnLocList (ginho1).</summary>
[XmlType("ReturnLocList")]
public class ReturnLocList : ResultedItemsCollection
{
    [XmlAttribute("index")] public int index;
    [XmlAttribute("worldid")] public int worldid;
    [XmlAttribute("desc")] public string desc;
    [XmlAttribute("alias")] public string alias;

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
