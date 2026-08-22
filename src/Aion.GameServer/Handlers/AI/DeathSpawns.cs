using System.Collections.Generic;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// What a retail npc leaves behind when it dies: 678 patterns across 1,927 npcs, 5,253 actions.
/// </summary>
/// <remarks>
/// The encounters no rotation table can reach. <see cref="BattleCycles"/> reads <c>on_die</c> too, but
/// it is keyed on a battle-timer chain, and <b>179 of the encounters still missing an add have no
/// rotation at all</b> — there is nothing there to hang a death handler off. A death spawn is not part
/// of a rotation; it needs a table keyed on dying, which is this one.
/// <para>
/// Retail splits <c>on_die</c> from <c>on_killed_by_user</c> and <c>on_killed_by_npc</c>: the first
/// fires however the npc died, the other two ask who did it. This port has one slot plus
/// <c>When.KilledByPlayer</c> and <c>When.KilledByNpc</c>, which is exactly that distinction, so each
/// is stored as one branch with its guard.
/// <b>Killed-by-npc was worth the condition on its own</b>: variables written there gate 9,280 of
/// retail's placements, and nothing here could say it before.
/// </para>
/// <para>
/// <see cref="DeathSpawnAI"/>'s own nine hand-read npcs are excluded from generation and keep their
/// entries: they carry curated notes, and one of them encodes a judgement about a betrayer npc that is
/// worth not regenerating over.
/// </para>
/// <para>
/// <b>The 12,656 lines this class used to be are
/// <c>game-server/data/static_data/pattern_tables/death_spawns.xml</c> now.</b> They were branches
/// pasted into C# source by an emitter; they are read by <see cref="PatternTableLoader"/> at load time
/// instead, and this class is the accessor over the result.
/// </para>
/// </remarks>
internal static class DeathSpawns
{
    /// <summary>What this npc leaves behind, or empty for one that leaves nothing.</summary>
    internal static PatternBranch[] RungsFor(int npcId) => Table.For(npcId);

    /// <summary>Every npc this table drives.</summary>
    internal static IEnumerable<int> Npcs => Table.Npcs();

    private static PatternTableData Table => DataManager.DEATH_SPAWN_TABLE;
}
