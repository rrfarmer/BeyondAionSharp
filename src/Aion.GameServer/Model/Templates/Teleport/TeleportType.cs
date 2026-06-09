using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Teleport;

/// <summary>Java parity: model/templates/teleport/TeleportType (ATracer).</summary>
[XmlType("type")]
public enum TeleportType
{
    REGULAR,
    FLIGHT
}
