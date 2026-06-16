using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Teleport;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/TeleporterData (orz). @XmlRootElement(npc_teleporter); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("npc_teleporter")]
public class TeleporterData
{
    [XmlElement("teleporter_template")] public List<TeleporterTemplate> templates;

    [XmlIgnore] private readonly Dictionary<int, TeleporterTemplate> teleporterTemplates = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (TeleporterTemplate template in templates)
        {
            teleporterTemplates[template.GetTeleportId()] = template;
        }
        templates = null;
    }

    public int Size()
    {
        return teleporterTemplates.Count;
    }

    public TeleporterTemplate GetTeleporterTemplateByNpcId(int npcId)
    {
        foreach (TeleporterTemplate template in teleporterTemplates.Values)
        {
            if (template.ContainNpc(npcId))
            {
                return template;
            }
        }
        return null;
    }

    public TeleporterTemplate GetTeleporterTemplateByTeleportId(int teleportId)
    {
        return teleporterTemplates.TryGetValue(teleportId, out var v) ? v : null;
    }
}
