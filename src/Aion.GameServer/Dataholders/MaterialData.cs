using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Materials;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/MaterialData (Rolandas). @XmlRootElement(material_templates); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("material_templates")]
public class MaterialData
{
    [XmlElement("material")] private List<MaterialTemplate> materialTemplates;

    [XmlIgnore] private readonly Dictionary<int, MaterialTemplate> materialsById = new();
    [XmlIgnore] private readonly HashSet<int> skillIds = new();

    public void AfterUnmarshal(object parent)
    {
        if (materialTemplates == null)
            return;

        foreach (MaterialTemplate template in materialTemplates)
        {
            materialsById[template.GetId()] = template;
            if (template.GetSkills() != null)
                foreach (var skill in template.GetSkills())
                    skillIds.Add(skill.GetId());
        }

        materialTemplates = null;
    }

    public MaterialTemplate GetTemplate(int materialId)
    {
        return materialsById.TryGetValue(materialId, out var v) ? v : null;
    }

    public bool IsMaterialSkill(int skillId)
    {
        return skillIds.Contains(skillId);
    }

    public int Size()
    {
        return materialsById.Count;
    }
}
