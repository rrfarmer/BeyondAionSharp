using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Challenge;

/// <summary>Java parity: model/templates/challenge/ChallengeTaskTemplate.</summary>
[XmlType("ChallengeTask")]
public class ChallengeTaskTemplate : IL10n
{
    // XmlSerializer binds public members only (Java @XmlAccessorType(FIELD) on private fields).
    [XmlElement("quest")] public List<ChallengeQuestTemplate> quest;
    [XmlElement("contrib")] public List<ContributionReward> contrib;
    [XmlElement("reward")] public ChallengeReward reward;

    [XmlAttribute("repeat")] public bool repeat;
    [XmlAttribute("town_residence")] public bool townResidence;
    [XmlAttribute("name_id")] public int nameId;
    [XmlAttribute("max_level")] public int maxLevel;
    [XmlAttribute("min_level")] public int minLevel;

    // Java parity: nullable Integer prev_task. XmlSerializer cannot encode Nullable<int> as an attribute, so it
    // binds via a string proxy that leaves the backing field null when the attribute is absent (mirrors JAXB).
    [XmlIgnore] private int? prevTask;

    [XmlAttribute("prev_task")]
    public string PrevTaskRaw
    {
        get => prevTask?.ToString();
        set => prevTask = string.IsNullOrEmpty(value) ? null : int.Parse(value);
    }

    [XmlAttribute("race")] public Race race;
    [XmlAttribute("type")] public ChallengeType type;
    [XmlAttribute("id")] public int id;
    [XmlAttribute("legion_level_task")] public bool legionLevelTask = false;

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
