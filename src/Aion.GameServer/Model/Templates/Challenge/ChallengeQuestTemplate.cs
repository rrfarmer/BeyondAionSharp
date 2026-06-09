using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Challenge;

/// <summary>Java parity: model/templates/challenge/ChallengeQuestTemplate.</summary>
[XmlType("ChallengeQuest")]
public class ChallengeQuestTemplate
{
    [XmlAttribute("score")] protected int score;

    [XmlAttribute("repeat_count")] protected int repeatCount;

    [XmlAttribute("id")] protected int id;

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
