using System;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/PlaceArea (Rolandas).</summary>
[XmlType("PlaceArea")]
public enum PlaceArea
{
    ALL,
    INTERIOR,
    EXTERIOR
}

public static class PlaceAreaExtensions
{
    public static string Value(this PlaceArea v) => v.ToString();

    public static PlaceArea FromValue(string value) => (PlaceArea) Enum.Parse(typeof(PlaceArea), value);
}
