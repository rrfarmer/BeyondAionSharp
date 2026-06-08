namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// How a skill is activated.
/// Java parity: skillengine/model/ActivationAttribute (@XmlType("activationAttribute") @XmlEnum).
/// </summary>
public enum ActivationAttribute
{
    NONE,
    ACTIVE,
    PROVOKED,
    MAINTAIN,
    TOGGLE,
    PASSIVE,
    CHARGE,
}
