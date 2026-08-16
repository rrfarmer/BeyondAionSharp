namespace Aion.GameServer.Controllers.Attack;

/// <summary>Aggro target selection mode. Java parity: controllers/attack/AggroTarget.</summary>
public enum AggroTarget
{
    RANDOM,
    RANDOM_EXCEPT_CURRENT_TARGET,
    MOST_HATED,
    SECOND_MOST_HATED,
    THIRD_MOST_HATED,

    /// <summary>Retail's <c>ATTACKERI_HAS_LOWEST_HP</c> and <c>ATTACKERI_HAS_MOST_HP</c>.</summary>
    /// <remarks>
    /// Added for the retail patterns rather than for Java, which names neither. They are not rare
    /// there: across the 5.8 files the attacker indicators run RANDOM_ONE 3,492, SECOND_HATING 725,
    /// RANDOM_EXCEPT_CURRENT 399, <b>HAS_LOWEST_HP 356</b>, THIRD_HATING 281 and HAS_MOST_HP 58 — so
    /// picking on whoever is closest to dying is the fourth most common thing a boss does with a
    /// target, and there was no way to say it. See docs/retail-ai-fidelity.md.
    /// <para>
    /// Ranked by health <em>fraction</em> rather than by absolute HP, so a boss reaching for the one
    /// most nearly dead is not fooled by a class with a smaller pool.
    /// </para>
    /// </remarks>
    LOWEST_HP,
    MOST_HP,
}
