using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HousingUseableItem (Rolandas).</summary>
[XmlType("HousingUseableItem")]
public class HousingUseableItem : PlaceableHouseObject
{
    [XmlElement("action")] protected UseItemAction action;

    [XmlAttribute("owner")] protected bool owner;

    [XmlAttribute("cd")] protected int? cd;

    [XmlAttribute("delay")] protected int delay;

    [XmlAttribute("use_count")] protected int? useCount;

    [XmlAttribute("required_item")] protected int? requiredItem;

    public UseItemAction GetAction()
    {
        return action;
    }

    /// <summary>Can the object be used only by the owner or visitors too.</summary>
    public bool IsOwnerOnly()
    {
        return owner;
    }

    /// <returns>null if no Cooltime is used</returns>
    public int? GetCd()
    {
        return cd;
    }

    public int GetDelay()
    {
        return delay;
    }

    /// <returns>null if use is not restricted</returns>
    public int? GetUseCount()
    {
        return useCount;
    }

    /// <returns>null if no item is required</returns>
    public int? GetRequiredItem()
    {
        return requiredItem;
    }

    public override byte GetTypeId()
    {
        return 1;
    }
}
