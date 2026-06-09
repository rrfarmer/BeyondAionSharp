using System;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/PartType (Rolandas).</summary>
[XmlType("PartType")]
public enum PartType
{
    ROOF,
    OUTWALL,
    FRAME,
    DOOR,
    GARDEN,
    FENCE,
    // 7 is unused
    INWALL_ANY,
    INFLOOR_ANY,
    // 20 - 26 is unknown, sometimes sent in CM_HOUSE_DECORATE (lineNo)
    ADDON
    // 28 - 29 is unknown, sometimes sent in CM_HOUSE_DECORATE (lineNo)
}

public static class PartTypeExtensions
{
    public static int GetStartLineNr(this PartType t) => t switch
    {
        PartType.ROOF => 1,
        PartType.OUTWALL => 2,
        PartType.FRAME => 3,
        PartType.DOOR => 4,
        PartType.GARDEN => 5,
        PartType.FENCE => 6,
        PartType.INWALL_ANY => 8,
        PartType.INFLOOR_ANY => 14,
        PartType.ADDON => 27,
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static int GetEndLineNr(this PartType t) => t switch
    {
        PartType.ROOF => 1,
        PartType.OUTWALL => 2,
        PartType.FRAME => 3,
        PartType.DOOR => 4,
        PartType.GARDEN => 5,
        PartType.FENCE => 6,
        PartType.INWALL_ANY => 13,
        PartType.INFLOOR_ANY => 19,
        PartType.ADDON => 27,
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static int GetRooms(this PartType t) => t.GetEndLineNr() - t.GetStartLineNr() + 1;

    public static PartType? GetForLineNr(int lineNr)
    {
        foreach (PartType type in Enum.GetValues<PartType>())
        {
            if (type.GetStartLineNr() <= lineNr && type.GetEndLineNr() >= lineNr)
                return type;
        }
        return null;
    }
}
