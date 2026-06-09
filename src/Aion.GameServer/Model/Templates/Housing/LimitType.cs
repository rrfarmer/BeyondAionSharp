using System;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>
/// Java parity: model/templates/housing/LimitType (Rolandas). See client_housing_object_place_tag.xml.
/// Java enum carried per-instance id + personal/trial limit arrays + methods → enum + extension data tables.
/// </summary>
[XmlType("LimitType")]
public enum LimitType
{
    // Limits are in the order of house type: a, b, c, d, s. Ordinals match Java's id values 0..7.
    NONE,
    OWNER_POT,
    VISITOR_POT,
    STORAGE,
    POT,
    COOKING,
    PICTURE,
    JUKEBOX
}

public static class LimitTypeExtensions
{
    private static int[] PersonalLimits(LimitType t) => t switch
    {
        LimitType.NONE => new[] { 0, 0, 0, 0, 0 },
        LimitType.OWNER_POT => new[] { 6, 4, 3, 8, 8 },
        LimitType.VISITOR_POT => new[] { 7, 5, 2, 8, 9 },
        LimitType.STORAGE => new[] { 6, 5, 4, 8, 7 },
        LimitType.POT => new[] { 6, 5, 4, 3, 7 },
        LimitType.COOKING => new[] { 1, 1, 1, 1, 1 },
        LimitType.PICTURE => new[] { 1, 1, 1, 1, 1 },
        LimitType.JUKEBOX => new[] { 1, 1, 1, 1, 1 },
        _ => throw new ArgumentOutOfRangeException(),
    };

    private static int[] TrialLimits(LimitType t) => t switch
    {
        LimitType.NONE => new[] { 0, 0, 0, 0, 0 },
        LimitType.OWNER_POT => new[] { 0, 0, 0, 4, 0 },
        LimitType.VISITOR_POT => new[] { 0, 0, 0, 4, 0 },
        LimitType.STORAGE => new[] { 0, 0, 0, 4, 0 },
        LimitType.POT => new[] { 6, 5, 4, 1, 7 },
        LimitType.COOKING => new[] { 1, 1, 1, 1, 1 },
        LimitType.PICTURE => new[] { 1, 1, 1, 0, 1 },
        LimitType.JUKEBOX => new[] { 1, 1, 1, 0, 1 },
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static string Value(this LimitType t) => t.ToString();

    public static int GetId(this LimitType t) => (int) t;

    public static int GetObjectPlaceLimit(this LimitType t, HouseType houseType) => PersonalLimits(t)[houseType.GetLimitTypeIndex()];

    public static int GetTrialObjectPlaceLimit(this LimitType t, HouseType houseType) => TrialLimits(t)[houseType.GetLimitTypeIndex()];

    public static LimitType FromValue(string value) => (LimitType) Enum.Parse(typeof(LimitType), value);
}
