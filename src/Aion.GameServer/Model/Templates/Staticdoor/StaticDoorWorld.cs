using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Staticdoor;

/// <summary>Java parity: model/templates/staticdoor/StaticDoorWorld (xTz).</summary>
[XmlType("World")]
public class StaticDoorWorld
{
    [XmlAttribute("world")] private int worldId;
    [XmlElement("staticdoor")] private List<StaticDoorTemplate> templates;

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
