using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Challenge;

/// <summary>Java parity: model/templates/challenge/ChallengeReward.</summary>
[XmlType("ChallengeReward")]
public class ChallengeReward
{
    [XmlAttribute("msg_id")] protected int? msgId;

    [XmlAttribute("value")] protected int? value;

    [XmlAttribute("type")] protected RewardType type;

    public int? GetMsgId()
    {
        return this.msgId;
    }

    public int? GetValue()
    {
        return this.value;
    }

    public RewardType GetType_()
    {
        return this.type;
    }
}
