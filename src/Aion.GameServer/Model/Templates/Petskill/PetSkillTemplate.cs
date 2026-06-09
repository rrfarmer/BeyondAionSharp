using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Petskill;

/// <summary>Java parity: model/templates/petskill/PetSkillTemplate (ATracer).</summary>
[XmlType("pet_skill")]
public class PetSkillTemplate
{
    [XmlAttribute("skill_id")] protected int skillId;
    [XmlAttribute("pet_id")] protected int petId;
    [XmlAttribute("order_skill")] protected int orderSkill;

    public int GetSkillId()
    {
        return skillId;
    }

    public int GetPetId()
    {
        return petId;
    }

    public int GetOrderSkill()
    {
        return orderSkill;
    }
}
