using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="TiamatDragonHardAI"/>, translated from retail pattern
/// <c>IDTiamat_Hard_Tiamat_Dragon</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class TiamatDragonHardAiTests
{
	private const int DragonLordsRefuge = 300520000;
	private const int HardTiamat = 236276;

	private const int InfernoSpirit = 283067;
	private const int BurrowingArrival = 283062;
	private static readonly int[] Mages = [856483, 856484, 856485, 856486];

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(TiamatDragonHardAI), typeof(AggressiveNpcAI), typeof(AggressiveNoLootNpcAI),
				typeof(GeneralNpcAI), typeof(ThickDustAI)).Build();

	/// <summary>Rehate and keep-alive: a fight the player loses ends the fight and cancels the chain.</summary>
	private static Action Alive(Npc boss, Player player) => () =>
	{
		BossAiHarness.Rehate(boss, player);
		BossAiHarness.KeepAlive(player);
	};

	private static int MageCount(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => Mages.Contains(n.GetNpcId()));

	private const int ShapeChangeFlash = 283174;
	private const int ThickDust = 283134;

	/// <summary>Waking puts a flash on its mark and a spirit and an arrival at her feet.</summary>
	[Fact]
	public void WakingBringsTheSpiritAndTheArrival()
	{
		using BossAiHarness harness = NewHarness();

		Npc boss = harness.Spawn(HardTiamat, 500f, 514f, 417f);

		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == InfernoSpirit));
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == BurrowingArrival));
		Assert.True(MageCount(harness) == 0, "the mages wait for the fight");

		// The flash stands on its own absolute mark, not on hers.
		Npc flash = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == ShapeChangeFlash));
		Assert.Equal(457.9f, flash.GetX(), 1);
		Assert.Equal(514.5f, flash.GetY(), 1);
	}

	/// <summary>
	/// All three are brief, and none of them equally so: six seconds for the spirit, eight for the
	/// arrival, ten for the flash. Read one at a time, so a pin fails on the one that moved.
	/// </summary>
	[Fact]
	public void TheThreeGoInTheOrderRetailGivesThem()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(HardTiamat, 500f, 514f, 417f);

		harness.Clock.Advance(TimeSpan.FromSeconds(7));
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == InfernoSpirit));
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == BurrowingArrival));
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == ShapeChangeFlash));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == BurrowingArrival));
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == ShapeChangeFlash));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == ShapeChangeFlash));
	}

	/// <summary>
	/// Nineteen seconds into the fight the four mages take the corners — fifteen to the one-shot, four
	/// more on its fuse.
	/// </summary>
	[Fact]
	public void TheFourMagesTakeTheCornersNineteenSecondsIn()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(HardTiamat, 500f, 514f, 417f);
		Player player = harness.SpawnPlayer(504f, 514f, 417f);
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);

		BossAiHarness.Watched early = harness.Watch(
			17, Alive(boss, player), Mages);
		Assert.Equal(0, early.Total);

		harness.Watch(5, Alive(boss, player), Mages);

		Assert.Equal(4, MageCount(harness));

		// One each, at four distinct corners, rather than four of one in a heap.
		Assert.Equal(4, harness.LiveNpcs().Where(n => Mages.Contains(n.GetNpcId()))
			.Select(n => n.GetNpcId()).Distinct().Count());
		Assert.Equal(4, harness.LiveNpcs().Where(n => Mages.Contains(n.GetNpcId()))
			.Select(n => (n.GetX(), n.GetY())).Distinct().Count());
	}

	/// <summary>They come once, not every fifteen seconds — the step carries a one-shot flag.</summary>
	[Fact]
	public void TheMagesComeOnlyOnce()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(HardTiamat, 500f, 514f, 417f);
		Player player = harness.SpawnPlayer(504f, 514f, 417f);
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);

		BossAiHarness.Watched run = harness.Watch(
			120, Alive(boss, player), Mages);

		Assert.Equal(4, run.Total);
	}

	/// <summary>Each mage faces the heading retail gives its corner, rather than all four facing north.</summary>
	[Fact]
	public void TheMagesCarryTheirRetailHeadings()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(HardTiamat, 500f, 514f, 417f);
		Player player = harness.SpawnPlayer(504f, 514f, 417f);
		harness.Engage(boss, player);

		harness.Watch(20, Alive(boss, player), Mages);

		Dictionary<int, int> headings = harness.LiveNpcs()
			.Where(n => Mages.Contains(n.GetNpcId()))
			.ToDictionary(n => n.GetNpcId(), n => (int)n.GetSpawn().GetHeading());

		// dir=77/42/17/103 in the pattern, as our 0..119 heading units.
		Assert.Equal(Facing(77), headings[856483]);
		Assert.Equal(Facing(42), headings[856484]);
		Assert.Equal(Facing(17), headings[856485]);
		Assert.Equal(Facing(103), headings[856486]);
	}

	private static int Facing(int degrees) => PositionUtil.ConvertAngleToHeading(degrees % 360);

	/// <summary>Killing her takes the corners with her — retail's on_die clears the group.</summary>
	[Fact]
	public void DyingTakesTheMagesWithHer()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(HardTiamat, 500f, 514f, 417f);
		Player player = harness.SpawnPlayer(504f, 514f, 417f);
		harness.Engage(boss, player);

		harness.Watch(20, Alive(boss, player), Mages);
		Assert.Equal(4, MageCount(harness));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, MageCount(harness));
	}

	/// <summary>
	/// The dust cloud she leaves on dying does not outlive the same branch: retail files it under the
	/// group the branch then clears, so it is placed and taken away in one breath.
	/// </summary>
	[Fact]
	public void TheDustCloudCancelsItself()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(HardTiamat, 500f, 514f, 417f);
		Player player = harness.SpawnPlayer(504f, 514f, 417f);
		harness.Engage(boss, player);

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == ThickDust));
	}
}
