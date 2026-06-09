using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Petskill;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/PetSkillData (ATracer). @XmlRootElement(pet_skill_templates); computeIfAbsent→TryGetValue+init; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("pet_skill_templates")]
public class PetSkillData
{
    [XmlElement("pet_skill")] private List<PetSkillTemplate> petSkills;

    [XmlIgnore] private readonly Dictionary<int, Dictionary<int, int>> petSkillData = new();
    [XmlIgnore] private readonly Dictionary<int, List<int>> petSkillsMap = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (PetSkillTemplate petSkill in petSkills)
        {
            if (!petSkillData.TryGetValue(petSkill.GetOrderSkill(), out var orderMap))
            {
                orderMap = new Dictionary<int, int>();
                petSkillData[petSkill.GetOrderSkill()] = orderMap;
            }
            orderMap[petSkill.GetPetId()] = petSkill.GetSkillId();

            if (!petSkillsMap.TryGetValue(petSkill.GetPetId(), out var skillList))
            {
                skillList = new List<int>();
                petSkillsMap[petSkill.GetPetId()] = skillList;
            }
            skillList.Add(petSkill.GetSkillId());
        }
        petSkills = null;
    }

    public int Size()
    {
        return petSkillData.Count;
    }

    public bool IsPetOrderSkill(int orderSkill)
    {
        return petSkillData.ContainsKey(orderSkill);
    }

    public int GetPetOrderSkill(int orderSkill, int petNpcId)
    {
        return petSkillData[orderSkill][petNpcId];
    }

    public bool PetHasSkill(int petNpcId, int skillId)
    {
        return petSkillsMap[petNpcId].Contains(skillId);
    }
}
