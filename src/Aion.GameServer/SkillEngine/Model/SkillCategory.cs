namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Functional category of a skill.
/// Java parity: skillengine/model/SkillCategory (@XmlType("skillCategory") @XmlEnum).
/// </summary>
public enum SkillCategory
{
    NONE,
    CHAIN_SKILL,
    PHYSICAL_DEBUFF,
    HEAL,
    MENTAL_DEBUFF,
    REBIRTH,
    DISPELL,
    DEATHBLOW,
    DRAIN,
}
