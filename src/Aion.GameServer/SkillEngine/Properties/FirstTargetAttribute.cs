namespace Aion.GameServer.SkillEngine.Properties;

/// <summary>
/// Initial target selection rule for a skill.
/// Java parity: skillengine/properties/FirstTargetAttribute (@XmlType("FirstTargetAttribute") @XmlEnum).
/// </summary>
public enum FirstTargetAttribute
{
    TARGETORME,
    ME,
    MYPET,
    MYMASTER,
    TARGET,
    PASSIVE,
    TARGET_MYPARTY_NONVISIBLE,
    POINT,
}
