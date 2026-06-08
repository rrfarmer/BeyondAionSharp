using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Zone;

/// <summary>Java parity: model/templates/zone/AreaType.</summary>
[XmlType("AreaType")]
public enum AreaType
{
    [XmlEnum("POLYGON")]   Polygon,
    [XmlEnum("CYLINDER")]  Cylinder,
    [XmlEnum("SPHERE")]    Sphere,
    [XmlEnum("SEMISPHERE")] Semisphere,
}
