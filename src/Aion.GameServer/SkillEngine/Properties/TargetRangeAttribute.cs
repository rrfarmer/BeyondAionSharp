namespace Aion.GameServer.SkillEngine.Properties;

/// <summary>
/// Range/scope of a skill's targeting.
/// Java parity: skillengine/properties/TargetRangeAttribute (@XmlType("TargetRangeAttribute") @XmlEnum).
/// </summary>
public enum TargetRangeAttribute
{
    ONLYONE,
    PARTY,
    AREA,
    PARTY_WITHPET,
    POINT,
}
