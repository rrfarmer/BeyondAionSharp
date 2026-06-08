using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Zone;

/// <summary>Java parity: model/templates/zone/Semisphere — extends Sphere for half-sphere geometry.</summary>
[XmlType("Semisphere")]
public class Semisphere : Sphere
{
    public Semisphere() { }

    public Semisphere(float x, float y, float z, float radius)
        : base(x, y, z, radius) { }
}
