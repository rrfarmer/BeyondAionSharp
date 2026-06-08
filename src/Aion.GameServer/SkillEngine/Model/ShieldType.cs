namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Bit-flag classification of shield/absorb effects.
/// Java parity: skillengine/model/ShieldType.
/// </summary>
public enum ShieldType
{
    CONVERT = 0,
    REFLECTOR = 1 << 0,      // 1
    NORMAL = 1 << 1,         // 2
    UNK = 1 << 2,            // 4
    PROTECT = 1 << 3,        // 8
    MPSHIELD = 1 << 4,       // 16
    SKILL_REFLECTOR = 1 << 5, // 32
}

public static class ShieldTypeExtensions
{
    // Java parity: getId()
    public static int GetId(this ShieldType type) => (int)type;
}
