using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Cp;

/// <summary>Java parity: model/templates/cp/CPType (Neon).</summary>
[XmlType(AnonymousType = true)]
public enum CPType
{
    CONQUEROR,
    PROTECTOR
}
