using System.Collections.Generic;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Retail's battle rotations and the handlers that arm them: 3,938 patterns across 30,166 npcs,
/// 318,584 actions.
/// </summary>
/// <remarks>
/// <b>Skill indices are resolved per npc.</b> Retail names a skill by its place in the npc's own
/// ordered list, so a rotation is only taken when the owning npc's list answers every index it uses;
/// a boss here never casts a skill picked by position out of the wrong list.
/// <para>
/// <b>Spawn groups are real here.</b> The wake and idle table spawns into <c>Untracked</c> because
/// nothing there refers back to what it placed; a rotation that despawns its adds needs the group it
/// spawned into.
/// </para>
/// <para>
/// <b>The 133,959 lines this class used to be are
/// <c>game-server/data/static_data/pattern_tables/battle_cycles.xml</c> now</b> — it was the largest
/// file in the port. Branch lists are shared: many npcs run the same retail pattern, so each distinct
/// list is stored once and each npc points at one. Two npcs share a list only when the text written
/// for them was identical, which cannot merge npcs differing in a way nobody thought to check.
/// </para>
/// </remarks>
internal static class BattleCycles
{
    /// <summary>The rotation itself, chosen by which timer fired.</summary>
    internal static PatternBranch[] CycleRungsFor(int npcId) => Table.For(npcId, "cycle");

    /// <summary>Arms the chain when the fight starts.</summary>
    internal static PatternBranch[] ArmingRungsFor(int npcId) => Table.For(npcId, "on_enter_attack_state");

    /// <summary>Arms the chain when another npc calls.</summary>
    internal static PatternBranch[] MessageRungsFor(int npcId) => Table.For(npcId, "on_message");

    /// <summary>Arms the chain on being hit.</summary>
    internal static PatternBranch[] AttackedRungsFor(int npcId) => Table.For(npcId, "on_attacked");

    /// <summary>Arms the chain on being spelled.</summary>
    internal static PatternBranch[] SpelledRungsFor(int npcId) => Table.For(npcId, "on_spelled");

    /// <summary>Arms the chain on waking.</summary>
    internal static PatternBranch[] WakeRungsFor(int npcId) => Table.For(npcId, "on_wake_up");

    /// <summary>Arms the chain on seeing an npc.</summary>
    internal static PatternBranch[] SeeNpcRungsFor(int npcId) => Table.For(npcId, "on_see_npc");

    /// <summary>Arms the chain on seeing a player.</summary>
    internal static PatternBranch[] SeeUserRungsFor(int npcId) => Table.For(npcId, "on_see_user");

    /// <summary>Rungs for a player moving inside the npc's sight, which repeats as they move.</summary>
    internal static PatternBranch[] SeeUserMoveRungsFor(int npcId) => Table.For(npcId, "on_see_user_move");

    /// <summary>What the encounter leaves behind when it dies.</summary>
    internal static PatternBranch[] DeathRungsFor(int npcId) => Table.For(npcId, "on_die");

    /// <summary>What it does when the fight ends without dying.</summary>
    internal static PatternBranch[] LeaveFightRungsFor(int npcId) => Table.For(npcId, "on_leave_attack_state");

    /// <summary>What the npc does the moment it goes back to idle.</summary>
    internal static PatternBranch[] EnterIdleRungsFor(int npcId) => Table.For(npcId, "on_enter_idle_state");

    /// <summary>What it does when a player talks to it.</summary>
    internal static PatternBranch[] TalkRungsFor(int npcId) => Table.For(npcId, "on_talked_by_user");

    /// <summary>What it does when something it counts as a friend is attacked.</summary>
    internal static PatternBranch[] FriendAttackedRungsFor(int npcId) => Table.For(npcId, "on_see_friend_attacked");

    /// <summary>What it does on reaching a point on its route.</summary>
    internal static PatternBranch[] ArrivedRungsFor(int npcId) => Table.For(npcId, "on_arrived_at_waypoint");

    /// <summary>What it leaves behind when it is removed without dying.</summary>
    internal static PatternBranch[] DespawnRungsFor(int npcId) => Table.For(npcId, "on_despawn");

    /// <summary>What it does when a friend is spelled.</summary>
    internal static PatternBranch[] FriendSpelledRungsFor(int npcId) => Table.For(npcId, "on_friend_spelled");

    /// <summary>What it does when it stops running away.</summary>
    internal static PatternBranch[] StopFleeingRungsFor(int npcId) => Table.For(npcId, "on_stop_to_flee");

    /// <summary>What it does when it sees a friend killed by a player.</summary>
    internal static PatternBranch[] FriendKilledRungsFor(int npcId) => Table.For(npcId, "on_see_friend_killed_by_user");

    /// <summary>What it does as it starts heading home.</summary>
    internal static PatternBranch[] EnterReturningRungsFor(int npcId) => Table.For(npcId, "on_enter_return_sp");

    /// <summary>What it does once it has arrived home.</summary>
    internal static PatternBranch[] LeaveReturningRungsFor(int npcId) => Table.For(npcId, "on_leave_return_sp");

    /// <summary>
    /// Every npc this table drives, across every handler.
    /// </summary>
    /// <remarks>
    /// The union rather than the rotation's owners alone, so an npc whose only rows are in a signal
    /// handler is still checked for a binding.
    /// </remarks>
    internal static IEnumerable<int> Npcs => Table.AllNpcs;

    private static PatternTableData Table => DataManager.BATTLE_CYCLE_TABLE;
}
