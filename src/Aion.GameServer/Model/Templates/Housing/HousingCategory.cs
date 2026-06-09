using System;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HousingCategory (Rolandas).</summary>
[XmlType("HousingObjectType")]
public enum HousingCategory
{
    BED,
    BOOK,
    CARPET,
    CHAIR,
    CURTAIN,
    DECORATION,
    LIGHT,
    NPC,
    OUTLIGHT,
    TABLE
}

public static class HousingCategoryExtensions
{
    public static string Value(this HousingCategory v) => v.ToString();

    public static HousingCategory FromValue(string value) => (HousingCategory) Enum.Parse(typeof(HousingCategory), value);
}
