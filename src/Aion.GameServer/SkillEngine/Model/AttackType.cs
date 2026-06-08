namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Attack category a modifier/proc applies to.
/// Java parity: skillengine/model/AttackType (@XmlType("attackType") @XmlEnum).
/// </summary>
public enum AttackType
{
    EVERYHIT,
    PHYSICAL_SKILL,
    MAGICAL_SKILL,
    ALL_SKILL,
}
