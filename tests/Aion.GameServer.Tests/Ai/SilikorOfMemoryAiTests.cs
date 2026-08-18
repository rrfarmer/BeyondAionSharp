using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="SilikorofMemoryAI"/> and the silikor guard set, translated from retail patterns
/// <c>ND2_WhG</c>, <c>ND2_WhG1</c>, <c>ND2_WhG2</c>, <c>ND2_WhG3</c> and <c>ND2_WhG4</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// What this replaces: a Java class that gave the boss three health phases, each calling two servants
/// that never expired. Retail gives him a thirty-second clock and one servant a time. Each of those
/// differences is a pin here, and so is the guard loop, which our server did not have at all.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SilikorOfMemoryAiTests
{
	/// <summary>Theobomos Lab.</summary>
	private const int Lab = 310110000;

	private const int Silikor = 214668;
	private const int Akaimum = 280973;
	private const int MeleeGuard = 280971;
	private const int CasterGuard = 280972;
	private const int MeleeMarker = 281034;
	private const int CasterMarker = 281035;

	private const int Fragment = 281053;
	private const int Essence = 281054;
	private const int CasterSummon = 281025;
	private const int CoreFx = 281032;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Lab).WithWorldSize(1024)
			.WithAi(typeof(SilikorofMemoryAI), typeof(SilikorGuardAI), typeof(SealedAkaimumAI),
				typeof(SilikorGuardMarkerAI), typeof(ServantNpcAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, List<Player>) Engaged(int npcId = Silikor, int raidSize = 3)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(npcId, 394f, 744f, 189f);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
			raid.Add(harness.SpawnPlayer(398f + i, 744f, 189f));

		harness.Engage(boss, raid[0]);
		for (int i = 0; i < raidSize; i++)
			for (int n = raidSize - i; n > 0; n--)
				BossAiHarness.Rehate(boss, raid[i]);

		return (harness, boss, raid);
	}

	private static void Advance(BossAiHarness harness, List<Player> raid, Npc boss, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	private static int Servants(BossAiHarness harness) =>
		Count(harness, Fragment) + Count(harness, Essence);

	/// <summary>
	/// <b>The servants are on a clock, not on health.</b> One arrives fifteen seconds in and one every
	/// thirty seconds after that, whatever his health is doing.
	/// </summary>
	[Fact]
	public void OneServantEveryThirtySeconds()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, raid, boss, 14);
		Assert.Equal(0, Servants(harness));

		Advance(harness, raid, boss, 2);
		Assert.Equal(1, Servants(harness));

		Advance(harness, raid, boss, 30);
		Assert.Equal(2, Servants(harness));

		Advance(harness, raid, boss, 30);
		Assert.Equal(3, Servants(harness));
	}

	/// <summary>
	/// <b>And crossing a health phase does nothing.</b> The Java class this replaces called two
	/// servants at fifty, twenty-five and ten percent; retail has no such rung.
	/// </summary>
	[Fact]
	public void CrossingTheOldPhasesCallsNobody()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		foreach (int percent in new[] { 49, 24, 9 })
		{
			BossAiHarness.SetExactPercent(boss, percent);
			Advance(harness, raid, boss, 3);
		}

		// Nine seconds of clock have passed, so the first call is still six away.
		Assert.Equal(0, Servants(harness));
	}

	/// <summary>Over a long fight both kinds turn up: retail's branch is a coin flip.</summary>
	[Fact]
	public void BothKindsOfServantAppear()
	{
		var (harness, boss, raid) = Engaged();
		// This pin is about the variety a rolled guard produces, so it hands back the production dice.
		// The harness forces rolled guards to pass by default, which makes counts exact and makes a
		// coin-toss branch look certain. A fixed seed would not help: a fresh npc per attempt with the
		// same seed makes every attempt identical.
		BossAiHarness.RandomRolls(boss);
		using BossAiHarness _h = harness;

		// Watched across the window rather than counted at the end of it. A servant lives three
		// minutes against a ten-minute window, so counting survivors sampled only the last handful of
		// calls and failed whenever those came up the same way -- measured at one solo run in forty,
		// which is what a coin flip over about six survivors predicts.
		Dictionary<int, BossAiHarness.Watched> seen = harness.WatchEach(
			20 * 30,
			() =>
			{
				foreach (Player member in raid)
				{
					BossAiHarness.Rehate(boss, member);
					BossAiHarness.KeepAlive(member);
				}
			},
			Fragment, Essence);

		Assert.True(seen[Fragment].Total > 0, "no fragment in twenty rolls");
		Assert.True(seen[Essence].Total > 0, "no essence in twenty rolls");
	}

	/// <summary>
	/// A servant keeps three minutes and then goes. The Java class left them standing for the whole
	/// fight, which is what turned a long pull into a crowd.
	/// </summary>
	[Fact]
	public void AServantKeepsThreeMinutes()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, raid, boss, 16);
		Npc first = Assert.Single(harness.LiveNpcs(),
			n => n.GetNpcId() == Fragment || n.GetNpcId() == Essence);

		// Followed rather than counted: the count also moves every time another is called, so it
		// says nothing about how long any one of them lasts.
		Advance(harness, raid, boss, 175);
		Assert.True(first.IsSpawned(), "it went before its three minutes were up");

		Advance(harness, raid, boss, 10);
		Assert.False(first.IsSpawned(), "it outlived its three minutes");
	}

	/// <summary>Dying leaves the core where retail puts it, and takes the servants with it.</summary>
	[Fact]
	public void DyingLeavesTheCore()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, raid, boss, 16);
		Assert.Equal(1, Servants(harness));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Servants(harness));
		Npc core = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == CoreFx);
		Assert.Equal(392.28f, core.GetX(), 1);
		Assert.Equal(754.11f, core.GetY(), 1);
	}

	/// <summary>
	/// <b>Every fifteen seconds he points, and both guards go.</b> This is what makes them part of his
	/// fight rather than a pull before it.
	/// </summary>
	[Fact]
	public void HePointsTheGuardsAtWhoeverHeIsFighting()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		// Thirty-six metres out: inside his fifty-metre order and well outside anything they would
		// find on their own. Standing them next to the raid passed whether or not he ever spoke.
		Npc melee = harness.Spawn(MeleeGuard, 394f, 780f, 189f);
		Npc caster = harness.Spawn(CasterGuard, 396f, 780f, 189f);
		BossAiHarness.MakeMutuallyKnown(boss, melee);
		BossAiHarness.MakeMutuallyKnown(boss, caster);

		Assert.Null(melee.GetTarget());

		Advance(harness, raid, boss, 16);

		Assert.Same(raid[0], melee.GetTarget());
		Assert.Same(raid[0], caster.GetTarget());
	}

	/// <summary>
	/// <b>A killed guard comes back.</b> Its marker shouts, and the akaimum stands a new one of the
	/// same kind on the same post — so clearing the hall means killing the akaimum.
	/// </summary>
	[Fact]
	public void TheAkaimumStandsAKilledGuardBackUp()
	{
		using BossAiHarness harness = NewHarness();
		Npc akaimum = harness.Spawn(Akaimum, 392f, 727f, 188f);
		Npc melee = harness.Spawn(MeleeGuard, 377f, 762f, 189f);
		BossAiHarness.MakeMutuallyKnown(akaimum, melee);

		melee.GetAi().OnGeneralEvent(AiEventType.Died);
		harness.LiveNpcs();

		Npc marker = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == MeleeMarker);
		BossAiHarness.MakeMutuallyKnown(akaimum, marker);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Npc raised = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == MeleeGuard && n != melee);
		Assert.Equal(377.24f, raised.GetX(), 1);
		Assert.Equal(762.6f, raised.GetY(), 1);
		Assert.Equal(0, Count(harness, CasterGuard));
	}

	/// <summary>
	/// <b>A guard that falls next to the akaimum is not stood back up.</b> Retail answers a marker within
	/// ten metres from a pair of branches above the re-placement pair, so first-match-wins decides it.
	/// </summary>
	/// <remarks>
	/// This is the half of the hall that was missing. The re-placement was ported and the exception to it
	/// was not, which made the akaimum strictly stronger than retail's: <b>every guard came back,
	/// wherever it died.</b> Pulling one into the akaimum's lap is a real tactic and it did nothing.
	/// </remarks>
	[Fact]
	public void AGuardThatFallsBesideTheAkaimumIsNotReplaced()
	{
		using BossAiHarness harness = NewHarness();
		Npc akaimum = harness.Spawn(Akaimum, 392f, 727f, 188f);

		// Five metres away, inside retail's ten.
		Npc melee = harness.Spawn(MeleeGuard, 396f, 730f, 188f);
		BossAiHarness.MakeMutuallyKnown(akaimum, melee);

		melee.GetAi().OnGeneralEvent(AiEventType.Died);
		Npc marker = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == MeleeMarker);
		BossAiHarness.MakeMutuallyKnown(akaimum, marker);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		// Counted as guards other than the one that died: the harness leaves a killed npc in the list,
		// so a plain count sees the corpse and reports a re-placement that never happened.
		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == MeleeGuard && n != melee);
	}

	/// <summary>
	/// <b>And a distant one still is.</b> The mirror, so the pin above is shown to be about distance
	/// rather than about the near branches swallowing every marker.
	/// </summary>
	[Fact]
	public void AGuardThatFallsAcrossTheHallStillComesBack()
	{
		using BossAiHarness harness = NewHarness();
		Npc akaimum = harness.Spawn(Akaimum, 392f, 727f, 188f);
		Npc melee = harness.Spawn(MeleeGuard, 377f, 762f, 189f);
		BossAiHarness.MakeMutuallyKnown(akaimum, melee);

		melee.GetAi().OnGeneralEvent(AiEventType.Died);
		Npc marker = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == MeleeMarker);
		BossAiHarness.MakeMutuallyKnown(akaimum, marker);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == MeleeGuard && n != melee);
	}

	/// <summary>
	/// <b>And 6621 sends it away, guards and all.</b> Retail's dismissal branch sits above both
	/// re-placement branches, so an akaimum that has just stood a guard back up still leaves with it.
	/// </summary>
	/// <remarks>
	/// <b>Nothing in this port sends 6621 yet.</b> Retail sends it from the silikor's <c>on_spelled</c>,
	/// behind a neutral-race caster and a consumed <i>world</i> flag that this akaimum sets when it
	/// re-places a guard — shared state between two npcs, which our per-npc flags cannot express. The
	/// branch is pinned anyway: it is unambiguous on its own, and it is the half that will be hard to
	/// verify later once the sender exists.
	/// </remarks>
	[Fact]
	public void SixSixTwoOneSendsTheAkaimumAndItsGuardsAway()
	{
		using BossAiHarness harness = NewHarness();
		Npc akaimum = harness.Spawn(Akaimum, 392f, 727f, 188f);
		Npc melee = harness.Spawn(MeleeGuard, 377f, 762f, 189f);
		BossAiHarness.MakeMutuallyKnown(akaimum, melee);

		// Kill and re-place a guard first, so the despawn has one of the akaimum's own spawns to take.
		melee.GetAi().OnGeneralEvent(AiEventType.Died);
		Npc marker = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == MeleeMarker);
		BossAiHarness.MakeMutuallyKnown(akaimum, marker);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.Equal(1, Count(harness, MeleeGuard));

		((Aion.GameServer.Ai.INpcMessageListener)akaimum.GetAi())
			.OnNpcMessage(akaimum, SealedAkaimumAI.Dismissed, null);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.DoesNotContain(harness.LiveNpcs(), n => n == akaimum);
		Assert.Equal(0, Count(harness, MeleeGuard));
	}

	/// <summary>
	/// And it reads which guard fell from the marker, not from the message: a caster's marker brings
	/// back a caster.
	/// </summary>
	[Fact]
	public void ACasterMarkerBringsBackACaster()
	{
		using BossAiHarness harness = NewHarness();
		Npc akaimum = harness.Spawn(Akaimum, 392f, 727f, 188f);
		Npc marker = harness.Spawn(CasterMarker, 407f, 762f, 189f);
		BossAiHarness.MakeMutuallyKnown(akaimum, marker);

		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Npc raised = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == CasterGuard);
		Assert.Equal(407.19f, raised.GetX(), 1);
		Assert.Equal(0, Count(harness, MeleeGuard));
	}

	/// <summary>
	/// <b>And a new guard turns the old one out.</b> Without it the akaimum's re-placement would stack
	/// guards on one post every time a corpse was left where it fell.
	/// </summary>
	[Fact]
	public void ANewGuardDismissesTheOldOne()
	{
		using BossAiHarness harness = NewHarness();
		Npc standing = harness.Spawn(MeleeGuard, 377f, 762f, 189f);
		Assert.True(standing.IsSpawned());

		Npc arriving = harness.Spawn(MeleeGuard, 379f, 762f, 189f);
		BossAiHarness.MakeMutuallyKnown(standing, arriving);
		arriving.GetAi().OnGeneralEvent(AiEventType.Spawned);

		Assert.False(standing.IsSpawned());
		Assert.True(arriving.IsSpawned());
	}

	/// <summary>
	/// <b>The caster guard drops something on whoever is hitting it.</b> A one-in-four roll every
	/// fifteen seconds while it is healthy, and the drop keeps thirty seconds.
	/// </summary>
	/// <remarks>
	/// Watched rather than counted at the end, for the fifth time in this suite: thirty seconds of life
	/// against a fifteen-second window means the field is usually empty when the clock stops, and a pin
	/// that counts then reads "nothing happened" however well the mechanic works.
	/// <para>
	/// <b>Sixty windows rather than twenty, because twenty was a flake and the arithmetic says so.</b>
	/// A one-in-four roll misses twenty times running with probability <c>0.75^20</c> — about one run in
	/// three hundred, which is invisible in a single run and inevitable across a suite that is run all
	/// day. It surfaced once, was clean over twenty repeats of the pin alone, and would have been filed
	/// as unexplained. <c>0.75^60</c> is one in a hundred million.
	/// </para>
	/// <para>
	/// <b>The sibling pin above needed no change</b>, and the same arithmetic is why: a coin flip over
	/// twenty rolls fails at <c>2 × 0.5^20</c>, which is already one in five hundred thousand. Window
	/// length is not the thing to standardise — the per-window odds are.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheCasterGuardDropsSummonsOnAttackers()
	{
		using BossAiHarness harness = NewHarness();
		Npc caster = harness.Spawn(CasterGuard, 407f, 762f, 189f);
		Player quarry = harness.SpawnPlayer(409f, 762f, 189f);
		harness.Engage(caster, quarry);

		BossAiHarness.Watched seen = harness.Watch(900, () =>
		{
			BossAiHarness.Rehate(caster, quarry);
			BossAiHarness.KeepAlive(quarry);
		}, CasterSummon);

		Assert.True(seen.Total > 0, "the caster guard dropped nothing in sixty windows");
	}

	/// <summary>
	/// Below thirty the melee guard peels to the second-most-hated player and keeps doing it every
	/// fifteen seconds.
	/// </summary>
	[Fact]
	public void BelowThirtyTheMeleeGuardPeels()
	{
		using BossAiHarness harness = NewHarness();
		Npc melee = harness.Spawn(MeleeGuard, 377f, 762f, 189f);
		var raid = new List<Player>();
		for (int i = 0; i < 3; i++)
			raid.Add(harness.SpawnPlayer(379f + i, 762f, 189f));

		harness.Engage(melee, raid[0]);
		for (int i = 0; i < 3; i++)
			for (int n = 3 - i; n > 0; n--)
				BossAiHarness.Rehate(melee, raid[i]);

		Assert.Same(raid[0], melee.GetTarget());

		// Stated rather than assumed: a peel pin is only a peel pin if the hate order says who is
		// first and who is second, and this ordering survived a mutation that took the tank instead.
		Assert.Same(raid[0], melee.GetAggroList().GetTarget(AggroTarget.MOST_HATED));
		Assert.Same(raid[1], melee.GetAggroList().GetTarget(AggroTarget.SECOND_MOST_HATED));

		BossAiHarness.SetExactPercent(melee, 20);
		for (int i = 0; i < 8; i++)
		{
			foreach (Player member in raid)
				BossAiHarness.Rehate(melee, member);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		Assert.Same(raid[1], melee.GetTarget());

		// And it keeps peeling: twenty-three seconds later, against whoever is second by then. The
		// order is turned over first, because peeling twice onto the same player proves nothing.
		for (int i = 0; i < 5; i++)
			BossAiHarness.Rehate(melee, raid[1]);

		Assert.Same(raid[1], melee.GetAggroList().GetTarget(AggroTarget.MOST_HATED));
		Assert.Same(raid[0], melee.GetAggroList().GetTarget(AggroTarget.SECOND_MOST_HATED));

		for (int i = 0; i < 25; i++)
		{
			foreach (Player member in raid)
				BossAiHarness.Rehate(melee, member);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		Assert.Same(raid[0], melee.GetTarget());

		// A third time, fifteen seconds after the second — which is the only thing that shows the
		// rung re-arms itself rather than firing once off the rung that opened it.
		for (int i = 0; i < 10; i++)
			BossAiHarness.Rehate(melee, raid[2]);

		Assert.Same(raid[1], melee.GetAggroList().GetTarget(AggroTarget.SECOND_MOST_HATED));

		for (int i = 0; i < 20; i++)
		{
			foreach (Player member in raid)
				BossAiHarness.Rehate(melee, member);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		Assert.Same(raid[1], melee.GetTarget());
	}
	/// <summary>
	/// <b>A second nearby marker arms the dismissal, once the akaimum has finished its walk.</b>
	/// </summary>
	/// <remarks>
	/// Retail's chain: the first close marker sets a per-npc flag and sends the akaimum walking, the walk
	/// ends and clears that flag, and only then can the second close marker match and set the <i>world</i>
	/// flag the silikor consumes to dismiss it. <b>This log recorded the dismissal as unreachable</b>
	/// because the middle step -- the arrival -- had no handler in the pattern engine.
	/// <para>
	/// Pinned on the flag rather than on the dismissal itself, because nothing in this port yet sends the
	/// silikor'''s 6621: what changed is that the world flag can now be reached at all.
	/// </para>
	/// </remarks>
	[Fact]
	public void ArrivingClearsTheFlagThatGatesTheSecondAnswer()
	{
		using BossAiHarness harness = NewHarness();
		Npc akaimum = harness.Spawn(Akaimum, 392f, 727f, 188f);
		Npc melee = harness.Spawn(MeleeGuard, 396f, 730f, 188f);
		BossAiHarness.MakeMutuallyKnown(akaimum, melee);

		melee.GetAi().OnGeneralEvent(AiEventType.Died);
		Npc marker = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == MeleeMarker);
		BossAiHarness.MakeMutuallyKnown(akaimum, marker);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		var pattern = (Aion.GameServer.Ai.Pattern.PatternAi)akaimum.GetAi();
		Assert.True(pattern.IsFlagSet(1), "the first close marker did not set the walking flag");

		akaimum.GetAi().OnGeneralEvent(AiEventType.MoveArrived);

		Assert.False(pattern.IsFlagSet(1), "arriving did not clear the walking flag");
	}
}
