namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Outcome status flags for a spell/attack (block, parry, dodge, ...).
/// Java parity: skillengine/model/SpellStatus.
/// </summary>
public enum SpellStatus
{
    NONE = 0,
    STUMBLE = 1,
    STAGGER = 2, // knockback
    OPENAERIAL = 4,
    CLOSEAERIAL = 8,
    SPIN = 16,
    BLOCK = 32,
    PARRY = 64,
    DODGE = 128,
    DODGE2 = -128, // TEMP
    RESIST = 256,
}

public static class SpellStatusExtensions
{
    // Java parity: getId()
    public static int GetId(this SpellStatus status) => (int)status;
}
