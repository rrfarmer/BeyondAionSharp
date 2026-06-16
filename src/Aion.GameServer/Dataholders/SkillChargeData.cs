using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/SkillChargeData (Rolandas). @XmlRootElement(skill_charge); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("skill_charge")]
public class SkillChargeData
{
    [XmlElement("charge")] public List<ChargeSkillEntry> chargeSkills;

    [XmlIgnore] private readonly Dictionary<int, ChargeSkillEntry> skillChargeData = new();
    [XmlIgnore] private readonly HashSet<int> skillIds = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (ChargeSkillEntry chargeSkill in chargeSkills)
        {
            skillChargeData[chargeSkill.GetId()] = chargeSkill;
            foreach (var s in chargeSkill.GetSkills())
                skillIds.Add(s.GetId());
        }
        chargeSkills = null;
    }

    public ChargeSkillEntry GetChargedSkillEntry(int chargeId)
    {
        return skillChargeData.TryGetValue(chargeId, out var v) ? v : null;
    }

    public bool IsChargeSkill(SkillTemplate skillTemplate)
    {
        return skillIds.Contains(skillTemplate.GetSkillId());
    }

    public int Size()
    {
        return skillChargeData.Count;
    }
}
