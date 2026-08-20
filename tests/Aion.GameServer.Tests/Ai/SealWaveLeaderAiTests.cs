using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="SealWaveLeaderAI"/> — Drakenspire Depths' five wave leaders
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// <b>The leaders are the senders,</b> so unlike the wave's rank and file these pins mostly measure what
/// comes <em>out</em> of the npc: a message the rest of the room was never hearing, and an add that was
/// never appearing. The hearing side is pinned through a wave attacker where one exists and through the
/// npc's own spawn list where it does not.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SealWaveLeaderAiTests
{
	private const int Reshanta = 400010000;

	private const int Leader1 = 236239;
	private const int Leader2 = 236240;
	private const int Leader3 = 236241;
	private const int Leader4 = 236242;
	private const int Leader5 = 236243;

	/// <summary><c>IDSeal_Wave_Group1_Fi</c> — a wave attacker, to be dismissed and to hear.</summary>
	private const int WaveTank = 236204;

	/// <summary><c>IDSeal_Forward_Guard_Li_Fi</c>.</summary>
	private const int ForwardGuard = 236248;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(SealWaveLeaderAI), typeof(SealWaveAttackerAI),
				typeof(AggressiveNpcAI), typeof(AggressiveNoLootNpcAI), typeof(GeneralNpcAI),
				typeof(NoActionAI))
			.Build();

	/// <summary>
	/// <b>Every leader puts an arrow on whoever it is fighting.</b> Retail's <c>spawn_on_target</c> row is
	/// identical on leaders 1, 4 and 5, and it is the only add in the whole wave — nothing was placing it,
	/// because all five leaders ran a class with no spawn in it at all.
	/// </summary>
	/// <remarks>
	/// Leader 4 is the one measured because its command timer needs no alternation: retail arms it at
	/// thirty seconds on entering combat and every rung of it spawns, so one window is the whole mechanic.
	/// <para>
	/// <b>Counted with <see cref="BossAiHarness.Watch"/> rather than by looking at the end.</b> The arrow
	/// lives fifteen seconds against a thirty-second beat, so for half of every cycle the field is empty
	/// and a pin that advanced the clock and then counted would read that as "nothing happened" — the
	/// exact silent failure the helper's own remark was written for.
	/// </para>
	/// </remarks>
	[Fact]
	public void EveryLeaderPutsAnArrowOnWhoeverItIsFighting()
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(Leader4, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		harness.Engage(leader, player);
		// Retail's healthy rung is is_hp_in_boundary 50..100, exclusive at BOTH ends, so an untouched
		// leader at exactly a hundred matches neither half and the timer never comes round again. That is
		// retail's own gap and not a translation error -- but it means a pin has to land a blow first.
		BossAiHarness.SetHpPercent(leader, 90);

		BossAiHarness.Watched seen = harness.Watch(35, () => Hold(harness, leader, player),
			SealWaveLeaderAI.ArrowTarget);

		Assert.True(seen.Total >= 1, "the leader's thirty-second command rung produced no arrow");
	}

	/// <summary>
	/// <b>Leader 4 keeps throwing them.</b> Its command timer rearms itself on both halves of the health
	/// split, so the arrows are a thirty-second drumbeat rather than a one-off.
	/// </summary>
	[Fact]
	public void LeaderFourKeepsThrowingThem()
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(Leader4, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		harness.Engage(leader, player);
		BossAiHarness.SetHpPercent(leader, 90);

		BossAiHarness.Watched seen = harness.Watch(70, () => Hold(harness, leader, player),
			SealWaveLeaderAI.ArrowTarget);

		Assert.True(seen.Total >= 2, $"a seventy-second window produced {seen.Total} arrows, not a drumbeat");
	}

	/// <summary>
	/// <b>And it throws them wounded as well as healthy.</b> Retail splits that rung on fifty percent and
	/// both halves spawn — the split is about the skills, not the add, so a leader below half must not go
	/// quiet.
	/// </summary>
	[Fact]
	public void AndItThrowsThemWoundedAsWellAsHealthy()
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(Leader4, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		harness.Engage(leader, player);

		BossAiHarness.Watched seen = harness.Watch(35, () =>
		{
			Hold(harness, leader, player);
			BossAiHarness.SetHpPercent(leader, 30);
		}, SealWaveLeaderAI.ArrowTarget);

		Assert.True(seen.Total >= 1, "a leader below half went quiet");
	}

	/// <summary>
	/// <b>Leader 1's ring alternates, and only one turn in two carries the arrow.</b> The two rungs that
	/// compete for timer 2 are guarded by <c>set_flag_var</c> and <c>unset_flag_var</c> on the same flag,
	/// so they take turns; a chain that spawned every time round would be twice retail's rate.
	/// </summary>
	/// <remarks>
	/// Retail's ring is 7 + 7 + 7 to close on timer 2, then 15 back to the start. The first close takes
	/// the plain turn, so a window that stops before the second close must be empty — and that emptiness
	/// is the whole assertion, because it is what tells an alternator apart from a rung that fires every
	/// time.
	/// </remarks>
	[Fact]
	public void LeaderOnesRingAlternates()
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(Leader1, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		harness.Engage(leader, player);

		BossAiHarness.Watched firstTurn = harness.Watch(25, () => Hold(harness, leader, player),
			SealWaveLeaderAI.ArrowTarget);
		Assert.Equal(0, firstTurn.Total);

		BossAiHarness.Watched secondTurn = harness.Watch(35, () => Hold(harness, leader, player),
			SealWaveLeaderAI.ArrowTarget);

		Assert.True(secondTurn.Total >= 1, "the command turn of the ring produced no arrow");
	}

	/// <summary>Keeps the fight open across a window, the way every timer pin in this suite does.</summary>
	private static void Hold(BossAiHarness harness, Npc leader, Player player)
	{
		BossAiHarness.Rehate(leader, player);
		BossAiHarness.KeepAlive(player);
	}

	/// <summary>
	/// <b>A leader buffs the wave the moment it is engaged.</b> 22750 is the message all nine wave
	/// patterns listen for, and nothing in the instance was sending it.
	/// </summary>
	/// <remarks>
	/// Measured through the wave attacker's own <c>despawn_self</c> rungs being <em>absent</em> for this
	/// number — a positive assertion is impossible without a skill index to answer the buff with, so this
	/// pin checks the broadcast reaches a hearer at all by using a number the hearer does answer, and the
	/// companion pin below checks 22750 specifically leaves the leader.
	/// </remarks>
	[Fact]
	public void ALeaderBuffsTheWaveTheMomentItIsEngaged()
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(Leader3, 300f, 300f, 200f);
		Npc heard = harness.Spawn(WaveTank, 305f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(leader, heard);
		var seen = new List<int>();
		heard.GetAggroList().AddHate(leader, 0);

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
			harness.Engage(leader, player);

		Assert.Contains(SealWaveLeaderAI.CommandBuff, seen);
	}

	/// <summary>
	/// <b>And asks to be healed once it is under seventy, once.</b> 22757 is answered only by the wave's
	/// priest leader, and the request stops the six-second health check that produced it — retail's rearm
	/// sits on a lower rung, so a leader asks and then stops asking.
	/// </summary>
	[Fact]
	public void AndAsksToBeHealedOnceItIsUnderSeventyOnce()
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(Leader3, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		var seen = new List<int>();

		harness.Engage(leader, player);
		BossAiHarness.SetHpPercent(leader, 50);

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
		{
			harness.Clock.Advance(TimeSpan.FromSeconds(7));
			BossAiHarness.Rehate(leader, player);
			Assert.Equal(1, seen.Count(m => m == SealWaveLeaderAI.HealRequest));

			// The check does not come round again: the request rung did not rearm it.
			harness.Clock.Advance(TimeSpan.FromSeconds(30));
			BossAiHarness.Rehate(leader, player);
		}

		Assert.Equal(1, seen.Count(m => m == SealWaveLeaderAI.HealRequest));
	}

	/// <summary>
	/// <b>Above seventy it does not ask at all,</b> which is what makes the health check a check rather
	/// than a countdown.
	/// </summary>
	[Fact]
	public void AboveSeventyItDoesNotAskAtAll()
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(Leader3, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		var seen = new List<int>();

		harness.Engage(leader, player);

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
		{
			harness.Clock.Advance(TimeSpan.FromSeconds(20));
			BossAiHarness.Rehate(leader, player);
		}

		Assert.DoesNotContain(SealWaveLeaderAI.HealRequest, seen);
	}

	/// <summary>
	/// <b>Leader 4 never asks,</b> because retail gives it no health check at all — it is the one leader
	/// with no timer 4 and no 22757 anywhere in its pattern.
	/// </summary>
	[Fact]
	public void LeaderFourNeverAsks()
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(Leader4, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		var seen = new List<int>();

		harness.Engage(leader, player);
		BossAiHarness.SetHpPercent(leader, 20);

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
		{
			harness.Clock.Advance(TimeSpan.FromSeconds(40));
			BossAiHarness.Rehate(leader, player);
		}

		Assert.DoesNotContain(SealWaveLeaderAI.HealRequest, seen);
	}

	/// <summary>
	/// <b>A leader takes the forward guard's shout every time.</b> Like the leader groups and unlike the
	/// rank and file, retail gives this rung no <c>test_probability</c>.
	/// </summary>
	[Fact]
	public void ALeaderTakesTheForwardGuardsShoutEveryTime()
	{
		using BossAiHarness harness = NewHarness();
		Npc guard = harness.Spawn(ForwardGuard, 300f, 300f, 200f);
		Npc leader = harness.Spawn(Leader2, 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(guard, leader);
		BossAiHarness.NeverRolls(leader);

		NpcMessageBus.Broadcast(guard, SealWaveLeaderAI.GuardTaunt, null, 100f);

		Assert.Equal(SealWaveLeaderAI.TauntHate, leader.GetAggroList().GetHate(guard));
	}

	/// <summary>
	/// <b>Leader 5 leaves something behind.</b> Retail runs the same rung on both its death handlers and
	/// it is the only leader that does — a field object at the point it fell.
	/// </summary>
	[Fact]
	public void LeaderFiveLeavesSomethingBehind()
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(Leader5, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);

		harness.Engage(leader, player);
		// Deliberately no Wound. BossAiHarness.Kill records no damage on purpose, and adding some sends
		// NpcController.OnDie down DoReward -- which needs a database, throws, and is caught by a handler
		// that only logs. The AI's Died event is raised AFTER DoReward inside that same try, so the throw
		// silently swallows the whole death handler and an on_die branch looks like it was never written.
		BossAiHarness.Kill(leader, player);

		Assert.Contains(harness.LiveNpcs(), n => n.GetNpcId() == SealWaveLeaderAI.OminousDarkness);
	}

	/// <summary>
	/// <b>And the other four do not.</b> The death spawn belongs to leader 5 alone, and a class shared
	/// across five bosses is exactly where that kind of thing leaks.
	/// </summary>
	[Fact]
	public void AndTheOtherFourDoNot()
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(Leader1, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);

		harness.Engage(leader, player);
		// Deliberately no Wound. BossAiHarness.Kill records no damage on purpose, and adding some sends
		// NpcController.OnDie down DoReward -- which needs a database, throws, and is caught by a handler
		// that only logs. The AI's Died event is raised AFTER DoReward inside that same try, so the throw
		// silently swallows the whole death handler and an on_die branch looks like it was never written.
		BossAiHarness.Kill(leader, player);

		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == SealWaveLeaderAI.OminousDarkness);
	}
}
