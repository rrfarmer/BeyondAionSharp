using System.Globalization;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Zone;

/// <summary>Java parity: model/templates/zone/Sphere — sphere zone geometry descriptor.</summary>
[XmlType("Sphere")]
public class Sphere
{
    // Java parity: nullable Float attributes. XmlSerializer cannot bind Nullable<float> to an attribute, so
    // public string proxies carry the wire value and the backing properties stay the faithful float?.
    [XmlIgnore] public float? X { get; set; }
    [XmlIgnore] public float? Y { get; set; }
    [XmlIgnore] public float? Z { get; set; }
    [XmlIgnore] public float? R { get; set; }

    [XmlAttribute("x")] public string? XRaw { get => Raw(X); set => X = Parse(value); }
    [XmlAttribute("y")] public string? YRaw { get => Raw(Y); set => Y = Parse(value); }
    [XmlAttribute("z")] public string? ZRaw { get => Raw(Z); set => Z = Parse(value); }
    [XmlAttribute("r")] public string? RRaw { get => Raw(R); set => R = Parse(value); }

    private static string? Raw(float? v) => v?.ToString(CultureInfo.InvariantCulture);
    private static float? Parse(string? v) => string.IsNullOrEmpty(v) ? null : float.Parse(v, CultureInfo.InvariantCulture);

    public Sphere() { }

    public Sphere(float x, float y, float z, float radius)
    {
        X = x; Y = y; Z = z; R = radius;
    }

    public float? GetX() => X;
    public float? GetY() => Y;
    public float? GetZ() => Z;
    public float? GetR() => R;
}
