using System.IO;
using System;
using System.Linq;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Guarded, multi-branch idle cycles: the wave controllers that never ran here.
/// </summary>
/// <remarks>
/// 81 retail patterns across 83 npcs, every one on a class that does nothing with a timer.
/// <c>IDForest_Wave_Phase1</c> below is the shape in miniature: retail's alternating-flag idiom, where
/// each rung fires once and hands over to the next, and the last one arms zero to stop.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class IdleCycleAiTests
{
	private const int AnyMap = 300520000;

	/// <summary>A forest wave controller: two waves, then it stops.</summary>
	private const int Controller = 282240;

	/// <summary>The add it places, five then three.</summary>
	private const int Add = 282190;

	/// <summary>One of the 67 controllers retail keeps passive -- a MONSTER tribe, so a player standing
	/// next to it would be aggroed by an aggressive class and is not by this one.</summary>
	private const int PassiveController = 282528;

	/// <summary>The one it places alongside the second wave.</summary>
	private const int Leader = 282191;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AnyMap).WithWorldSize(4096)
			.WithAi(typeof(IdleCycleAI), typeof(IdleCyclePassiveAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI)).Build();

	/// <summary><b>Nothing before the wake-up delay.</b></summary>
	[Fact]
	public void NothingBeforeTheWakeDelay()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Controller, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == Add);
	}

	/// <summary><b>The first rung places five.</b></summary>
	[Fact]
	public void TheFirstRungPlacesFive()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Controller, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));

		Assert.Equal(5, harness.LiveNpcs().Count(n => n.GetNpcId() == Add));
		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == Leader);
	}

	/// <summary>
	/// <b>Five seconds later the second rung places three more and the leader.</b> The flag on the
	/// first rung is spent, so the cycle falls through to the next one.
	/// </summary>
	[Fact]
	public void TheSecondRungFollowsFiveSecondsLater()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Controller, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Assert.Equal(8, harness.LiveNpcs().Count(n => n.GetNpcId() == Add));
		Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == Leader));
	}

	/// <summary>
	/// <b>And there is no third wave.</b> The second rung arms zero, which stops the timer — read as
	/// "next tick" it would place three more adds every tick for the life of the controller.
	/// <para>
	/// Sampled ten seconds after the second wave, not two minutes: the adds carry <c>live_time=60</c>,
	/// so by two minutes they have gone by themselves and the count is zero whether the bug is there or
	/// not. Written that way first, and the suite said so.
	/// </para>
	/// </summary>
	[Fact]
	public void ThereIsNoThirdWave()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Controller, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		harness.Clock.Advance(TimeSpan.FromSeconds(10));

		Assert.Equal(8, harness.LiveNpcs().Count(n => n.GetNpcId() == Add));
	}

	/// <summary>
	/// <b>Every cycle in the table has a wake delay and at least one rung.</b> A controller with rungs
	/// and no delay never starts; one with a delay and no rungs wakes to do nothing.
	/// </summary>
	[Fact]
	public void EveryCycleHasBothHalves()
	{
		Assert.Equal(83, IdleCycles.WakeMillis.Count);

		foreach ((int npcId, int delay) in IdleCycles.WakeMillis)
		{
			Assert.True(delay > 0, $"npc {npcId} has no wake delay");
			Assert.NotEmpty(IdleCycles.CycleRungsFor(npcId));
			Assert.NotEmpty(IdleCycles.WakeRungFor(npcId));
		}
	}

	/// <summary>
	/// <b>Every message a cycle sends carries a real string id.</b>
	/// </summary>
	/// <remarks>
	/// The ids come from the client's own <c>strings.xml</c> by way of
	/// <c>tools/client-extract/out/string_ids.tsv</c>. A name that failed to resolve would emit as zero
	/// and send an empty line rather than fail, which is the quiet failure worth a pin: the extractor
	/// refuses the whole pattern instead, and this is what proves it.
	/// </remarks>
	[Fact]
	public void EveryMessageCarriesARealStringId()
	{
		string path = Path.Combine(BossAiHarness.RepoRoot(), "tools", "client-extract", "out",
			"idle_cycles.tsv");
		int shouts = 0;
		int systemLines = 0;

		foreach (string line in File.ReadLines(path).Skip(1))
		{
			string[] fields = line.Split('	');
			if (fields.Length < 15)
				continue;

			if (fields[6] is not ("say" or "sysmsg"))
				continue;

			Assert.True(int.Parse(fields[7]) > 0, $"unresolved string id in {fields[14]}");
			if (fields[6] == "say")
				shouts++;
			else
				systemLines++;
		}

		// A shout is spoken by the npc within fifty metres; a system line goes to the whole instance.
		// Retail leans heavily on the second in these controllers.
		Assert.Equal(3, shouts);
		Assert.Equal(53, systemLines);
	}
	/// <summary><b>Killing the controller takes its wave with it.</b></summary>
	/// <remarks>
	/// Retail marks these spawns <c>despawn_at_attack_state</c> -- 3,129 of the 3,294 inside
	/// <c>on_idle_timer</c>, and 2,267 of those are permanent. An earlier entry guessed the flag
	/// "rarely applies" to wave controllers because they seldom fight; the count says otherwise, and
	/// the transition that matters for them is not a fight ending but the controller being killed or
	/// removed. Without this the adds outlive whatever placed them, forever.
	/// </remarks>
	[Fact]
	public void KillingTheControllerClearsItsWave()
	{
		using BossAiHarness harness = NewHarness();
		Npc controller = harness.Spawn(Controller, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.Equal(5, harness.LiveNpcs().Count(npc => npc.GetNpcId() == Add));

		// The player arrives only to land the killing blow; spawning one next to the controller before
		// the wave pulls it into combat and the cycle never runs.
		Player player = harness.SpawnPlayer(900f, 900f, 200f);

		// Well inside the adds' sixty-second lifetime.
		BossAiHarness.Kill(controller, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(0, harness.LiveNpcs().Count(npc => npc.GetNpcId() == Add));
	}
	/// <summary><b>A controller retail keeps passive does not attack anybody.</b></summary>
	/// <remarks>
	/// <b>67 of the 83 npcs this table drives were <c>general</c> before it bound them</b>, and the
	/// class it bound them to descends from <c>AggressiveNpcAI</c>. For several entries they were
	/// scenery that attacked on sight, and every pin in this file stayed green the whole time: the
	/// waves still arrived on schedule, which is all any of them looked at.
	/// <para>
	/// <b>This pin does not prove the passivity, and the difference is worth being clear about.</b> A
	/// mutation restoring the aggressive handler survives it: <c>AggressiveNpcAI</c> guards that handler
	/// on <c>CanThink()</c> and then defers through an <c>AggroNotifier</c>, and neither reaches
	/// anything the harness stands up. What is pinned is that these npcs are bound to the passive class
	/// and that nothing here hates anybody; the base class itself is asserted only by the binding.
	/// </para>
	/// <para>
	/// The equivalent pin in the wake tables <i>is</i> decisive, because that class descends from
	/// <c>GeneralNpcAI</c> and a type check settles it. This one cannot, because
	/// <c>PassivePatternAi</c> inherits <c>AggressiveNpcAI</c> and puts its three handlers back.
	/// </para>
	/// </remarks>
	[Fact]
	public void APassiveControllerStaysPassive()
	{
		using BossAiHarness harness = NewHarness();
		Npc controller = harness.Spawn(PassiveController, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(controller, player);

		controller.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CreatureAggro, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		// Behaviour, not type: PassivePatternAi still inherits AggressiveNpcAI and puts the three
		// handlers back the way GeneralNpcAI has them, so the class check that works for the wake
		// tables says nothing here. What matters is that the aggro event moved nothing.
		Assert.Empty(controller.GetAggroList().Stream());
		Assert.False(controller.GetAi().IsInState(Aion.GameServer.Ai.AIState.FIGHT));
	}
}
