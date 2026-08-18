using System;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the vasharti watch, translated from retail pattern <c>IDYun_Temp_62</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class VashartiWatchAiTests
{
	private const int Yunanmarch = 300300000;

	private const int Watcher = 236284;
	private const int Officer = 236285;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Yunanmarch).WithWorldSize(2048)
			.WithAi(typeof(VashartiWatchAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// A post: one watcher engaged with a raider, one neighbour close enough to hear it.
	/// </summary>
	/// <remarks>
	/// The neighbour stands beside the raider rather than beside the watcher, because a listener too
	/// far from the player cannot take hate on them however well it heard the call — the trap the
	/// Ophidan Bridge pins fell into.
	/// </remarks>
	private static (BossAiHarness, Npc, Npc, Player) Post()
	{
		BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(Watcher, 300f, 300f, 200f);
		Npc neighbour = harness.Spawn(Officer, 312f, 300f, 200f);
		Player raider = harness.SpawnPlayer(310f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(caller, neighbour);
		harness.Engage(caller, raider);
		return (harness, caller, neighbour, raider);
	}

	/// <summary>
	/// <b>The post edges onto the target and keeps edging.</b> Retail arms the beat on entering combat
	/// and re-arms it from itself, so it runs for as long as the fight does.
	/// </summary>
	/// <remarks>
	/// <b>Not a clean one-per-three-seconds, and that is the mechanic rather than a defect.</b> A
	/// neighbour that takes a point engages, and an engaged watcher starts calling too — so a post
	/// feeds itself, and the rate depends on how many of them are in earshot of each other. What is
	/// pinned is the shape: it grows, it keeps growing, and every step is glance-sized.
	/// </remarks>
	[Fact]
	public void ThePostEdgesOntoTheTargetEveryThreeSeconds()
	{
		var (harness, caller, neighbour, raider) = Post();
		using BossAiHarness _h = harness;

		int before = neighbour.GetAggroList().GetHate(raider);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		int early = neighbour.GetAggroList().GetHate(raider) - before;

		harness.Clock.Advance(TimeSpan.FromSeconds(9));
		int later = neighbour.GetAggroList().GetHate(raider) - before;

		Assert.True(early > 0, "the post never called");
		Assert.True(later > early, "the post called once and stopped");
		Assert.True(later < 20, "a glance became a claim: " + later);
	}

	/// <summary>
	/// <b>One point is a glance, not a claim.</b> It is nowhere near enough to take the player off
	/// whoever they are already fighting, which is what makes this a drift rather than a snap.
	/// </summary>
	[Fact]
	public void OnePointIsAGlanceNotAClaim()
	{
		var (harness, caller, neighbour, raider) = Post();
		using BossAiHarness _h = harness;

		Player other = harness.SpawnPlayer(313f, 300f, 200f, race: Race.ASMODIANS);
		harness.Engage(neighbour, other);
		int held = neighbour.GetAggroList().GetHate(other);

		harness.Clock.Advance(TimeSpan.FromSeconds(30));

		Assert.True(neighbour.GetAggroList().GetHate(raider) < held,
			"ten calls were enough to take the officer off the player it was already fighting");
	}

	/// <summary>
	/// <b>And only within twenty-five metres</b>, which is retail's range on every broadcast in the
	/// pattern.
	/// </summary>
	[Fact]
	public void AndOnlyWithinTwentyFiveMetres()
	{
		var (harness, caller, neighbour, raider) = Post();
		using BossAiHarness _h = harness;

		Npc distant = harness.Spawn(Officer, 340f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(caller, distant);

		harness.Clock.Advance(TimeSpan.FromSeconds(12));

		Assert.True(neighbour.GetAggroList().GetHate(raider) > 0, "the near officer never heard it");
		Assert.Equal(0, distant.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The beat stops when the fight does.</b> Retail's is a battle timer, so a post that goes home
	/// is not still calling its neighbours onto a player who left.
	/// </summary>
	[Fact]
	public void TheBeatStopsWhenTheFightDoes()
	{
		var (harness, caller, neighbour, raider) = Post();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		int whileFighting = neighbour.GetAggroList().GetHate(raider);
		Assert.True(whileFighting > 0, "the post never called at all");

		// Both of them: a neighbour that took a point is fighting too, and an engaged watcher runs its
		// own beat. Sending only the caller home leaves the post calling, which is correct.
		caller.GetAi().OnGeneralEvent(AiEventType.BackHome);
		neighbour.GetAi().OnGeneralEvent(AiEventType.BackHome);
		harness.Clock.Advance(TimeSpan.FromSeconds(30));

		Assert.Equal(whileFighting, neighbour.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The message number is retail's, not ours.</b> Caller and listener share one constant, so
	/// nothing else here would notice it changing.
	/// </summary>
	[Fact]
	public void TheMessageNumberIsRetails()
	{
		Assert.Equal(450, VashartiWatchAI.OntoThisOne);
	}
}
