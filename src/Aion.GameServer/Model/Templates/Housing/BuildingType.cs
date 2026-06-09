using System;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/BuildingType (Rolandas).</summary>
[XmlType("BuildingType")]
public enum BuildingType
{
    PERSONAL_FIELD,
    PERSONAL_INS
}

public static class BuildingTypeExtensions
{
    public static int GetId(this BuildingType t) => t switch
    {
        BuildingType.PERSONAL_FIELD => 2,
        BuildingType.PERSONAL_INS => 1,
        _ => throw new ArgumentOutOfRangeException(),
    };
}
