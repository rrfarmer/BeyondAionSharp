using System.Xml.Serialization;

namespace Aion.GameServer.Model.GameObjects.State;

/// <summary>
/// Stealth detection thresholds for creatures.
/// Java parity: model/gameobjects/state/CreatureSeeState.
/// [XmlEnum] aliases bind the SCREAMING_CASE wire tokens (SEARCH1, ...) used by skill_templates.xml
/// (SearchEffect.state) to these PascalCase members, matching the Java enum-constant names on the wire.
/// </summary>
public enum CreatureSeeState
{
    [XmlEnum("NORMAL")] Normal = 0,      // Normal visibility
    [XmlEnum("SEARCH1")] Search1 = 1,    // See-Through: Hide I
    [XmlEnum("SEARCH2")] Search2 = 2,    // See-Through: Hide II
    [XmlEnum("SEARCH5")] Search5 = 5,    // NPC stealth
    [XmlEnum("SEARCH10")] Search10 = 10, // 3.0 NPC stealth
    [XmlEnum("SEARCH20")] Search20 = 20,
}

public static class CreatureSeeStateExtensions
{
    // Java parity: getId()
    public static int GetId(this CreatureSeeState state) => (int)state;
}
