using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Item;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HousePart (Rolandas).</summary>
[XmlRoot("house_part")]
public class HousePart
{
    [XmlAttribute("id")] private int id;

    [XmlAttribute("name")] private string name;

    [XmlAttribute("quality")] private ItemQuality quality;

    [XmlAttribute("type")] private PartType type;

    // Java parity: @XmlAttribute(name="building_tags") Set<String> — space-separated.
    private HashSet<string> buildingTags;

    [XmlAttribute("building_tags")]
    public string BuildingTagsRaw
    {
        get => buildingTags == null ? null : string.Join(" ", buildingTags);
        set => buildingTags = value == null
            ? null
            : value.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }

    public int GetId()
    {
        return id;
    }

    public string GetName()
    {
        return name;
    }

    public ItemQuality GetQuality()
    {
        return quality;
    }

    public PartType GetType_()
    {
        return type;
    }

    public ISet<string> GetTags()
    {
        return buildingTags;
    }

    public bool IsForBuilding(Building building)
    {
        return buildingTags.Contains(building.GetPartsMatchTag());
    }
}
