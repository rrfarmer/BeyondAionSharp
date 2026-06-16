using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/UseItemAction (Rolandas).</summary>
[XmlType("UseItemAction")]
public class UseItemAction
{
    // Nullable [XmlAttribute] trap: proxy Nullable<int> through string attributes (see PlaceableHouseObject).
    [XmlIgnore] public int? finalRewardId;

    [XmlAttribute("final_reward_id")]
    public string finalRewardIdRaw
    {
        get => finalRewardId?.ToString();
        set => finalRewardId = string.IsNullOrEmpty(value) ? null : int.Parse(value);
    }

    [XmlIgnore] public int? rewardId;

    [XmlAttribute("reward_id")]
    public string rewardIdRaw
    {
        get => rewardId?.ToString();
        set => rewardId = string.IsNullOrEmpty(value) ? null : int.Parse(value);
    }

    [XmlIgnore] public int? removeCount;

    [XmlAttribute("remove_count")]
    public string removeCountRaw
    {
        get => removeCount?.ToString();
        set => removeCount = string.IsNullOrEmpty(value) ? null : int.Parse(value);
    }

    [XmlIgnore] public int? checkType;

    [XmlAttribute("check_type")]
    public string checkTypeRaw
    {
        get => checkType?.ToString();
        set => checkType = string.IsNullOrEmpty(value) ? null : int.Parse(value);
    }

    public int? GetFinalRewardId() { return finalRewardId; }
    public int? GetRewardId() { return rewardId; }
    public int? GetRemoveCount() { return removeCount; }
    public int? GetCheckType() { return checkType; }
}
