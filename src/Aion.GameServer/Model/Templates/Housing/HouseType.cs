using System;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HouseType (Rolandas). Ordinals match limitTypeIndex (0..4).</summary>
public enum HouseType
{
    ESTATE,
    MANSION,
    HOUSE,
    STUDIO,
    PALACE
}

public static class HouseTypeExtensions
{
    public static int GetLimitTypeIndex(this HouseType t) => (int) t;

    public static int GetId(this HouseType t) => t switch
    {
        HouseType.ESTATE => 3,
        HouseType.MANSION => 2,
        HouseType.HOUSE => 1,
        HouseType.STUDIO => 0,
        HouseType.PALACE => 4,
        _ => throw new ArgumentOutOfRangeException(),
    };

    // building parts end with this letter (like CP_S for palace)
    public static string GetAbbreviation(this HouseType t) => t switch
    {
        HouseType.ESTATE => "a",
        HouseType.MANSION => "b",
        HouseType.HOUSE => "c",
        HouseType.STUDIO => "d",
        HouseType.PALACE => "s",
        _ => throw new ArgumentOutOfRangeException(),
    };
}
