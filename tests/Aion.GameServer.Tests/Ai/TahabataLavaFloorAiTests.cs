using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Tahabata's lava floor, which burned once instead of for the whole phase.
/// </summary>
/// <remarks>
/// Retail's floor sets a two-second idle timer on waking and then drops its damage twin at its own point
/// for three seconds, re-arming at one — so it pulses every second and carries no lifetime of its own,
/// standing until the next health rung takes it up.
/// <para>
/// This port spawned the floor and one damage npc together, once, ten seconds after the rung fired. A
/// floor that ticks once is not the mechanic: the phase is meant to be a place you cannot stand.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TahabataLavaFloorAiTests
{
	private const int TiamatStronghold = 300510000;

	/// <summary>The three floors, and the damage npc each one drops.</summary>
	private const int FloorOne = 283116;
	private const int DamageOne = 283117;
	private const int FloorTwo = 283118;
	private const int DamageTwo = 283119;
	private const int FloorThree = 283120;
	private const int DamageThree = 283121;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(TahabataLavaFloorAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>Each floor drops its own damage npc, and only its own.</b>
	/// </summary>
	/// <remarks>
	/// The pairing is the next id up — 283116 with 283117 — and it holds for all three. Getting it wrong
	/// would put the first phase's damage under the third phase's floor.
	/// </remarks>
	[Theory]
	[InlineData(FloorOne, DamageOne, DamageThree)]
	[InlineData(FloorTwo, DamageTwo, DamageOne)]
	[InlineData(FloorThree, DamageThree, DamageTwo)]
	public void EachFloorDropsItsOwnDamage(int floor, int damage, int theOther)
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(floor, 679.88f, 1068.88f, 497.88f);

		// Two and a half seconds: the first tick has landed and the second has not. Reading at three
		// finds two, because each damage npc lives three seconds and the ticks are a second apart.
		harness.Clock.Advance(TimeSpan.FromMilliseconds(2500));

		Assert.Equal(1, Count(harness, damage));
		Assert.Equal(0, Count(harness, theOther));
	}

	/// <summary>
	/// <b>Nothing burns for the first two seconds.</b>
	/// </summary>
	[Fact]
	public void TheFloorWaitsTwoSecondsBeforeItsFirstTick()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(FloorOne, 679.88f, 1068.88f, 497.88f);

		harness.Clock.Advance(TimeSpan.FromMilliseconds(1500));

		Assert.Equal(0, Count(harness, DamageOne));
	}

	/// <summary>
	/// <b>And then it ticks every second, for as long as it stands.</b>
	/// </summary>
	/// <remarks>
	/// This is the correction: one tick against a phase's worth. Counted by arrivals, because each damage
	/// npc clears itself after three seconds.
	/// </remarks>
	[Fact]
	public void TheFloorTicksEverySecond()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(FloorOne, 679.88f, 1068.88f, 497.88f);

		// Two seconds to the first, then one a second: ticks at 2 through 13 inclusive is twelve.
		BossAiHarness.Watched seen = harness.WatchNew(13, null, DamageOne);

		Assert.Equal(12, seen.Total);
	}

	/// <summary>
	/// <b>The floor itself has no lifetime.</b> It stands until something takes it up.
	/// </summary>
	/// <remarks>
	/// Retail gives the FX no <c>live_time</c> — the rung that follows despawns it. A floor that expired
	/// on its own would end the phase early.
	/// </remarks>
	[Fact]
	public void TheFloorStaysUntilSomethingRemovesIt()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(FloorOne, 679.88f, 1068.88f, 497.88f);

		harness.Clock.Advance(TimeSpan.FromMinutes(2));

		Assert.Equal(1, Count(harness, FloorOne));
	}

	/// <summary>
	/// <b>The damage does not pile up.</b> Each npc lives three seconds, so only a few stand at once.
	/// </summary>
	/// <remarks>
	/// Without the lifetime a floor left one behind every second — thirteen of them after thirteen
	/// seconds. Counting arrivals cannot see that, which is why this counts what is standing.
	/// </remarks>
	[Fact]
	public void TheDamageDoesNotPileUp()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(FloorOne, 679.88f, 1068.88f, 497.88f);

		harness.Clock.Advance(TimeSpan.FromSeconds(13));

		// Ticks a second apart, each living three: three or four alive at any moment, never a dozen.
		Assert.InRange(Count(harness, DamageOne), 1, 4);
	}

	/// <summary>The boss himself, for the rungs that lay the floors.</summary>
	private const int Tahabata = 219358;

	private static BossAiHarness NewBossHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(BrigadeGeneralTahabataAI), typeof(TahabataLavaFloorAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>The first floor is laid at ninety-five per cent, not ninety-six.</b>
	/// </summary>
	/// <remarks>
	/// Retail's three lava rungs are 95, 60 and 20; this class had 96 and 55 for the first two. One point
	/// sounds like nothing, but the pin has to separate them or the threshold is unpinned — and reverting
	/// the whole ladder survived the first mutation sweep for exactly that reason.
	/// </remarks>
	[Fact]
	public void TheFirstFloorIsLaidAtNinetyFive()
	{
		using BossAiHarness harness = NewBossHarness();
		Npc boss = harness.Spawn(Tahabata, 679f, 1068f, 497.88f);
		var player = harness.SpawnPlayer(683f, 1068f, 497.88f);
		harness.Engage(boss, player);

		// Read at ninety-eight rather than ninety-six: HP percentage is computed from current over max
		// and rounds, so ninety-six can read as ninety-five and fire the rung. Ninety-eight is clear of
		// that and still below the old ninety-six threshold, which is what this has to separate.
		BossAiHarness.SetHpPercent(boss, 98);
		boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);
		Assert.Equal(0, Count(harness, FloorOne));

		BossAiHarness.SetHpPercent(boss, 95);
		boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);
		Assert.Equal(1, Count(harness, FloorOne));
	}

	/// <summary>
	/// <b>And the second floor takes up the first.</b>
	/// </summary>
	/// <remarks>
	/// Retail's rung opens with <c>despawn SPAWN_ID_2</c>. This class never removed the old floor, so all
	/// three accumulated and the room kept every phase's damage at once.
	/// </remarks>
	[Fact]
	public void TheSecondFloorTakesUpTheFirst()
	{
		using BossAiHarness harness = NewBossHarness();
		Npc boss = harness.Spawn(Tahabata, 679f, 1068f, 497.88f);
		var player = harness.SpawnPlayer(683f, 1068f, 497.88f);
		harness.Engage(boss, player);

		foreach (int percent in new[] { 95, 75, 60 })
		{
			BossAiHarness.SetHpPercent(boss, percent);
			boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);
		}

		Assert.Equal(0, Count(harness, FloorOne));
		Assert.Equal(1, Count(harness, FloorTwo));
	}
}
