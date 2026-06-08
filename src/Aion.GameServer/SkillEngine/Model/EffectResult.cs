namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Result of attempting to apply an effect.
/// Java parity: skillengine/model/EffectResult.
/// </summary>
public enum EffectResult
{
    NORMAL = 0,
    ABSORBED = 1,
    CONFLICT = 2,
    DODGE = 3,
    RESIST = 4,
    IMMUNE = 5, // TODO: IMPLEMENT
    CANCELED_DUE_TO_TOO_MANY_EFFECTS = 6,
}

public static class EffectResultExtensions
{
    // Java parity: getId()
    public static int GetId(this EffectResult result) => (int)result;
}
