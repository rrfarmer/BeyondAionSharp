using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Challenge;

/// <summary>Java parity: model/templates/challenge/ChallengeReward.</summary>
[XmlType("ChallengeReward")]
public class ChallengeReward
{
    // Java parity: nullable Integer msg_id/value. XmlSerializer binds public members only and cannot encode
    // Nullable<int> as an attribute, so both bind via string proxies that leave the backing field null when the
    // attribute is absent (mirrors JAXB). type is a non-nullable enum (binds by member name).
    [XmlIgnore] private int? msgId;
    [XmlIgnore] private int? value;

    [XmlAttribute("msg_id")]
    public string MsgIdRaw
    {
        get => msgId?.ToString();
        set => msgId = string.IsNullOrEmpty(value) ? null : int.Parse(value);
    }

    [XmlAttribute("value")]
    public string ValueRaw
    {
        get => this.value?.ToString();
        set => this.value = string.IsNullOrEmpty(value) ? null : int.Parse(value);
    }

    [XmlAttribute("type")] public RewardType type;

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
