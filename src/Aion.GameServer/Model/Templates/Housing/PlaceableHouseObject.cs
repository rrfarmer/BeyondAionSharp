using System;
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
    // Nullable [XmlAttribute] trap: XmlSerializer cannot encode Nullable<int>/Nullable<enum> as an attribute
    // (throws at the serializer ctor -> the whole load aborts -> hollow fallback). Bind via string proxies.
    [XmlIgnore] public int? useDays;

    [XmlAttribute("use_days")]
    public string useDaysRaw
    {
        get => useDays?.ToString();
        set => useDays = string.IsNullOrEmpty(value) ? null : int.Parse(value);
    }

    [XmlIgnore] public LimitType? limit;

    [XmlAttribute("limit")]
    public string limitRaw
    {
        get => limit?.ToString();
        set => limit = string.IsNullOrEmpty(value) ? null : (LimitType)Enum.Parse(typeof(LimitType), value);
    }

    [XmlIgnore] public PlaceLocation? location;

    [XmlAttribute("location")]
    public string locationRaw
    {
        get => location?.ToString();
        set => location = string.IsNullOrEmpty(value) ? null : (PlaceLocation)Enum.Parse(typeof(PlaceLocation), value);
    }

    [XmlIgnore] public PlaceArea? area;

    [XmlAttribute("area")]
    public string areaRaw
    {
        get => area?.ToString();
        set => area = string.IsNullOrEmpty(value) ? null : (PlaceArea)Enum.Parse(typeof(PlaceArea), value);
    }

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
