using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Guides;

/// <summary>Java parity: model/templates/Guides/GuideTemplate (xTz).</summary>
[XmlType("GuideTemplate")]
public class GuideTemplate
{
    [XmlAttribute("level")] public int level;
    // Java parity: nullable PlayerClass/Race. XmlSerializer cannot bind a Nullable<enum> attribute, so public
    // string proxies carry the wire value and the backing fields stay the faithful nullable enums.
    [XmlIgnore] public PlayerClass? classType;
    [XmlAttribute("title")] public string title;
    [XmlIgnore] public Race? race;

    [XmlAttribute("classType")]
    public string ClassTypeRaw
    {
        get => classType?.ToString();
        set => classType = string.IsNullOrEmpty(value) ? null : System.Enum.Parse<PlayerClass>(value);
    }

    [XmlAttribute("race")]
    public string RaceRaw
    {
        get => race?.ToString();
        set => race = string.IsNullOrEmpty(value) ? null : System.Enum.Parse<Race>(value);
    }

    [XmlElement("reward_info")] public string rewardInfo = "";
    [XmlElement("message")] public string message = "";
    [XmlElement("select")] public string select = "";
    [XmlElement("survey")] public List<SurveyTemplate> surveys;
    [XmlAttribute("rewardCount")] public int rewardCount;
    [XmlIgnore] private bool isActivated = true;

    /// <returns>the level</returns>
    public int GetLevel()
    {
        return this.level;
    }

    /// <returns>the classId</returns>
    public PlayerClass? GetPlayerClass()
    {
        return this.classType;
    }

    /// <returns>the title</returns>
    public string GetTitle()
    {
        return this.title;
    }

    /// <returns>the race</returns>
    public Race? GetRace()
    {
        return this.race;
    }

    /// <returns>the surveys</returns>
    public List<SurveyTemplate> GetSurveys()
    {
        return this.surveys;
    }

    /// <returns>the message</returns>
    public string GetMessage()
    {
        return this.message;
    }

    /// <returns>the select</returns>
    public string GetSelect()
    {
        return this.select;
    }

    /// <returns>the rewardInfo</returns>
    public string GetRewardInfo()
    {
        return this.rewardInfo;
    }

    public int GetRewardCount()
    {
        return this.rewardCount;
    }

    /// <returns>the isActivated</returns>
    public bool IsActivated()
    {
        return isActivated;
    }

    public void SetActivated(bool isActivated)
    {
        this.isActivated = isActivated;
    }
}
