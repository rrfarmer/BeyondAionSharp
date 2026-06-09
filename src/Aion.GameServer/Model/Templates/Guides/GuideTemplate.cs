using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Guides;

/// <summary>Java parity: model/templates/Guides/GuideTemplate (xTz).</summary>
[XmlType("GuideTemplate")]
public class GuideTemplate
{
    [XmlAttribute("level")] private int level;
    [XmlAttribute("classType")] private PlayerClass classType;
    [XmlAttribute("title")] private string title;
    [XmlAttribute("race")] private Race race;
    [XmlElement("reward_info")] private string rewardInfo = "";
    [XmlElement("message")] private string message = "";
    [XmlElement("select")] private string select = "";
    [XmlElement("survey")] private List<SurveyTemplate> surveys;
    [XmlAttribute("rewardCount")] private int rewardCount;
    [XmlIgnore] private bool isActivated = true;

    /// <returns>the level</returns>
    public int GetLevel()
    {
        return this.level;
    }

    /// <returns>the classId</returns>
    public PlayerClass GetPlayerClass()
    {
        return this.classType;
    }

    /// <returns>the title</returns>
    public string GetTitle()
    {
        return this.title;
    }

    /// <returns>the race</returns>
    public Race GetRace()
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
