using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Challenge;

/// <summary>Java parity: model/templates/challenge/ChallengeQuestTemplate.</summary>
[XmlType("ChallengeQuest")]
public class ChallengeQuestTemplate
{
    // XmlSerializer binds public members only (Java @XmlAccessorType(FIELD) on protected fields).
    [XmlAttribute("score")] public int score;

    [XmlAttribute("repeat_count")] public int repeatCount;

    [XmlAttribute("id")] public int id;

    public int GetScore()
    {
        return this.score;
    }

    public int GetRepeatCount()
    {
        return this.repeatCount;
    }

    public int GetId()
    {
        return this.id;
    }
}
