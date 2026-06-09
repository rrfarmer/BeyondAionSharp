using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/SkillTreeData (ATracer). @XmlRootElement(skill_tree); .ordinal()→(int)enum; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("skill_tree")]
public class SkillTreeData
{
    [XmlElement("skill")] private List<SkillLearnTemplate> skillTemplates;

    [XmlIgnore] private readonly Dictionary<int, List<SkillLearnTemplate>> templates = new();
    [XmlIgnore] private readonly Dictionary<int, List<SkillLearnTemplate>> templatesById = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (SkillLearnTemplate template in skillTemplates)
        {
            if (template.GetClassId() == null)
            {
                foreach (PlayerClass pClass in Enum.GetValues<PlayerClass>())
                {
                    AddTemplate(pClass, template);
                }
            }
            else
            {
                AddTemplate(template.GetClassId().Value, template);
            }
        }
        skillTemplates = null;
    }

    private void AddTemplate(PlayerClass playerClass, SkillLearnTemplate template)
    {
        int hash = MakeHash((int)playerClass, (int)template.GetRace(), template.GetMinLevel());
        if (!templates.TryGetValue(hash, out var value))
        {
            value = new List<SkillLearnTemplate>();
            templates[hash] = value;
        }

        value.Add(template);

        if (!templatesById.TryGetValue(template.GetSkillId(), out value))
        {
            value = new List<SkillLearnTemplate>();
            templatesById[template.GetSkillId()] = value;
        }

        value.Add(template);
    }

    /// <summary>Search for all skill templates that match the given class, level and race.</summary>
    public List<SkillLearnTemplate> GetTemplatesFor(PlayerClass playerClass, int level, Race race)
    {
        List<SkillLearnTemplate> newSkills = new();

        List<SkillLearnTemplate> classRaceSpecificTemplates = templates.TryGetValue(MakeHash((int)playerClass, (int)race, level), out var a) ? a : null;
        List<SkillLearnTemplate> classSpecificTemplates = templates.TryGetValue(MakeHash((int)playerClass, (int)Race.PC_ALL, level), out var b) ? b : null;

        if (classRaceSpecificTemplates != null)
            newSkills.AddRange(classRaceSpecificTemplates);
        if (classSpecificTemplates != null)
            newSkills.AddRange(classSpecificTemplates);

        return newSkills;
    }

    public List<SkillLearnTemplate> GetSkillsForSkill(int skillId, PlayerClass playerClass, Race race, int playerLevel)
    {
        List<SkillLearnTemplate> skillTree = new();
        foreach (SkillLearnTemplate learnTemplate in GetTemplatesForSkill(GetHighestSkill(skillId), playerClass, race))
        {
            CreateSkillTree(learnTemplate, skillTree);
            break;
        }
        if (playerLevel > -1)
            skillTree.RemoveAll(template => template.GetMinLevel() > playerLevel);
        return skillTree;
    }

    /// <summary>Creates skill tree list in ascending player level order recursively.</summary>
    private void CreateSkillTree(SkillLearnTemplate topSkill, List<SkillLearnTemplate> addList)
    {
        if (topSkill == null)
            return;
        addList.Insert(0, topSkill);
        if (topSkill.GetLearnSkill() == null)
            return;

        foreach (SkillLearnTemplate template in GetTemplatesForSkill(topSkill.GetLearnSkill().Value, topSkill.GetClassId(), topSkill.GetRace()))
        {
            if (topSkill.IsStigma() != template.IsStigma())
                continue;
            CreateSkillTree(template, addList);
            break;
        }
    }

    public List<SkillLearnTemplate> GetTemplatesForSkill(int skillId, PlayerClass? playerClass, Race race)
    {
        List<SkillLearnTemplate> searchSkills = new();
        List<SkillLearnTemplate> byId = templatesById.TryGetValue(skillId, out var v) ? v : null;
        if (byId != null)
        {
            foreach (SkillLearnTemplate template in byId)
                if ((template.GetClassId() == null || template.GetClassId() == playerClass)
                        && (template.GetRace() == Race.PC_ALL || template.GetRace() == race))
                    searchSkills.Add(template);
        }
        return searchSkills;
    }

    /// <summary>The skill id with the highest skill level of this skills' stack.</summary>
    private int GetHighestSkill(int skillId)
    {
        SkillTemplate baseTemplate = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
        if (baseTemplate == null)
            return skillId;
        List<SkillTemplate> stackTemplates = DataManager.SKILL_DATA.GetSkillTemplatesByStack(baseTemplate.GetStack());
        if (stackTemplates == null)
            return skillId;
        SkillTemplate highestSkill = stackTemplates.Count == 0 ? baseTemplate : stackTemplates.Aggregate((t1, t2) => t1.GetLvl() - t2.GetLvl() >= 0 ? t1 : t2);
        return highestSkill.GetSkillId();
    }

    public bool IsLearnedSkill(int skillId)
    {
        return templatesById.TryGetValue(skillId, out _);
    }

    public int Size()
    {
        return templates.Values.Sum(l => l.Count);
    }

    private static int MakeHash(int classId, int race, int level)
    {
        int result = classId << 8;
        result = (result | race) << 8;
        return result | level;
    }
}
