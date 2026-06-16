using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HousingUseableItem (Rolandas).</summary>
[XmlType("HousingUseableItem")]
public class HousingUseableItem : PlaceableHouseObject
{
    [XmlElement("action")] public UseItemAction action;

    [XmlAttribute("owner")] public bool owner;

    [XmlAttribute("delay")] public int delay;

    // Nullable [XmlAttribute] trap: proxy Nullable<int> through string attributes (see PlaceableHouseObject).
    [XmlIgnore] public int? cd;

    [XmlAttribute("cd")]
    public string cdRaw
    {
        get => cd?.ToString();
        set => cd = string.IsNullOrEmpty(value) ? null : int.Parse(value);
    }

    [XmlIgnore] public int? useCount;

    [XmlAttribute("use_count")]
    public string useCountRaw
    {
        get => useCount?.ToString();
        set => useCount = string.IsNullOrEmpty(value) ? null : int.Parse(value);
    }

    [XmlIgnore] public int? requiredItem;

    [XmlAttribute("required_item")]
    public string requiredItemRaw
    {
        get => requiredItem?.ToString();
        set => requiredItem = string.IsNullOrEmpty(value) ? null : int.Parse(value);
    }

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
