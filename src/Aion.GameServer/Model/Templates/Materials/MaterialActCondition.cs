using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Materials;

/// <summary>Java parity: model/templates/materials/MaterialActCondition (Rolandas).</summary>
[XmlType]
public enum MaterialActCondition
{
    SUNNY,
    NIGHT
}
