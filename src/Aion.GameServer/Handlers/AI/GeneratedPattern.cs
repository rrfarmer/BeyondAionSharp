using Aion.GameServer.Ai.Pattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Every generated pattern table an npc has rows in, composed into one <see cref="AiPattern"/>.
/// </summary>
/// <remarks>
/// <b>An npc gets one <c>ai=</c> binding, and therefore one class.</b> The generated tables, however,
/// are family-shaped: <see cref="BattleCycles"/> reads the combat handlers, <see cref="WakeIdlePatterns"/>
/// reads waking and idling, <see cref="DeathSpawns"/> reads the death family. Each class filled only
/// its own table's slots, so a pattern's handlers were read only by whichever table won the npc, and
/// everything the other tables knew about that npc was dropped on the floor.
/// <para>
/// Measured before this existed: 319 patterns lost <c>on_message</c>, 290 lost
/// <c>on_enter_attack_state</c>, 246 lost <c>on_battle_timer</c>, 242 lost <c>on_killed_by_user</c>.
/// The sharpest case was 533 npcs holding a complete retail combat rotation, already parseable, in a
/// pattern whose owning table could not read it.
/// </para>
/// <para>
/// <b>This is composition, not rebinding.</b> The tempting fix is to widen the battle table's accepted
/// classes and let it take the npcs the wake table holds -- but <see cref="BattleCycleAI"/> descends
/// from <c>AggressiveNpcAI</c>, so that hands an aggro radius to passive npcs, and it drops their wake
/// rungs in exchange for their rotation. Trading one set of dropped handlers for another is not
/// progress. Instead every npc keeps the class it had -- which already encodes whether retail fights --
/// and each class fills its slots from every table that has rows for that npc.
/// </para>
/// <para>
/// <b>Two slots can be claimed twice, and both cases were checked rather than assumed.</b> The battle
/// and death tables both read <c>on_die</c>; the battle, wake and idle tables all read
/// <c>on_wake_up</c>. Running the extractors widened, over the whole dump: <c>on_die</c> is present in
/// both tables for 290 npcs and the rungs are <b>identical for all 290</b>, and <c>on_wake_up</c> is
/// never produced by two tables for the same npc. So first-non-empty cannot silently pick a worse
/// reading of the same handler. The death table is preferred for <c>on_die</c> anyway, because it also
/// carries <c>on_killed_by_npc</c> and <c>on_killed_by_user</c> with their killer guard, and is a
/// superset wherever it has anything at all.
/// </para>
/// <para>
/// Slots are never concatenated. Retail branch lists are first-match-wins, so appending one table's
/// reading of a handler to another's would let a branch fire after a branch that retail says ends the
/// evaluation.
/// </para>
/// </remarks>
internal static class GeneratedPattern
{
	/// <summary>The first of <paramref name="sources"/> that has any rungs, or none.</summary>
	private static PatternBranch[] First(params PatternBranch[][] sources)
	{
		foreach (PatternBranch[] source in sources)
		{
			if (source.Length != 0)
			{
				return source;
			}
		}

		return [];
	}

	/// <summary>Every slot any generated table can fill for <paramref name="npcId"/>.</summary>
	internal static AiPattern For(int npcId)
		=> new AiPattern
		{
			// Combat: only the battle table reads these.
			OnEnterAttack = AiPattern.Of(BattleCycles.ArmingRungsFor(npcId)),
			OnMessage = AiPattern.Of(BattleCycles.MessageRungsFor(npcId)),
			OnAttacked = AiPattern.Of(BattleCycles.AttackedRungsFor(npcId)),
			OnSpelled = AiPattern.Of(BattleCycles.SpelledRungsFor(npcId)),
			OnSeeNpc = AiPattern.Of(BattleCycles.SeeNpcRungsFor(npcId)),
			OnSeeUser = AiPattern.Of(BattleCycles.SeeUserRungsFor(npcId)),
			OnLeaveAttack = AiPattern.Of(BattleCycles.LeaveFightRungsFor(npcId)),
			OnBattleTimer = AiPattern.Of(BattleCycles.CycleRungsFor(npcId)),

			// Waking and idling: three tables can read these, and never do so for the same npc.
			OnWakeUp = AiPattern.Of(First(
				BattleCycles.WakeRungsFor(npcId),
				WakeIdlePatterns.OnWakeUpFor(npcId),
				IdleCycles.WakeRungFor(npcId))),
			OnIdleTimer = AiPattern.Of(First(
				WakeIdlePatterns.OnIdleTimerFor(npcId),
				IdleCycles.CycleRungsFor(npcId))),

			// Dying: the death table first, because it is a superset wherever it has rows.
			OnDie = AiPattern.Of(First(
				DeathSpawns.RungsFor(npcId),
				BattleCycles.DeathRungsFor(npcId))),
		};
}
