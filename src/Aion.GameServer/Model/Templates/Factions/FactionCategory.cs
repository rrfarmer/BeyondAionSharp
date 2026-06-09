using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Factions;

/// <summary>Java parity: model/templates/factions/FactionCategory (vlog).</summary>
[XmlType("FactionCategory")]
public enum FactionCategory
{
    MENTOR,
    DAILY,
    COMBINESKILL,
    SHUGO
}
