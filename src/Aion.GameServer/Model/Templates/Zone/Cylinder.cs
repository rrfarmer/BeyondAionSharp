using System.Globalization;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Zone;

/// <summary>Java parity: model/templates/zone/Cylinder — cylinder zone geometry descriptor.</summary>
[XmlType("Cylinder")]
public class Cylinder
{
    // Java parity: nullable Float attributes. XmlSerializer cannot bind Nullable<float> to an attribute, so
    // public string proxies carry the wire value and the backing properties stay the faithful float?.
    [XmlIgnore] public float? Top    { get; set; }
    [XmlIgnore] public float? Bottom { get; set; }
    [XmlIgnore] public float? X      { get; set; }
    [XmlIgnore] public float? Y      { get; set; }
    [XmlIgnore] public float? R      { get; set; }

    [XmlAttribute("top")]    public string? TopRaw    { get => Raw(Top);    set => Top = Parse(value); }
    [XmlAttribute("bottom")] public string? BottomRaw { get => Raw(Bottom); set => Bottom = Parse(value); }
    [XmlAttribute("x")]      public string? XRaw      { get => Raw(X);      set => X = Parse(value); }
    [XmlAttribute("y")]      public string? YRaw      { get => Raw(Y);      set => Y = Parse(value); }
    [XmlAttribute("r")]      public string? RRaw      { get => Raw(R);      set => R = Parse(value); }

    private static string? Raw(float? v) => v?.ToString(CultureInfo.InvariantCulture);
    private static float? Parse(string? v) => string.IsNullOrEmpty(v) ? null : float.Parse(v, CultureInfo.InvariantCulture);

    public Cylinder() { }

    public Cylinder(float x, float y, float radius, float top, float bottom)
    {
        X = x; Y = y; R = radius; Top = top; Bottom = bottom;
    }

    public float? GetTop()    => Top;
    public float? GetBottom() => Bottom;
    public float? GetX()      => X;
    public float? GetY()      => Y;
    public float? GetR()      => R;
}
