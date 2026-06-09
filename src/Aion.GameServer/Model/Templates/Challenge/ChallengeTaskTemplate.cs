using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Challenge;

/// <summary>Java parity: model/templates/challenge/ChallengeTaskTemplate.</summary>
[XmlType("ChallengeTask")]
public class ChallengeTaskTemplate : IL10n
{
    [XmlElement("quest")] private List<ChallengeQuestTemplate> quest;
    [XmlElement("contrib")] private List<ContributionReward> contrib;
    [XmlElement("reward")] private ChallengeReward reward;

    [XmlAttribute("repeat")] private bool repeat;
    [XmlAttribute("town_residence")] private bool townResidence;
    [XmlAttribute("name_id")] private int nameId;
    [XmlAttribute("max_level")] private int maxLevel;
    [XmlAttribute("min_level")] private int minLevel;

    // Java parity: nullable Integer prev_task.
    [XmlAttribute("prev_task")] private int? prevTask;

    [XmlAttribute("race")] private Race race;
    [XmlAttribute("type")] private ChallengeType type;
    [XmlAttribute("id")] private int id;
    [XmlAttribute("legion_level_task")] private bool legionLevelTask = false;

    public List<ChallengeQuestTemplate> GetQuests()
    {
        return quest;
    }

    public List<ContributionReward> GetContrib()
    {
        return contrib;
    }

    public ChallengeReward GetReward()
    {
        return reward;
    }

    public bool IsRepeatable()
    {
        return repeat;
    }

    public bool IsTownResidence()
    {
        return townResidence;
    }

    public int GetL10nId()
    {
        return nameId;
    }

    public int GetMaxLevel()
    {
        return maxLevel;
    }

    public int GetMinLevel()
    {
        return minLevel;
    }

    public int? GetPrevTask()
    {
        return prevTask;
    }

    public Race GetRace()
    {
        return race;
    }

    public ChallengeType GetType_()
    {
        return type;
    }

    public int GetId()
    {
        return id;
    }

    public bool IsLegionLevelTask()
    {
        return legionLevelTask;
    }
}
