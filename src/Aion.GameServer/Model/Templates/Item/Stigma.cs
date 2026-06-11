using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>Java parity: model/templates/item/Stigma.</summary>
[XmlRoot("Stigma")]
public class Stigma
{
    [XmlAttribute("gain_skill_group1")] private string gainSkillGroup1;
    [XmlAttribute("gain_skill_group2")] private string gainSkillGroup2;
    [XmlAttribute("chargeable")] private bool chargeable;

    [XmlIgnore] private string[] gainSkillGroups;

    // Java parity: afterUnmarshal(Unmarshaller, Object) — invoked by the loader after deserialization.
    public void AfterUnmarshal()
    {
        if (gainSkillGroup2 == null)
            gainSkillGroups = new string[] { gainSkillGroup1 };
        else
            gainSkillGroups = new string[] { gainSkillGroup1, gainSkillGroup2 };
    }

    public string[] GetGainSkillGroups()
    {
        return gainSkillGroups;
    }

    public List<Aion.GameServer.SkillEngine.Model.SkillTemplate> GetGainSkillsByGroup(int groupNo)
    {
        if (groupNo > 0 && groupNo <= gainSkillGroups.Length)
            return Aion.GameServer.Dataholders.DataManager.SKILL_DATA.GetSkillTemplatesByGroup(gainSkillGroups[groupNo - 1]);
        else
            return null;
    }

    public bool IsChargeable()
    {
        return chargeable;
    }
}
