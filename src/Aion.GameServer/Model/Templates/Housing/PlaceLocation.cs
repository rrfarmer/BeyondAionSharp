using System;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/PlaceLocation (Rolandas).</summary>
[XmlType("PlaceLocation")]
public enum PlaceLocation
{
    FLOOR,
    STACK,
    WALL
}

public static class PlaceLocationExtensions
{
    public static string Value(this PlaceLocation v) => v.ToString();

    public static PlaceLocation FromValue(string value) => (PlaceLocation) Enum.Parse(typeof(PlaceLocation), value);
}
