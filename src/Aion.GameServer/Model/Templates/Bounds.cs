using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates;

/// <summary>Java parity: model/templates/Bounds (Rolandas).</summary>
[XmlType("Bounds")]
public class Bounds : BoundRadius
{
    [XmlAttribute("altitude")] public float altitude;

    public float GetAltitude()
    {
        return altitude;
    }
}
