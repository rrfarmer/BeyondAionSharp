using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/UseItemAction (Rolandas).</summary>
[XmlType("UseItemAction")]
public class UseItemAction
{
    [XmlAttribute("final_reward_id")] protected int? finalRewardId;
    [XmlAttribute("reward_id")] protected int? rewardId;
    [XmlAttribute("remove_count")] protected int? removeCount;
    [XmlAttribute("check_type")] protected int? checkType;

    public int? GetFinalRewardId() { return finalRewardId; }
    public int? GetRewardId() { return rewardId; }
    public int? GetRemoveCount() { return removeCount; }
    public int? GetCheckType() { return checkType; }
}
