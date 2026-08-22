using System.Collections.Generic;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// What a retail npc does the moment it appears, and each time its own idle timer fires:
/// 1,555 patterns across 3,928 npcs, 9,913 actions.
/// </summary>
/// <remarks>
/// <see cref="WakeVariables"/> takes the ones whose whole behaviour is an unguarded list of variable
/// writes; everything with a guard, a timer, a message or a spawn beside it is here.
/// <para>
/// <b>This is pattern data and drives two classes.</b> <c>PassivePatternAI</c> runs it for the npcs
/// retail keeps on <c>general</c> and <c>AggressivePatternAI</c> for the ones it keeps on
/// <c>aggressive</c>; the binder picks by what the npc already was. The table used to be called
/// <c>PassivePatterns</c>, which stopped being true the moment it held an npc that fights.
/// </para>
/// <para>
/// <b>The vocabulary was never the obstacle — the base class was.</b> <c>PatternAi</c> extends
/// <c>AggressiveNpcAI</c>, and this project once bound 67 wave controllers to a class that descends
/// from it and made them attack on sight for a dozen entries without a single pin noticing.
/// <see cref="PassivePatternAi"/> is what makes this table safe.
/// </para>
/// <para>
/// <b>The 23,898 lines this class used to be are
/// <c>game-server/data/static_data/pattern_tables/wake_idle_patterns.xml</c> now.</b> An npc may
/// appear under both handlers, so each element names its own.
/// </para>
/// </remarks>
internal static class WakeIdlePatterns
{
	/// <summary>Retail's <c>SPAWN_ID_NONE</c>: these rungs do not track what they placed.</summary>
	internal const int Untracked = 0;

	/// <summary>What it does the moment it appears.</summary>
	internal static PatternBranch[] OnWakeUpFor(int npcId) => Table.For(npcId, "on_wake_up");

	/// <summary>What it does each time its own timer fires.</summary>
	internal static PatternBranch[] OnIdleTimerFor(int npcId) => Table.For(npcId, "on_idle_timer");

	/// <summary>Every npc this table drives.</summary>
	internal static IEnumerable<int> Npcs => Table.AllNpcs;

	private static PatternTableData Table => DataManager.WAKE_IDLE_TABLE;
}
