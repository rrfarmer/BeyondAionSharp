using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="DeputyHanumanAI"/> and <see cref="HanumanSubordinateAI"/>, translated from
/// retail patterns <c>NDrakan_KhB</c>, <c>NDrakan_ChSlave4</c> and <c>NDrakan_Chslave5</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Two LEGENDARY bosses that had no AI class at all. The shape worth pinning is that the adds are the
/// same four twice re-forged rather than three separate waves, that the ladder stops itself below
/// thirty, and that the pick he peels with changes when it does.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DeputyHanumanAiTests
{
	/// <summary>Heiron, where both captains stand.</summary>
	private const int Heiron = 210040000;

	private const int Hanuman = 212306;
	private const int Indratu = 280751;

	private const int First = 280752;
	private const int Second = 280753;
	private const int Third = 280754;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Heiron).WithWorldSize(4096)
			.WithAi(typeof(DeputyHanumanAI), typeof(HanumanSubordinateAI), typeof(AggressiveNpcAI))
			.Build();

	private static (BossAiHarness, Npc, List<Player>) Engaged(int npcId = Hanuman, int raidSize = 3)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(npcId, 1000f, 2800f, 236f);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
			raid.Add(harness.SpawnPlayer(1005f + i, 2800f, 236f));

		harness.Engage(boss, raid[0]);

		// Hate descending with the list, so raid[2] is the third-most-hated and raid[0] holds him.
		for (int i = 0; i < raidSize; i++)
			for (int n = raidSize - i; n > 0; n--)
				BossAiHarness.Rehate(boss, raid[i]);

		return (harness, boss, raid);
	}

	/// <summary>Keeps every member on the hate list and standing, which is the ordinary case.</summary>
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

	/// <summary>Keeps the hate order without healing anybody, for the pins about who he picks.</summary>
	private static void AdvanceWounded(BossAiHarness harness, List<Player> raid, Npc boss, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player member in raid)
				BossAiHarness.Rehate(boss, member);

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Untouched he calls nobody — the whole ladder hangs off the fight.</summary>
	[Fact]
	public void AnUnpulledHanumanCallsNobody()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Hanuman, 1000f, 2800f, 236f);

		harness.Clock.Advance(TimeSpan.FromSeconds(120));

		Assert.Equal(0, Count(harness, First));
	}

	/// <summary>Above ninety nothing arrives, however long the fight runs.</summary>
	[Fact]
	public void AboveNinetyHeCallsNobody()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 95);
		Advance(harness, raid, boss, 60);

		Assert.Equal(0, Count(harness, First));
		Assert.Equal(0, Count(harness, Second));
	}

	/// <summary>Two at 71–90, and two more into the same group at 51–70. Each band pays once.</summary>
	[Fact]
	public void EachOfTheTwoUpperBandsCallsTwoSubordinates()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 85);
		Advance(harness, raid, boss, 8);
		Assert.Equal(2, Count(harness, First));

		// Standing in the band pays nothing more: the rung carries a flag var.
		Advance(harness, raid, boss, 40);
		Assert.Equal(2, Count(harness, First));

		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, raid, boss, 10);
		Assert.Equal(4, Count(harness, First));
	}

	/// <summary>
	/// <b>The wave is re-forged, not added to.</b> Entering 31–50 every subordinate still standing
	/// sheds itself for the second form, and five seconds later two already-changed ones arrive — so
	/// four become six, and none of the first form is left.
	/// </summary>
	[Fact]
	public void TheThirdBandChangesThemAllAndAddsTwoMore()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 85);
		Advance(harness, raid, boss, 8);
		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, raid, boss, 10);
		Assert.Equal(4, Count(harness, First));

		BossAiHarness.SetExactPercent(boss, 40);
		Advance(harness, raid, boss, 15);

		Assert.Equal(0, Count(harness, First));
		Assert.Equal(6, Count(harness, Second));
	}

	/// <summary>
	/// And below thirty they change a second time — six of the second form become six of the third,
	/// and the ladder that fed them is over.
	/// </summary>
	[Fact]
	public void BelowThirtyTheyChangeAgainAndNothingMoreArrives()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 85);
		Advance(harness, raid, boss, 8);
		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, raid, boss, 10);
		BossAiHarness.SetExactPercent(boss, 40);
		Advance(harness, raid, boss, 15);
		Assert.Equal(6, Count(harness, Second));

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 10);

		Assert.Equal(0, Count(harness, Second));
		Assert.Equal(6, Count(harness, Third));

		// The deep rung is the only one that does not re-arm the ladder.
		Advance(harness, raid, boss, 120);
		Assert.Equal(6, Count(harness, Third));
	}

	/// <summary>
	/// <b>A raid that skips straight to the end gets nothing at all.</b> Pushed under thirty before
	/// any band opened there is no wave to change, and no wave ever comes.
	/// </summary>
	[Fact]
	public void PushedStraightBelowThirtyHeCallsNobody()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 120);

		Assert.Equal(0, Count(harness, First));
		Assert.Equal(0, Count(harness, Second));
		Assert.Equal(0, Count(harness, Third));
	}

	/// <summary>
	/// The pair 31–50 sends directly keeps <b>twenty</b> minutes, not the thirty the first form gets —
	/// retail shortens the clock as the forms escalate.
	/// </summary>
	/// <remarks>
	/// Pinned by outliving it, which is the only way a lifetime is observable. It survived the first
	/// mutation sweep: every other pin reads a count within a minute of the wave landing, and at that
	/// range twelve hundred seconds and eighteen hundred look exactly alike.
	/// </remarks>
	[Fact]
	public void TheDirectWaveKeepsTwentyMinutes()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 40);
		Advance(harness, raid, boss, 15);
		Assert.Equal(2, Count(harness, Second));

		Advance(harness, raid, boss, 1190);
		Assert.Equal(2, Count(harness, Second));

		Advance(harness, raid, boss, 20);
		Assert.Equal(0, Count(harness, Second));
	}

	/// <summary>
	/// <b>And the stop is permanent.</b> Healed from under thirty back into a band that never opened,
	/// he still calls nobody — the deep rung took the clock away and no rung above it can re-arm it.
	/// </summary>
	/// <remarks>
	/// This is the pin the ladder's own shape hides: below thirty every band rung is out of range
	/// anyway, so re-arming the clock there looks free. It costs a whole wave the moment a healer
	/// brings him back up, which is the case a fifteen-percent pin can never reach.
	/// </remarks>
	[Fact]
	public void HealedBackUpAfterTheStopHeStillCallsNobody()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 10);

		BossAiHarness.SetExactPercent(boss, 40);
		Advance(harness, raid, boss, 60);

		Assert.Equal(0, Count(harness, First));
		Assert.Equal(0, Count(harness, Second));
	}

	/// <summary>Missing Indratu shares the pattern, so he pays the same ladder.</summary>
	[Fact]
	public void IndratuRunsTheSameLadder()
	{
		var (harness, boss, raid) = Engaged(Indratu);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 85);
		Advance(harness, raid, boss, 8);

		Assert.Equal(2, Count(harness, First));
	}

	/// <summary>
	/// <b>The first change is on a two-second fuse and the second is not.</b> Driven by the message
	/// rather than by the boss, so the pin fails if the fuse is dropped or the second form grows one.
	/// </summary>
	/// <remarks>
	/// The add is engaged first because retail's fuse is an <c>add_battle_timer</c>, and a battle timer
	/// only fires while its owner is fighting — which is also the fight's own case, since every
	/// subordinate is called into a pull. A peeled add walking home misses its change; that is retail's
	/// own consequence and not ours.
	/// </remarks>
	[Fact]
	public void OnlyTheFirstFormChangesOnAFuse()
	{
		using BossAiHarness harness = NewHarness();
		Npc first = harness.Spawn(First, 1000f, 2800f, 236f);
		Npc caller = harness.Spawn(Hanuman, 1002f, 2800f, 236f);
		Player quarry = harness.SpawnPlayer(1005f, 2800f, 236f);
		BossAiHarness.MakeMutuallyKnown(caller, first);
		harness.Engage(first, quarry);

		NpcMessageBus.Broadcast(caller, HanumanSubordinateAI.ChangeOnce, null, 50f);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.Equal(1, Count(harness, First));
		Assert.Equal(0, Count(harness, Second));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, First));
		Npc second = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == Second);

		BossAiHarness.MakeMutuallyKnown(caller, second);
		NpcMessageBus.Broadcast(caller, HanumanSubordinateAI.ChangeAgain, null, 50f);

		Assert.Equal(0, Count(harness, Second));
		Assert.Equal(1, Count(harness, Third));
	}

	/// <summary>
	/// The change lands where the add was standing, not on the caller — retail's
	/// <c>SPAWN_LOCATION_MY_POINT</c> with no range, which is what keeps a scattered pack scattered.
	/// </summary>
	[Fact]
	public void TheSuccessorTakesTheSameGround()
	{
		using BossAiHarness harness = NewHarness();
		Npc first = harness.Spawn(First, 1024f, 2782f, 236f);
		Npc caller = harness.Spawn(Hanuman, 1000f, 2800f, 236f);
		Player quarry = harness.SpawnPlayer(1027f, 2782f, 236f);
		BossAiHarness.MakeMutuallyKnown(caller, first);
		harness.Engage(first, quarry);

		NpcMessageBus.Broadcast(caller, HanumanSubordinateAI.ChangeOnce, null, 50f);
		harness.Clock.Advance(TimeSpan.FromSeconds(3));

		Npc second = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == Second);
		Assert.Equal(1024f, second.GetX(), 1);
		Assert.Equal(2782f, second.GetY(), 1);
	}

	/// <summary>
	/// <b>He peels.</b> Twenty-five seconds after the 71–90 band opens he turns on the third-most-hated
	/// player — off the tank and off the off-tank.
	/// </summary>
	[Fact]
	public void InTheUpperBandHeTurnsOnTheThirdMostHated()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		Assert.Same(raid[0], boss.GetTarget());

		BossAiHarness.SetExactPercent(boss, 85);
		AdvanceWounded(harness, raid, boss, 8);
		Assert.Same(raid[0], boss.GetTarget());

		AdvanceWounded(harness, raid, boss, 27);
		Assert.Same(raid[2], boss.GetTarget());
	}

	/// <summary>
	/// <b>And it keeps peeling for as long as the band lasts.</b> The alarm alternates between two
	/// timers — twenty-five seconds out, twenty back — so a group that survives the first peel is
	/// peeled again forty-five seconds later, against whoever is third by then.
	/// </summary>
	/// <remarks>
	/// The hate order is turned over between the two peels on purpose: peeling twice onto the same
	/// player is indistinguishable from peeling once, so a pin that leaves the order alone passes
	/// whether or not the hand-back rung exists.
	/// </remarks>
	[Fact]
	public void TheUpperBandPeelRepeats()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 85);
		AdvanceWounded(harness, raid, boss, 35);
		Assert.Same(raid[2], boss.GetTarget());

		// The peeled-onto player is now holding him, which puts the off-tank third.
		for (int i = 0; i < 5; i++)
			BossAiHarness.Rehate(boss, raid[2]);

		AdvanceWounded(harness, raid, boss, 45);
		Assert.Same(raid[1], boss.GetTarget());
	}

	/// <summary>
	/// <b>And below thirty the pick changes.</b> Every twenty-eight seconds he goes for the lowest
	/// health fraction in the room instead — here the off-tank, who is neither holding him nor third
	/// on the list, so the pin cannot pass on the peel alone.
	/// </summary>
	[Fact]
	public void BelowThirtyHeHuntsTheWeakest()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		raid[1].GetLifeStats().SetCurrentHpPercent(5);

		BossAiHarness.SetExactPercent(boss, 20);
		AdvanceWounded(harness, raid, boss, 8);
		Assert.Same(raid[2], boss.GetTarget());

		AdvanceWounded(harness, raid, boss, 20);
		Assert.Same(raid[1], boss.GetTarget());
	}

	/// <summary>
	/// The group re-picks when told, and the two forms answer differently: the second takes whoever is
	/// closest to dying, which is what makes an ignored pack lethal to a healer late on.
	/// </summary>
	[Fact]
	public void ToldToRePickTheSecondFormTakesTheWeakest()
	{
		using BossAiHarness harness = NewHarness();
		Npc add = harness.Spawn(Second, 1000f, 2800f, 236f);
		Npc caller = harness.Spawn(Hanuman, 1002f, 2800f, 236f);
		BossAiHarness.MakeMutuallyKnown(caller, add);

		var raid = new List<Player>();
		for (int i = 0; i < 3; i++)
			raid.Add(harness.SpawnPlayer(1005f + i, 2800f, 236f));

		harness.Engage(add, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(add, member);

		raid[2].GetLifeStats().SetCurrentHpPercent(5);
		Assert.NotSame(raid[2], add.GetTarget());

		NpcMessageBus.Broadcast(caller, HanumanSubordinateAI.PickAnother, null, 50f);

		Assert.Same(raid[2], add.GetTarget());
	}
}
