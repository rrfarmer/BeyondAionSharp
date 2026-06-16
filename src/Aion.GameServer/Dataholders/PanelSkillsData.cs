using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Panels;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/PanelSkillsData. @XmlRootElement(polymorph_panels); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("polymorph_panels")]
public class PanelSkillsData
{
    [XmlElement("panel")] public List<SkillPanel> templates;

    [XmlIgnore] private readonly Dictionary<int, SkillPanel> skillPanels = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (SkillPanel panel in templates)
        {
            skillPanels[panel.GetPanelId()] = panel;
        }
        templates = null;
    }

    public SkillPanel GetSkillPanel(int id)
    {
        return skillPanels.TryGetValue(id, out var v) ? v : null;
    }

    public int Size()
    {
        return skillPanels.Count;
    }
}
