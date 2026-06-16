using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/SkillData (ATracer, Neon). @XmlRootElement(skill_data); LinkedHashMap→Dictionary; computeIfAbsent→TryGetValue+init; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("skill_data")]
public class SkillData
{
    private static readonly ILogger log = NullLogger.Instance;

    // Public so XmlSerializer can populate it (JAXB read the private field via @XmlAccessorType(FIELD)).
    [XmlElement("skill_template")] public List<SkillTemplate> skillTemplates;

    [XmlIgnore] private readonly Dictionary<int, SkillTemplate> skillTemplateById = new();
    [XmlIgnore] private readonly Dictionary<string, List<SkillTemplate>> skillTemplatesByGroup = new();
    [XmlIgnore] private readonly Dictionary<string, List<SkillTemplate>> skillTemplatesByStack = new();

    public void AfterUnmarshal(object parent)
    {
        skillTemplateById.Clear();
        skillTemplatesByGroup.Clear();
        skillTemplatesByStack.Clear();
        foreach (SkillTemplate skillTemplate in skillTemplates)
        {
            // Java parity: JAXB fires Effects.afterUnmarshal (building the effectTypes set) per <effects>
            // element before the parent holder's afterUnmarshal; XmlSerializer does not invoke JAXB callbacks,
            // so fire it here, children-first, before indexing the template.
            skillTemplate.GetEffects()?.AfterUnmarshal(this);
            int skillId = skillTemplate.GetSkillId();
            skillTemplateById[skillId] = skillTemplate;
            if (skillTemplate.GetGroup() != null)
            {
                if (!skillTemplatesByGroup.TryGetValue(skillTemplate.GetGroup(), out var groupList))
                {
                    groupList = new List<SkillTemplate>();
                    skillTemplatesByGroup[skillTemplate.GetGroup()] = groupList;
                }
                groupList.Add(skillTemplate);
            }
            if (skillTemplate.GetStack() != null)
            {
                if (!skillTemplatesByStack.TryGetValue(skillTemplate.GetStack(), out var stackList))
                {
                    stackList = new List<SkillTemplate>();
                    skillTemplatesByStack[skillTemplate.GetStack()] = stackList;
                }
                stackList.Add(skillTemplate);
            }
        }
        skillTemplates = null;
    }

    public SkillTemplate GetSkillTemplate(int skillId)
    {
        return skillTemplateById.TryGetValue(skillId, out var v) ? v : null;
    }

    /// <summary>All skill templates of this group. A group is less precise and may be null.</summary>
    public List<SkillTemplate> GetSkillTemplatesByGroup(string skillGroup)
    {
        return skillTemplatesByGroup.TryGetValue(skillGroup, out var v) ? v : null;
    }

    /// <summary>All skill templates of this stack. A stack is more precise than a group.</summary>
    public List<SkillTemplate> GetSkillTemplatesByStack(string skillStack)
    {
        return skillTemplatesByStack.TryGetValue(skillStack, out var v) ? v : null;
    }

    public int Size()
    {
        return skillTemplateById.Count;
    }

    public ICollection<SkillTemplate> GetSkillTemplates()
    {
        return skillTemplateById.Values;
    }

    public void ValidateMotions()
    {
        StringBuilder missing = new StringBuilder();
        HashSet<string> motionNames = new HashSet<string>();
        foreach (SkillTemplate t in GetSkillTemplates())
        {
            Motion m = t.GetMotion();
            if (m == null || m.GetName() == null)
                continue;
            if (motionNames.Add(m.GetName()))
            {
                MotionTime mt = DataManager.MOTION_DATA.GetMotionTime(m.GetName());
                if (mt == null)
                    missing.Append('"').Append(m.GetName()).Append("\" (skill id ").Append(t.GetSkillId()).Append("), ");
            }
        }
        if (missing.Length > 0)
            log.LogWarning("Missing motion times for these motion names: {Missing}", missing.ToString().Substring(0, missing.Length - 2));
    }
}
