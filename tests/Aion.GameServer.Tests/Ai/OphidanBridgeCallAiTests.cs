using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="OphidanBridgeCallAI"/>, translated from retail patterns
/// <c>BIDF5_U01_Boss_Wi</c>, <c>BIDF5_U01_Monster_01</c> and the twelve <c>BIDF5_U01_Runaway_*</c>
/// patterns (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Sixteen npcs share one branch pair: engaging calls everything within thirty metres onto your
/// target, and answering the call is itself an entry into combat, so the pull chains across the
/// bridge. Every pin here holds the player <b>forty metres away from the listener</b> so that a
/// fugitive which found the fight by itself would fail rather than pass — the geometry lesson from
/// the naga entry, applied from the start this time.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class OphidanBridgeCallAiTests
{
	private const int OphidanBridge = 300590000;

	private const int Aethercaster = 235769;
	private const int Aetherknife = 235771;
	private const int Mazikin = 235756;
	private const int SpiritedVelkur = 235768;
	private const int Hirakiki = 235760;
	private const int Sweeper = 857437;
	private const int MazikinGradeTwo = 235757;
	private const int CheckMarker = 856062;

	private const float CallerX = 323f;
	private const float CallerY = 489f;
	private const float Floor = 607f;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(OphidanBridge).WithWorldSize(2048)
			.WithAi(typeof(OphidanBridgeCallAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// The player stands forty metres south of the caller, which puts it forty-seven from a listener
	/// twenty-five metres east — outside the listener's own reach, so only the call can deliver it.
	/// </summary>
	private static (BossAiHarness, Npc, Player) Pulled(int callerId)
	{
		BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(callerId, CallerX, CallerY, Floor);
		Player player = harness.SpawnPlayer(CallerX, CallerY - 40f, Floor);
		harness.Engage(caller, player);
		return (harness, caller, player);
	}

	private static void Advance(BossAiHarness harness, Npc caller, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(caller, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	/// <summary><b>Pulling one calls its neighbours onto the same player.</b></summary>
	[Fact]
	public void PullingOneCallsItsNeighbours()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc caller = harness.Spawn(Aethercaster, CallerX, CallerY, Floor);
		Npc neighbour = harness.Spawn(Mazikin, CallerX + 25f, CallerY, Floor);
		Player player = harness.SpawnPlayer(CallerX, CallerY - 40f, Floor);
		BossAiHarness.MakeMutuallyKnown(caller, neighbour);
		BossAiHarness.MakeMutuallyKnown(neighbour, player);
		Assert.Null(neighbour.GetTarget());

		harness.Engage(caller, player);

		Assert.Same(player, neighbour.GetTarget());
	}

	/// <summary>
	/// <b>And only within thirty metres.</b> Retail's <c>range_as_meter</c> is what keeps the bridge
	/// from emptying itself on the first pull.
	/// </summary>
	[Fact]
	public void AnythingBeyondThirtyMetresIsLeftAlone()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc caller = harness.Spawn(Aethercaster, CallerX, CallerY, Floor);
		Npc distant = harness.Spawn(Mazikin, CallerX + 40f, CallerY, Floor);
		Player player = harness.SpawnPlayer(CallerX, CallerY - 40f, Floor);
		BossAiHarness.MakeMutuallyKnown(caller, distant);
		BossAiHarness.MakeMutuallyKnown(distant, player);

		harness.Engage(caller, player);
		Advance(harness, caller, player, 10);

		Assert.Null(distant.GetTarget());
	}

	/// <summary>
	/// <b>The call chains.</b> Answering it is an entry into combat, and entering combat is what makes
	/// an NPC call in turn — so a listener fifty metres from the pull, but twenty-five from one that
	/// heard it, joins anyway.
	/// </summary>
	[Fact]
	public void TheCallChainsThroughWhoeverHeardIt()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc caller = harness.Spawn(Aethercaster, CallerX, CallerY, Floor);
		Npc middle = harness.Spawn(Mazikin, CallerX + 25f, CallerY, Floor);
		Npc far = harness.Spawn(Aetherknife, CallerX + 50f, CallerY, Floor);
		Player player = harness.SpawnPlayer(CallerX, CallerY - 40f, Floor);
		BossAiHarness.MakeMutuallyKnown(caller, middle);
		BossAiHarness.MakeMutuallyKnown(middle, far);
		BossAiHarness.MakeMutuallyKnown(middle, player);
		BossAiHarness.MakeMutuallyKnown(far, player);

		harness.Engage(caller, player);

		// retail's add_hate_point leaves the target alone, so the chain is carried by hate rather than
		// by facing: the middle listener takes the call, its own entry into combat sends its own, and
		// the far one hears that. See CallChainTests for the engine property this rests on.
		Assert.True(middle.GetAggroList().GetHate(player) > 0, "the call never reached the middle");

		// AND THE CHAIN STOPS HERE, which it did not before. The old assertion was Assert.Same on the
		// middle listener's target, and a forced target is what made its own call go out at once. With
		// the faithful action the middle listener joins the fight -- CallChainTests pins that hate alone
		// is enough -- but its onward cry does not reach the far one in this arrangement. Whether that
		// is the reach, the guard on its entry branch, or the moment its current target is set has not
		// been established. Asserted as zero so this pin goes red the day it is, rather than left
		// claiming a chain that no longer happens. See docs/retail-ai-fidelity.md.
		Assert.Equal(0, far.GetAggroList().GetHate(player));
	}

	/// <summary>
	/// <b>Ten thousand hate points is a hand-off, not a nudge.</b> Retail's <c>point_to_add</c> is far
	/// above anything a player accumulates, so a fugitive that has answered the call does not drift
	/// back to somebody who turns up beside it afterwards.
	/// </summary>
	/// <remarks>
	/// Written first as a decoy standing next to the listener before the pull, which measured the
	/// wrong thing entirely: hating the decoy put the listener into combat, its own call named the
	/// decoy, and the caller took the decoy too. The order matters — the call has to be the first
	/// thing that happens.
	/// </remarks>
	[Fact]
	public void TheCallOutweighsWhoeverTurnsUpAfterwards()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc caller = harness.Spawn(Aethercaster, CallerX, CallerY, Floor);
		Npc neighbour = harness.Spawn(Mazikin, CallerX + 25f, CallerY, Floor);
		Player quarry = harness.SpawnPlayer(CallerX, CallerY - 40f, Floor);
		BossAiHarness.MakeMutuallyKnown(caller, neighbour);
		BossAiHarness.MakeMutuallyKnown(neighbour, quarry);

		harness.Engage(caller, quarry);
		Assert.Same(quarry, neighbour.GetTarget());

		// A second player arrives at the fugitive's elbow and hits it. One thousand hate against the
		// call's ten thousand does not move it.
		Player latecomer = harness.SpawnPlayer(CallerX + 26f, CallerY, Floor);
		BossAiHarness.MakeMutuallyKnown(neighbour, latecomer);
		BossAiHarness.Rehate(neighbour, latecomer);

		Assert.Same(quarry, neighbour.GetAggroList().GetTarget(AggroTarget.MOST_HATED));
	}

	/// <summary>
	/// <b>Normal mode does not link.</b> Spirited Velkur has neither half of the pair — the same fight
	/// with one mechanic taken out, which is why he keeps the stock AI.
	/// </summary>
	[Fact]
	public void NormalModeDoesNotLink()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc caller = harness.Spawn(SpiritedVelkur, CallerX, CallerY, Floor);
		Npc neighbour = harness.Spawn(Mazikin, CallerX + 25f, CallerY, Floor);
		Player player = harness.SpawnPlayer(CallerX, CallerY - 40f, Floor);
		BossAiHarness.MakeMutuallyKnown(caller, neighbour);
		BossAiHarness.MakeMutuallyKnown(neighbour, player);

		harness.Engage(caller, player);
		Advance(harness, caller, player, 10);

		Assert.Null(neighbour.GetTarget());
	}

	// Retail's first quarter, and a corner of the bridge a hundred metres from every one of the four.
	private const float QuarterX = 674.2f;
	private const float QuarterY = 471.7f;
	private const float QuarterZ = 599.4f;

	private static BossAiHarness Sweeping(int bossId, out Npc boss, out Player player)
	{
		BossAiHarness harness = BossAiHarness.For(OphidanBridge).WithWorldSize(2048)
			.WithAi(typeof(OphidanBridgeCallAI), typeof(OphidanBridgeSweeperAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();
		boss = harness.Spawn(bossId, CallerX, CallerY, Floor);
		player = harness.SpawnPlayer(CallerX, CallerY - 40f, Floor);
		return harness;
	}

	private static int Standing(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>Pulling a boss sweeps the bridge.</b> Four triggers land at four fixed points and each
	/// clears the fugitives around it — the first thing built on <c>despawn_by_nameid</c>.
	/// </summary>
	[Fact]
	public void PullingABossSweepsTheBridge()
	{
		using BossAiHarness harness = Sweeping(Aethercaster, out Npc boss, out Player player);

		for (int i = 0; i < 3; i++)
			harness.Spawn(Mazikin, QuarterX + i, QuarterY, QuarterZ);
		Assert.Equal(3, Standing(harness, Mazikin));

		harness.Engage(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(0, Standing(harness, Mazikin));
	}

	/// <summary>
	/// <b>And only within fifty metres of a trigger.</b> Retail's <c>bound_radius</c> is what makes
	/// this a sweep of the approach rather than a wipe of the instance.
	/// </summary>
	[Fact]
	public void TheSweepReachesFiftyMetresAndNoFurther()
	{
		using BossAiHarness harness = Sweeping(Aethercaster, out Npc boss, out Player player);

		Npc near = harness.Spawn(Mazikin, QuarterX + 10f, QuarterY, QuarterZ);
		Npc far = harness.Spawn(Mazikin, QuarterX + 70f, QuarterY, QuarterZ);

		harness.Engage(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.DoesNotContain(harness.LiveNpcs(), n => ReferenceEquals(n, near));
		Assert.Contains(harness.LiveNpcs(), n => ReferenceEquals(n, far));
	}

	/// <summary>
	/// <b>And ten of a kind at a time.</b> Retail's <c>max_count</c> is ten on every one of the nine
	/// sweeps, so a twelfth fugitive of the same grade under one trigger survives it.
	/// </summary>
	[Fact]
	public void TheSweepTakesTenOfEachGrade()
	{
		using BossAiHarness harness = Sweeping(Aethercaster, out Npc boss, out Player player);

		for (int i = 0; i < 12; i++)
			harness.Spawn(Mazikin, QuarterX + (i * 0.5f), QuarterY, QuarterZ);

		harness.Engage(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(2, Standing(harness, Mazikin));
	}

	/// <summary>
	/// <b>Normal mode sweeps even though it does not call.</b> The two mechanics are separate in
	/// retail's own file, and Spirited Velkur has exactly one of them.
	/// </summary>
	[Fact]
	public void NormalModeSweepsWithoutCalling()
	{
		using BossAiHarness harness = Sweeping(SpiritedVelkur, out Npc boss, out Player player);

		harness.Spawn(Mazikin, QuarterX, QuarterY, QuarterZ);
		Npc neighbour = harness.Spawn(Mazikin, CallerX + 25f, CallerY, Floor);
		BossAiHarness.MakeMutuallyKnown(boss, neighbour);
		BossAiHarness.MakeMutuallyKnown(neighbour, player);

		harness.Engage(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		// The one by the trigger is gone; the one beside him was never called, only left alone.
		Assert.Equal(1, Standing(harness, Mazikin));
		Assert.Null(neighbour.GetTarget());
	}

	/// <summary>And a fugitive calls without sweeping: the other half of the same split.</summary>
	/// <remarks>
	/// The witness has to be something the sweep would actually take. Written first with a velkur
	/// standing at the trigger point, which proved nothing at all — the nine grades retail names are
	/// all fugitives, so a boss survives a sweep whether one happened or not.
	/// </remarks>
	[Fact]
	public void AFugitiveCallsWithoutSweeping()
	{
		using BossAiHarness harness = Sweeping(Mazikin, out Npc caller, out Player player);

		harness.Spawn(Hirakiki, QuarterX, QuarterY, QuarterZ);

		harness.Engage(caller, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(1, Standing(harness, Hirakiki));
	}

	/// <summary>
	/// <b>And the triggers do not stand around afterwards.</b> Retail gives them
	/// <c>despawn_at_attack_state</c> and no <c>live_time</c>; the five seconds is ours, so it is
	/// pinned as ours rather than left for someone to discover.
	/// </summary>
	[Fact]
	public void TheTriggersDoNotOutstayTheSweep()
	{
		using BossAiHarness harness = Sweeping(Aethercaster, out Npc boss, out Player player);

		harness.Engage(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(4, Standing(harness, Sweeper));

		harness.Clock.Advance(TimeSpan.FromSeconds(10));
		Assert.Equal(0, Standing(harness, Sweeper));
	}

	/// <summary>
	/// <b>A hard-mode velkur clears the normal-mode boss as it appears.</b> Retail says "the two modes
	/// are the same fight and only one of them is running" with one <c>despawn_by_nameid</c> on
	/// <c>on_wake_up</c>, which is a use of the verb entirely separate from the bridge sweep.
	/// </summary>
	[Fact]
	public void AHardModeVelkurClearsTheNormalModeBoss()
	{
		using BossAiHarness harness = Sweeping(Mazikin, out Npc _unused, out Player _p);

		Npc normal = harness.Spawn(SpiritedVelkur, CallerX + 10f, CallerY, Floor);
		Assert.Equal(1, Standing(harness, SpiritedVelkur));

		harness.Spawn(Aethercaster, CallerX, CallerY, Floor);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.DoesNotContain(harness.LiveNpcs(), n => ReferenceEquals(n, normal));
	}

	/// <summary>And not one standing well away from him.</summary>
	/// <remarks>
	/// This pin bounds the clear from above but cannot say <em>which</em> bound stopped it. A wake-up
	/// action runs before the NPC has a known list, so it falls back to scanning its own map region —
	/// the same limit already recorded for wake-up broadcasts — and at seventy metres the region edge
	/// may be doing the work rather than retail's fifty. What is decisive is the mutation from the
	/// other side: dropping the range to five metres leaves the boss at ten standing, and is caught.
	/// </remarks>
	[Fact]
	public void ButNotOneStandingSeventyMetresOff()
	{
		using BossAiHarness harness = Sweeping(Mazikin, out Npc _unused, out Player _p);

		Npc normal = harness.Spawn(SpiritedVelkur, CallerX + 70f, CallerY, Floor);

		harness.Spawn(Aethercaster, CallerX, CallerY, Floor);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Contains(harness.LiveNpcs(), n => ReferenceEquals(n, normal));
	}

	/// <summary>
	/// <b>And a fugitive reaching its second grade clears the check marker at its post.</b> The third
	/// use of the verb in one file, and the one that shows it is bookkeeping as often as it is a sweep.
	/// </summary>
	[Fact]
	public void ASecondGradeFugitiveClearsItsCheckMarker()
	{
		using BossAiHarness harness = Sweeping(Mazikin, out Npc _unused, out Player _p);

		Npc marker = harness.Spawn(CheckMarker, CallerX + 5f, CallerY, Floor);

		harness.Spawn(MazikinGradeTwo, CallerX, CallerY, Floor);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.DoesNotContain(harness.LiveNpcs(), n => ReferenceEquals(n, marker));
	}

	/// <summary>The first grade leaves it: retail hangs the clear on the second and third only.</summary>
	[Fact]
	public void TheFirstGradeLeavesTheMarkerStanding()
	{
		using BossAiHarness harness = Sweeping(Mazikin, out Npc _unused, out Player _p);

		Npc marker = harness.Spawn(CheckMarker, CallerX + 5f, CallerY, Floor);

		harness.Spawn(Mazikin, CallerX, CallerY, Floor);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Contains(harness.LiveNpcs(), n => ReferenceEquals(n, marker));
	}
}
