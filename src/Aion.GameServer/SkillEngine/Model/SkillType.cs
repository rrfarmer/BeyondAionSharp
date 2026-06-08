namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Physical vs magical skill classification.
/// Java parity: skillengine/model/SkillType (@XmlType("skillType") @XmlEnum).
/// </summary>
public enum SkillType
{
    NONE,
    PHYSICAL,
    MAGICAL,
    ALL,
}
