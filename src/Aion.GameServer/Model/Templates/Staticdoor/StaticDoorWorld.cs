using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Staticdoor;

/// <summary>Java parity: model/templates/staticdoor/StaticDoorWorld (xTz).</summary>
[XmlType("World")]
public class StaticDoorWorld
{
    // XmlSerializer binds only public members (JAXB read these via @XmlAccessorType(FIELD)).
    [XmlAttribute("world")] public int worldId;
    [XmlElement("staticdoor")] public List<StaticDoorTemplate> templates;

    [XmlIgnore] private Dictionary<int, StaticDoorTemplate> templatesByStaticId;

    // Java parity: afterUnmarshal(Unmarshaller, Object parent).
    public void AfterUnmarshal(object parent)
    {
        templatesByStaticId = new Dictionary<int, StaticDoorTemplate>();
        foreach (StaticDoorTemplate template in templates)
        {
            if (!templatesByStaticId.TryAdd(template.GetId(), template))
                throw new System.ArgumentException("Duplicate door template for world " + worldId + ", id: " + template.GetId());
        }
        templates = null;
    }

    public int GetWorldId()
    {
        return worldId;
    }

    public ICollection<StaticDoorTemplate> GetStaticDoors()
    {
        return templatesByStaticId.Values;
    }

    public StaticDoorTemplate GetStaticDoor(int staticId)
    {
        return templatesByStaticId.TryGetValue(staticId, out var t) ? t : null;
    }
}
