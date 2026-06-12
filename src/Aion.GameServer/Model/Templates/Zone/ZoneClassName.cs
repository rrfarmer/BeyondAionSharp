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
    DUMMY,
    // Java parity: SUB
    SUB,
    // Java parity: FLY
    FLY,
    // Java parity: NO_FLY
    NO_FLY,
    // Java parity: ARTIFACT
    ARTIFACT,
    // Java parity: FORT
    FORT,
    // Java parity: LIMIT
    LIMIT,
    // Java parity: ITEM_USE
    ITEM_USE,
    // Java parity: PVP
    PVP,
    // Java parity: DUEL
    DUEL,
    // Java parity: HOUSE
    HOUSE,
    // Java parity: WEATHER
    WEATHER,
    // Java parity: DOMINION
    DOMINION,
}
