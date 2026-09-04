namespace Aion.GameServer.Model.Summons;

/// <summary>
/// Java parity: model/summons/UnsummonType (xTz). Cause of a summon release, defining how long the summon stays alive
/// and whether its master may take the order back.
/// </summary>
public enum UnsummonType
{
    LOGOUT,
    DISTANCE,
    COMMAND,
    SUMMON_DEATH,
    MASTER_DEATH,
    /// <summary>Live time ran out, instance script, ...</summary>
    UNSPECIFIED,
    /// <summary>The summon was ordered to cast a skill and vanish afterwards.</summary>
    SKILL_ORDER,
    PET_ORDER_UNSUMMON_EFFECT,
}

public static class UnsummonTypeExtensions
{
    // Java parity: per-constant (delayMillis, cancelableByMaster) ctor args.
    public static int GetDelayMillis(this UnsummonType type) => type switch
    {
        UnsummonType.COMMAND => 3000,
        UnsummonType.SKILL_ORDER => 3000,
        _ => 0,
    };

    public static bool IsInstant(this UnsummonType type) => type.GetDelayMillis() == 0;

    public static bool IsCancelableByMaster(this UnsummonType type) => type == UnsummonType.COMMAND;
}
