using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="BrigadeGeneralAnuhartAI"/> and <see cref="AnuhartSubordinateAI"/>, translated
/// from retail patterns <c>XDrakan_LastBoss</c> and <c>LastBoss_Su</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Dark Poeta's last boss, on plain <c>aggressive</c>, in an instance whose other five grades were
/// translated some time ago. The shape worth pinning is that his four subordinates land on fixed
/// marks and are then told what to hit, and that below thirty an enrage relay keeps adding to them.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class BrigadeGeneralAnuhartAiTests
{
	private const int DarkPoeta = 300040000;

	private const int Anuhart = 214904;
	private const int Subordinate = 281249;
	private const int FlameCentre = 281246;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DarkPoeta).WithWorldSize(2048)
			.WithAi(typeof(BrigadeGeneralAnuhartAI), typeof(AnuhartSubordinateAI), typeof(NTrapAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>He stands on his platform; the raid is thirty metres off, out of an add's own reach.</summary>
	private static (BossAiHarness, Npc, List<Player>) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Anuhart, 274f, 322f, 130f);
		var raid = new List<Player>();
		for (int i = 0; i < 3; i++)
			raid.Add(harness.SpawnPlayer(310f + i, 322f, 130f));

		harness.Engage(boss, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(boss, member);

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

	private static int Arrived(BossAiHarness harness, Npc boss, List<Player> raid, int seconds,
		params int[] npcIds) =>
		harness.WatchNew(seconds, () =>
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}
		}, npcIds).Total;

	/// <summary>Above seventy he calls nobody, however long the fight runs.</summary>
	[Fact]
	public void AboveSeventyHeCallsNobody()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 90);
		Advance(harness, raid, boss, 120);

		Assert.Equal(0, Count(harness, Subordinate));
	}

	/// <summary>
	/// <b>Crossing seventy puts four subordinates on four fixed marks.</b> Retail names the
	/// coordinates rather than a walker route, which is what makes them placeable at all.
	/// </summary>
	[Fact]
	public void CrossingSeventyPutsFourOnTheirMarks()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, raid, boss, 10);

		Assert.Equal(4, Count(harness, Subordinate));

		// One of them stands on the mark furthest from him, which a spawn-at-his-feet would not.
		Assert.Contains(harness.LiveNpcs(),
			n => n.GetNpcId() == Subordinate && Math.Abs(n.GetX() - 266.974f) < 1f);
	}

	/// <summary>And once, however long he spends in the band.</summary>
	[Fact]
	public void TheBandStepPaysOnce()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, raid, boss, 10);
		Assert.Equal(4, Count(harness, Subordinate));

		Advance(harness, raid, boss, 120);
		Assert.Equal(4, Count(harness, Subordinate));
	}

	/// <summary>
	/// <b>They land with nothing to do and are picked up by the first relay order.</b> Thirty metres
	/// from the raid a subordinate has nobody it could have found by itself, so this reads the orders
	/// and nothing else.
	/// </summary>
	/// <remarks>
	/// The step spawns them and broadcasts in the same branch, and
	/// <see cref="Aion.GameServer.Ai.Pattern.PatternAi"/> excludes whatever the running branch spawned
	/// from that branch's own broadcast — the rule measured for RM-56c. This is the <b>second</b>
	/// encounter to want the opposite (the anuhart casters' pet was the first), and our measured
	/// behaviour is kept in both. See docs/retail-ai-fidelity.md.
	/// </remarks>
	[Fact]
	public void TheSubordinatesArePickedUpByTheFirstRelayOrder()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, raid, boss, 10);

		Npc summoned = Assert.IsType<Npc>(
			harness.LiveNpcs().FirstOrDefault(n => n.GetNpcId() == Subordinate));
		Assert.Null(summoned.GetTarget());

		Advance(harness, raid, boss, 35);
		Assert.NotNull(summoned.GetTarget());
	}

	/// <summary>
	/// <b>And the order keeps being re-issued.</b> A relay repeats it about every twenty-seven seconds
	/// for the rest of the fight, so a subordinate peeled onto something else is taken back.
	/// </summary>
	[Fact]
	public void TheOrderKeepsBeingReIssued()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;
		Npc boss = harness.Spawn(Anuhart, 274f, 322f, 130f);
		Player quarry = harness.SpawnPlayer(310f, 322f, 130f);
		Player elsewhere = harness.SpawnPlayer(274f, 400f, 130f);
		harness.Engage(boss, quarry);
		var only = new List<Player> { quarry };

		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, only, boss, 10);
		Npc summoned = Assert.IsType<Npc>(
			harness.LiveNpcs().FirstOrDefault(n => n.GetNpcId() == Subordinate));

		// Past the first relay order, which is what puts them on anybody at all.
		Advance(harness, only, boss, 35);
		Assert.Same(quarry, summoned.GetTarget());

		NpcMessageBus.Broadcast(boss, AnuhartSubordinateAI.TakeThisOne, elsewhere, 50f);
		Assert.Same(elsewhere, summoned.GetTarget());

		Advance(harness, only, boss, 35);
		Assert.Same(quarry, summoned.GetTarget());
	}

	/// <summary>
	/// <b>Below thirty the flame centres start falling</b>, and the enrage relay keeps adding
	/// subordinates on top of the four.
	/// </summary>
	[Fact]
	public void BelowThirtyTheEnrageKeepsAdding()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, raid, boss, 10);
		Assert.Equal(4, Count(harness, Subordinate));

		BossAiHarness.SetExactPercent(boss, 20);
		Assert.Equal(4, Arrived(harness, boss, raid, 10, FlameCentre));

		// Then four more flames and two more subordinates on each turn of the relay.
		Assert.True(Arrived(harness, boss, raid, 120, Subordinate) >= 4,
			"the enrage relay added no subordinates in two minutes");
	}

	/// <summary>A raid that pushes him straight past seventy gets no subordinates at all.</summary>
	[Fact]
	public void PushedStraightBelowThirtyNoneAreCalled()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 15);

		Assert.Equal(0, Count(harness, Subordinate));
	}

	/// <summary>Both exits clear the subordinates.</summary>
	[Fact]
	public void BothExitsClearTheSubordinates()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, raid, boss, 10);
		Assert.Equal(4, Count(harness, Subordinate));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Equal(0, Count(harness, Subordinate));
	}

	/// <summary>A subordinate answers either of retail's two orders.</summary>
	[Theory]
	[InlineData(AnuhartSubordinateAI.TakeThisOne)]
	[InlineData(AnuhartSubordinateAI.GoForThisOne)]
	public void ASubordinateAnswersEitherOrder(int message)
	{
		using BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(Anuhart, 274f, 322f, 130f);
		Npc summoned = harness.Spawn(Subordinate, 276f, 322f, 130f);
		Player quarry = harness.SpawnPlayer(310f, 322f, 130f);
		BossAiHarness.MakeMutuallyKnown(caller, summoned);

		Assert.Null(summoned.GetTarget());

		NpcMessageBus.Broadcast(caller, message, quarry, 50f);

		Assert.Same(quarry, summoned.GetTarget());
	}
}
