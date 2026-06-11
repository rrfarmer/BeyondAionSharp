using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Controllers.Effects;

/// <summary>
/// Abnormal-state categories that accumulate diminishing-returns resistance.
/// Java parity: controllers/effect/CumulativeResistType.
/// </summary>
public enum CumulativeResistType
{
    FEAR,
    PARALYZE,
    SLEEP,
}

public static class CumulativeResistTypeExtensions
{
    // Java parity: static get(StatEnum) — maps a resistance stat to its cumulative type (null if none).
    public static CumulativeResistType? Get(StatEnum stat) => stat switch
    {
        StatEnum.FEAR_RESISTANCE => CumulativeResistType.FEAR,
        StatEnum.PARALYZE_RESISTANCE => CumulativeResistType.PARALYZE,
        StatEnum.SLEEP_RESISTANCE => CumulativeResistType.SLEEP,
        _ => null,
    };
}
