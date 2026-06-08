namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Dispel category of an effect.
/// Java parity: skillengine/model/DispelCategoryType.
/// </summary>
public enum DispelCategoryType
{
    NONE,
    ALL,
    BUFF,
    DEBUFF,
    DEBUFF_MENTAL,
    DEBUFF_PHYSICAL,
    EXTRA,
    NEVER,
    NPC_BUFF,
    NPC_DEBUFF_PHYSICAL,
    STUN,
}
