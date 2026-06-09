using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/PlaceableHouseObject (Rolandas).</summary>
[XmlType("PlaceableHouseObject")]
[XmlInclude(typeof(HousingJukeBox))]
[XmlInclude(typeof(HousingPicture))]
[XmlInclude(typeof(HousingPostbox))]
[XmlInclude(typeof(HousingChair))]
[XmlInclude(typeof(HousingStorage))]
[XmlInclude(typeof(HousingNpc))]
[XmlInclude(typeof(HousingMoveableItem))]
[XmlInclude(typeof(HousingUseableItem))]
[XmlInclude(typeof(HousingPassiveItem))]
[XmlInclude(typeof(HousingEmblem))]
public abstract class PlaceableHouseObject : AbstractHouseObject
{
    [XmlAttribute("use_days")] protected int? useDays;

    [XmlAttribute("limit")] protected LimitType? limit;

    [XmlAttribute("location")] protected PlaceLocation? location;

    [XmlAttribute("area")] protected PlaceArea? area;

    /// <summary>Gets the value of the useDays property.</summary>
    /// <returns>0 if not restricted</returns>
    public int GetUseDays()
    {
        if (useDays == null)
            return 0;
        return useDays.Value;
    }

    /// <summary>Where the object is allowed to be placed on?</summary>
    /// <returns><see cref="LimitType.NONE"/> if no restriction</returns>
    public LimitType GetPlacementLimit()
    {
        if (limit == null)
            return LimitType.NONE;
        return limit.Value;
    }

    /// <summary>How the object is allowed to be placed (stacks, ground, wall)?</summary>
    public PlaceLocation? GetLocation()
    {
        return location;
    }

    /// <summary>Environment where the object is allowed to be placed (interior, exterior).</summary>
    public PlaceArea? GetArea()
    {
        return area;
    }

    public abstract byte GetTypeId();
}
