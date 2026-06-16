using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/Building (Rolandas).</summary>
[XmlRoot("building")]
public class Building
{
    [XmlAttribute("id")] public int id;

    [XmlElement("parts")] public Parts parts;

    [XmlAttribute("default")] public bool isDefault;

    [XmlAttribute("parts_match")] public string partsMatch;

    // Java parity: nullable HouseType/BuildingType. XmlSerializer cannot bind a Nullable<enum> attribute, so
    // public string proxies carry the wire value and the backing fields stay the faithful nullable enums.
    [XmlIgnore] public HouseType? size;

    [XmlIgnore] public BuildingType? type;

    [XmlAttribute("size")]
    public string SizeRaw
    {
        get => size?.ToString();
        set => size = string.IsNullOrEmpty(value) ? null : System.Enum.Parse<HouseType>(value);
    }

    [XmlAttribute("type")]
    public string TypeRaw
    {
        get => type?.ToString();
        set => type = string.IsNullOrEmpty(value) ? null : System.Enum.Parse<BuildingType>(value);
    }

    [XmlIgnore] private Dictionary<PartType, int> partsByType;

    // Java parity: afterUnmarshal(Unmarshaller, Object parent) — invoked by the housing loader after deserialization.
    public void AfterUnmarshal(object parent)
    {
        if (parts == null)
            return;
        partsByType = new Dictionary<PartType, int>();
        if (parts.GetDoor() != 0)
            partsByType[PartType.DOOR] = parts.GetDoor();
        if (parts.GetFence() != null)
            partsByType[PartType.FENCE] = parts.GetFence().Value;
        if (parts.GetFrame() != null)
            partsByType[PartType.FRAME] = parts.GetFrame().Value;
        if (parts.GetGarden() != null)
            partsByType[PartType.GARDEN] = parts.GetGarden().Value;
        if (parts.GetInfloor() != 0)
            partsByType[PartType.INFLOOR_ANY] = parts.GetInfloor();
        if (parts.GetInwall() != 0)
            partsByType[PartType.INWALL_ANY] = parts.GetInwall();
        if (parts.GetOutwall() != null)
            partsByType[PartType.OUTWALL] = parts.GetOutwall().Value;
        if (parts.GetRoof() != null)
            partsByType[PartType.ROOF] = parts.GetRoof().Value;
    }

    public int GetId()
    {
        return id;
    }

    public bool IsDefault()
    {
        return isDefault;
    }

    // All DataManager calls are just to ensure integrity if called from housing land templates. Because buildings in land templates have only id and
    // isDefault set. Buildings template has full info though, except isDefault value for the land.

    public string GetPartsMatchTag()
    {
        if (partsMatch == null)
            return DataManager.HOUSE_BUILDING_DATA.GetBuilding(id).partsMatch;
        return partsMatch;
    }

    public HouseType GetSize()
    {
        if (size == null)
            return DataManager.HOUSE_BUILDING_DATA.GetBuilding(id).size.Value;
        return size.Value;
    }

    public BuildingType GetType_()
    {
        if (type == null)
            return DataManager.HOUSE_BUILDING_DATA.GetBuilding(id).type.Value;
        return type.Value;
    }

    public int? GetDefaultDecorId(PartType partType)
    {
        return GetPartsByType().TryGetValue(partType, out int v) ? v : (int?) null;
    }

    public List<int> GetDefaultPartIds()
    {
        return new List<int>(GetPartsByType().Values);
    }

    private Dictionary<PartType, int> GetPartsByType()
    {
        if (partsByType == null)
            return DataManager.HOUSE_BUILDING_DATA.GetBuilding(id).partsByType;
        return partsByType;
    }
}
