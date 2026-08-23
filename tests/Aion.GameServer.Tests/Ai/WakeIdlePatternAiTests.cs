using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Full retail patterns on npcs that do not fight.
/// </summary>
/// <remarks>
/// 425 patterns across 462 npcs, all on <c>general</c>. <see cref="WakeVariables"/> took the ones whose
/// whole behaviour was an unguarded list of variable writes; these carry a guard, a timer, a message or
/// a spawn as well.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class WakeIdlePatternAiTests
{
	private const int Map = 300520000;

	/// <summary><c>IDArena_Solo_InviNPC_3</c>: broadcasts once and removes itself.</summary>
	/// <remarks>
	/// This was <c>IDDF3_BroadNPC_System</c> until that npc turned out to be placed by a battle
	/// rotation, and the table now gives up npcs another rotation owns for the length of a fight --
	/// their own wake pattern contradicts the encounter that placed them. This relay is placed by
	/// nothing, so its pattern is the only account of what it does.
	/// </remarks>
	private const int Relay = 205691;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Map).WithWorldSize(4096)
			.WithAi(typeof(PassivePatternAI), typeof(AggressivePatternAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI)).Build();

	/// <summary><b>A relay does its one job and goes.</b></summary>
	/// <remarks>
	/// The npc exists to carry a message and leave, and until this table ran its pattern it simply
	/// stood there forever. A death-spawn pin had been counting it as a lingering add, which was only
	/// ever countable because nothing ran the pattern.
	/// </remarks>
	[Fact]
	public void ARelayShoutsAndLeaves()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Relay, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.DoesNotContain(harness.LiveNpcs(), npc => npc.GetNpcId() == Relay);
	}

	/// <summary><b>And it does not fight, whatever its pattern says.</b></summary>
	/// <remarks>
	/// The invariant the whole table rests on. Every other pattern table feeds a class descending from
	/// <c>AggressiveNpcAI</c>; binding these npcs to one of those makes scenery attack on sight, which
	/// this project did to 67 wave controllers and did not notice for a dozen entries. The same npc is
	/// spawned under both classes so that only the class differs.
	/// </remarks>
	[Fact]
	public void APassivePatternNpcIgnoresAggroWhereAnAggressiveOneDoesNot()
	{
		using BossAiHarness harness = NewHarness();
		Npc passive = harness.SpawnWithAi(Hostile, "passive_pattern", 300f, 300f, 200f);
		// Far apart: an aggressive npc broadcasts its aggro to nearby friends, and a passive one
		// standing beside it joins through the support path, which is correct and would hide the point.
		Npc aggressive = harness.SpawnWithAi(Hostile, "aggressive", 900f, 900f, 200f);
		Player near = harness.SpawnPlayer(302f, 300f, 200f);
		Player far = harness.SpawnPlayer(902f, 900f, 200f);
		BossAiHarness.MakeMutuallyKnown(passive, near);
		BossAiHarness.MakeMutuallyKnown(aggressive, far);

		passive.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CreatureAggro, near);
		aggressive.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CreatureAggro, far);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.NotEmpty(aggressive.GetAggroList().Stream());
		Assert.Empty(passive.GetAggroList().Stream());
	}

	/// <summary><c>ND2_WhG3</c>: its wake rung sends it to step 0 of its route.</summary>
	private const int Waypointer = 214713;

	/// <summary>The tornado a Tiamat beacon lays: casts once, then removes itself.</summary>
	private const int Tornado = 283069;

	/// <summary>An npc hostile enough to players that the aggro event reaches its list.</summary>
	private const int Hostile = 217307;

	/// <summary><b>The aggressive half of the same table keeps its aggression.</b></summary>
	/// <remarks>
	/// 267 npcs run these patterns through <c>AggressivePatternAI</c> rather than the passive class,
	/// because retail keeps them on <c>aggressive</c>. 172 of them were write-only until it existed --
	/// their patterns said more and nothing could run it, since the only pattern class that would take
	/// them was passive and would have removed their aggression.
	/// <para>
	/// The same npc under both classes, as above, so the only difference is the class.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheAggressiveHalfOfTheTableStillFights()
	{
		using BossAiHarness harness = BossAiHarness.For(Map).WithWorldSize(4096)
			.WithAi(typeof(AggressivePatternAI), typeof(PassivePatternAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI)).Build();
		Npc fighter = harness.SpawnWithAi(Hostile, "aggressive_pattern", 300f, 300f, 200f);
		Npc passive = harness.SpawnWithAi(Hostile, "passive_pattern", 900f, 900f, 200f);
		Player near = harness.SpawnPlayer(302f, 300f, 200f);
		Player far = harness.SpawnPlayer(902f, 900f, 200f);
		BossAiHarness.MakeMutuallyKnown(fighter, near);
		BossAiHarness.MakeMutuallyKnown(passive, far);

		fighter.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CreatureAggro, near);
		passive.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CreatureAggro, far);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.NotEmpty(fighter.GetAggroList().Stream());
		Assert.Empty(passive.GetAggroList().Stream());
	}
	/// <summary><b>The tornado a Tiamat beacon lays casts, and then goes.</b></summary>
	/// <remarks>
	/// The mechanic this whole thread exists for. <c>NLycan_SELC_S2</c> is a hazard: retail has it use a
	/// skill and despawn itself, and the skill is the damage. Bound to inert <c>aggressive</c> it did
	/// neither -- it stood on the ground and the beacon delivered nothing -- and a pin counting it
	/// standing there passed for years.
	/// <para>
	/// <b>Both halves, because each alone has been claimed falsely.</b> A previous entry reported this
	/// npc casting and leaving when the table had in fact given it up, and nothing here would have
	/// noticed: the beacon's own pins had been moved onto its spawn count by then, so the hazard could
	/// be dead without a single test objecting.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheTornadoCastsAndLeaves()
	{
		using BossAiHarness harness = NewHarness();
		Npc tornado = harness.Spawn(Tornado, 300f, 300f, 200f);

		Assert.Equal(1, ((PatternAi)tornado.GetAi()).ImmediateCastCount);

		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.DoesNotContain(harness.LiveNpcs(), npc => npc.GetNpcId() == Tornado);
	}
	/// <summary><b>Do-nothing branches are carried, and they sit above other branches.</b></summary>
	/// <remarks>
	/// Retail writes <c>do_nothing</c> 3,445 times, and it is not padding. Branch lists are
	/// first-match-wins -- <c>PatternAi</c> runs the first branch whose guards hold and returns -- so a
	/// matching do-nothing branch means "this case, and none of the ones below it". Dropping it
	/// promotes whatever came next, which is the opposite instruction.
	/// <para>
	/// The count alone would not show that: a do-nothing branch at the bottom of a list changes
	/// nothing, and one at the top changes everything. This asserts that the table actually contains
	/// the second kind, which is what makes carrying them worth the row.
	/// </para>
	/// </remarks>
	[Fact]
	public void DoNothingBranchesAreCarriedAndBlockTheOnesBelow()
	{
		string path = System.IO.Path.Combine(BossAiHarness.RepoRoot(),
			"tools", "client-extract", "out", "wake_idle_patterns.tsv");
		string[] lines = System.IO.File.ReadAllLines(path);
		string[] header = lines[0].Split('	');
		int npcAt = Array.IndexOf(header, "npc");
		int handlerAt = Array.IndexOf(header, "handler");
		int branchAt = Array.IndexOf(header, "branch");
		int kindAt = Array.IndexOf(header, "kind");

		var byRung = new Dictionary<(string, string, string), List<string>>();
		var lastBranch = new Dictionary<(string, string), int>();
		foreach (string line in lines.Skip(1))
		{
			string[] f = line.Split('	');
			var rung = (f[npcAt], f[handlerAt], f[branchAt]);
			if (!byRung.TryGetValue(rung, out List<string>? kinds))
				byRung[rung] = kinds = new List<string>();
			kinds.Add(f[kindAt]);

			var owner = (f[npcAt], f[handlerAt]);
			int branch = int.Parse(f[branchAt]);
			lastBranch[owner] = Math.Max(lastBranch.TryGetValue(owner, out int seen) ? seen : 0, branch);
		}

		int carried = byRung.Values.Count(kinds => kinds.All(k => k == "nothing"));
		int blocking = byRung.Count(rung => rung.Value.All(k => k == "nothing")
			&& int.Parse(rung.Key.Item3) < lastBranch[(rung.Key.Item1, rung.Key.Item2)]);

		// Rungs, not actions: 89 do-nothing actions collapse into 68 branches, some rungs carrying the
		// element more than once. The branch is the unit that blocks, so the branch is what is counted.
		Assert.Equal(68, carried);
		Assert.True(blocking > 0, "no do-nothing branch sits above another, so carrying them buys nothing");
	}
	/// <summary><b>A waypoint rung starts the npc on its own route.</b></summary>
	/// <remarks>
	/// Retail's <c>goto_waypoint</c> carries an index into the npc's route rather than a path name, and
	/// 929 of its 1,112 uses ask for step 0. <c>SetWalkerTemplate</c> already took a starting step, so
	/// the helper is the ordinary route walk with that argument filled in.
	/// <para>
	/// An npc that is not a path walker does nothing, which is the case pinned here: retail's patterns
	/// are shared across npcs and not every one of them has the route the pattern assumes, so the rung
	/// has to be safe on the ones that do not rather than throwing or walking somewhere arbitrary.
	/// </para>
	/// </remarks>
	[Fact]
	public void AWaypointRungIsHarmlessOnAnNpcWithNoRoute()
	{
		using BossAiHarness harness = NewHarness();
		Npc walker = harness.Spawn(Waypointer, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.False(walker.GetAi().IsInState(Aion.GameServer.Ai.AIState.WALKING),
			"an npc with no route was sent walking anyway");
		Assert.Contains(harness.LiveNpcs(), npc => npc.GetNpcId() == Waypointer);
	}
}
