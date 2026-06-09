using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>Java parity: skillengine/model/SkillLearnTemplate (ATracer, Neon). @XmlType("skill"). classId/skillLearn nullable (Java object types).</summary>
[XmlType("skill")]
public class SkillLearnTemplate
{
    [XmlAttribute("classId")] private PlayerClass? classId;
    [XmlAttribute("skillId")] private int skillId;
    [XmlAttribute("skillLearn")] private int? skillLearn;
    [XmlAttribute("race")] private Race race = Race.PC_ALL;
    [XmlAttribute("minLevel")] private int minLevel;
    [XmlAttribute("autolearn")] private bool autolearn;
    [XmlAttribute("stigma")] private sbyte stigma = 0;

    public PlayerClass? GetClassId()
    {
        return classId;
    }

    public int GetSkillId()
    {
        return skillId;
    }

    public int GetSkillLevel()
    {
        return DataManager.SKILL_DATA.GetSkillTemplate(skillId).GetLvl();
    }

    public int GetMinLevel()
    {
        return minLevel;
    }

    public Race GetRace()
    {
        return race;
    }

    public bool IsAutolearn()
    {
        return autolearn;
    }

    public bool IsStigma()
    {
        return stigma > 0;
    }

    public bool IsLinkedStigma()
    {
        return stigma == 4;
    }

    /// <summary>Returns the skillId of the pre-skill to this one, or null if it has no pre-skill.</summary>
    public int? GetLearnSkill()
    {
        return skillLearn;
    }
}
