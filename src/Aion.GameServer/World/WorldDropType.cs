using System.Xml.Serialization;

namespace Aion.GameServer.World;

/// <summary>
/// Drop table type for a world region.
/// Java parity: world/WorldDropType.
/// </summary>
/// <remarks>
/// The XML wire values are the Java SCREAMING_SNAKE constant names (e.g. wd_type="BALAUREA",
/// drop_type="NONE"); the [XmlEnum] mappings keep the C# PascalCase members while deserializing
/// 1:1 with JAXB. Purely additive — the enum ordinals/values are unchanged, so no consumer behaviour changes.
/// </remarks>
public enum WorldDropType
{
    // Java parity: ASMODAE
    [XmlEnum("ASMODAE")] Asmodae,
    // Java parity: ELYSEA
    [XmlEnum("ELYSEA")] Elysea,
    // Java parity: ABYSS
    [XmlEnum("ABYSS")] Abyss,
    // Java parity: ABYSS_INSTANCE
    [XmlEnum("ABYSS_INSTANCE")] AbyssInstance,
    // Java parity: PANESTERRA
    [XmlEnum("PANESTERRA")] Panesterra,
    // Java parity: ARENA_INSTANCE
    [XmlEnum("ARENA_INSTANCE")] ArenaInstance,
    // Java parity: BALAUREA
    [XmlEnum("BALAUREA")] Balaurea,
    // Java parity: BALAUREA_INSTANCE
    [XmlEnum("BALAUREA_INSTANCE")] BalaureInstance,
    // Java parity: BALAUREA_HIGH
    [XmlEnum("BALAUREA_HIGH")] BalaureHigh,
    // Java parity: BALAUREA_HIGH_INSTANCE
    [XmlEnum("BALAUREA_HIGH_INSTANCE")] BalaureHighInstance,
    // Java parity: DREDGION_INSTANCE
    [XmlEnum("DREDGION_INSTANCE")] DredgionInstance,
    // Java parity: BATTLEFIELD_INSTANCE
    [XmlEnum("BATTLEFIELD_INSTANCE")] BattlefieldInstance,
    // Java parity: STEEL_RAKE_INSTANCE
    [XmlEnum("STEEL_RAKE_INSTANCE")] SteelRakeInstance,
    // Java parity: OTHER_INSTANCES
    [XmlEnum("OTHER_INSTANCES")] OtherInstances,
    // Java parity: NONE (wire value "NONE").
    [XmlEnum("NONE")] None,

    // Java-name alias (call sites use the Java SCREAMING_SNAKE name; same value, additive).
    // Distinct [XmlEnum] string so it never collides with None on read (the XML only ever uses "NONE").
    [XmlEnum("__NONE_ALIAS__")] NONE = None,
}
