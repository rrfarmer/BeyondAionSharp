using System;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>Java parity: skillengine/model/SkillLearnTemplate (ATracer, Neon). @XmlType("skill"). classId/skillLearn nullable (Java object types).</summary>
[XmlType("skill")]
public class SkillLearnTemplate
{
    // XmlSerializer binds public members only (Java @XmlAccessorType(FIELD) on private fields). classId (Java
    // PlayerClass object) and skillLearn (Java Integer) are nullable — null when the attribute is absent.
    // XmlSerializer cannot encode a Nullable<enum> NOR a Nullable<int> as an attribute, so both bind via string
    // proxies that parse to the nullable backing fields (mirrors JAXB leaving the field null when absent).
    // race defaults to PC_ALL exactly as Java.
    [XmlIgnore] private PlayerClass? classId;
    [XmlIgnore] private int? skillLearn;

    [XmlAttribute("classId")]
    public string ClassIdRaw
    {
        get => classId?.ToString();
        set => classId = string.IsNullOrEmpty(value) ? null : Enum.Parse<PlayerClass>(value);
    }

    [XmlAttribute("skillLearn")]
    public string SkillLearnRaw
    {
        get => skillLearn?.ToString();
        set => skillLearn = string.IsNullOrEmpty(value) ? null : int.Parse(value);
    }

    [XmlAttribute("skillId")] public int skillId;
    [XmlAttribute("race")] public Race race = Race.PC_ALL;
    [XmlAttribute("minLevel")] public int minLevel;
    [XmlAttribute("autolearn")] public bool autolearn;
    [XmlAttribute("stigma")] public sbyte stigma = 0;

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
