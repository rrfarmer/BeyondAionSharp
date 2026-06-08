namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Secondary movement/CC effect kind (id is a byte; several constants share ids per Java).
/// Java parity: skillengine/model/SubEffectType.
/// </summary>
public enum SubEffectType
{
    NONE = 0,
    SPIN = 0,
    PULL = 2,
    PULL_NPC = 6,
    STUMBLE = 4,
    STAGGER = 4,
    OPENAERIAL = 4,
    SIMPLE_MOVE_BACK = 12,
}

public static class SubEffectTypeExtensions
{
    // Java parity: getId() — returns byte
    public static byte GetId(this SubEffectType type) => (byte)type;
}
