namespace Aion.GameServer.Ai.Pattern;

/// <summary>
/// Retail's <c>order_in_attacker_list</c>: which end of the hate list a capped multi-target spawn
/// keeps.
/// </summary>
/// <remarks>
/// Only meaningful together with a cap, and the cap is what makes it a mechanic. Across the 5.8 files
/// <c>ORDERI_RANDOM</c> is the common case by a wide margin — 254 uses against 65 descending and 5
/// ascending — so a runtime that assumed "top of the hate list" would be wrong far more often than
/// right.
/// </remarks>
public enum MultiTargetOrder
{
    /// <summary><c>ORDERI_DESCENDING</c> — the most-hated first.</summary>
    Descending,

    /// <summary><c>ORDERI_ASCENDING</c> — the least-hated first.</summary>
    Ascending,

    /// <summary><c>ORDERI_RANDOM</c> — any of them, which is what most patterns ask for.</summary>
    Random,
}
