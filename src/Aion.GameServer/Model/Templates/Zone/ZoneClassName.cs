using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Zone;

/// <summary>
/// Zone class names for zone template XML.
/// Java parity: model/templates/zone/ZoneClassName.
/// </summary>
[XmlType("ZoneClassName")]
public enum ZoneClassName
{
    // Java parity: DUMMY
    Dummy,
    // Java parity: SUB
    Sub,
    // Java parity: FLY
    Fly,
    // Java parity: NO_FLY
    NoFly,
    // Java parity: ARTIFACT
    Artifact,
    // Java parity: FORT
    Fort,
    // Java parity: LIMIT
    Limit,
    // Java parity: ITEM_USE
    ItemUse,
    // Java parity: PVP
    Pvp,
    // Java parity: DUEL
    Duel,
    // Java parity: HOUSE
    House,
    // Java parity: WEATHER
    Weather,
    // Java parity: DOMINION
    Dominion,
}
