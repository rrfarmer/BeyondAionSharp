using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="ChiefGunnerKurmataAI"/>, <see cref="SupplyBaseFlameCannonAI"/> and
/// <see cref="SupplyBaseMarkAI"/>, translated from retail patterns
/// <c>IDVritra_Base_Drakan_Gi_Nmd</c>, <c>…_Tank</c> and <c>…_Beacon</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// A targeting mechanic in three parts: the gunner paints a player, the paint calls, and the cannon
/// fires at the paint. The pins that matter are that the marks land on players rather than on him,
/// that they stick, and that the cannon turns on the mark rather than on the player under it.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ChiefGunnerKurmataAiTests
{
	private const int SauroSupplyBase = 301220000;

	private const int Kurmata = 230851;
	private const int Cannon = 284453;
	private const int Mark = 284454;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(SauroSupplyBase).WithWorldSize(2048)
			.WithAi(typeof(ChiefGunnerKurmataAI), typeof(SupplyBaseFlameCannonAI),
				typeof(SupplyBaseMarkAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>Four players, spread out, so a mark's position says which of them it landed on.</summary>
	private static (BossAiHarness, Npc, List<Player>) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Kurmata, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < 4; i++)
			raid.Add(harness.SpawnPlayer(306f + (i * 6f), 300f, 200f));

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

	private static int Standing(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary><b>He marks somebody the moment he is pulled</b>, and it lands on a player.</summary>
	[Fact]
	public void HeMarksSomebodyOnBeingPulled()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		Assert.Equal(1, Standing(harness, Mark));
		Npc mark = harness.LiveNpcs().Single(n => n.GetNpcId() == Mark);
		Assert.True(mark.GetX() > 303f, $"the mark is at {mark.GetX():F1}, which is his own feet");
	}

	/// <summary>
	/// <b>The mark sticks to whoever it landed on.</b> Retail gives it a hundred thousand hate points
	/// and <c>attack_target_after_spawn</c>, so it is not scenery.
	/// </summary>
	[Fact]
	public void TheMarkSticksToWhoeverItLandedOn()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		Npc mark = harness.LiveNpcs().Single(n => n.GetNpcId() == Mark);
		Advance(harness, raid, boss, 2);

		Assert.NotNull(mark.GetTarget());
	}

	/// <summary>
	/// <b>And a second mark on his quarry twenty-two seconds in</b>, on the loop's own step rather
	/// than on the opening.
	/// </summary>
	/// <remarks>
	/// Counted as arrivals rather than as heads: a mark lives twenty seconds, so the first is already
	/// gone by the time the second lands and a head count would read one either way.
	/// </remarks>
	[Fact]
	public void AndASecondMarkOnHisQuarry()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;
		BossAiHarness.SetExactPercent(boss, 80);

		int by20 = harness.WatchNew(20, () => Keep(harness, raid, boss), Mark).Total;
		Assert.Equal(0, by20);

		int by30 = harness.WatchNew(10, () => Keep(harness, raid, boss), Mark).Total;
		Assert.Equal(1, by30);
	}

	private static void Keep(BossAiHarness harness, List<Player> raid, Npc boss)
	{
		foreach (Player member in raid)
		{
			BossAiHarness.Rehate(boss, member);
			BossAiHarness.KeepAlive(member);
		}
	}

	/// <summary>
	/// <b>Below sixty he marks two at a time.</b> Retail's <c>total_set_to_spawn</c> is two, not the
	/// whole raid, which is what the element's name invites you to assume.
	/// </summary>
	[Fact]
	public void BelowSixtyHeMarksTwoAtATime()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;
		BossAiHarness.SetExactPercent(boss, 50);

		// The opening mark, then the sixty-percent rung at the first five-second tick.
		Advance(harness, raid, boss, 8);

		Assert.Equal(3, Standing(harness, Mark));
	}

	/// <summary>And the relay puts two more up nineteen seconds after that, over and over.</summary>
	[Fact]
	public void AndTheRelayKeepsMarkingInPairs()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;
		BossAiHarness.SetExactPercent(boss, 50);

		int arrived = harness.WatchNew(70, () =>
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}
		}, Mark).Total;

		// Three pairs: the sixty-percent rung, and two turns of the eleven-and-fourteen relay. The
		// mark he plants on the pull is already standing when the watch opens, so it is not counted.
		Assert.Equal(6, arrived);
	}

	/// <summary>
	/// <b>The cannon fires at the mark, not at the player under it.</b> Retail's <c>22273</c> carries
	/// the mark itself as its parameter, which is how a pattern says "where I am pointing".
	/// </summary>
	[Fact]
	public void TheCannonFiresAtTheMarkItself()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Kurmata, 300f, 300f, 200f);
		Npc cannon = harness.Spawn(Cannon, 306f, 300f, 200f);
		Player player = harness.SpawnPlayer(312f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, cannon);
		BossAiHarness.MakeMutuallyKnown(cannon, player);
		Assert.Null(cannon.GetTarget());

		// The whole chain, in order: he calls, he marks, and the mark announces itself as it wakes.
		harness.Engage(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Npc aimed = Assert.IsType<Npc>(cannon.GetTarget());
		Assert.Equal(Mark, aimed.GetNpcId());
	}

	/// <summary>And on being pulled the gunner sends the cannon after whoever pulled him.</summary>
	/// <remarks>
	/// The cannon stands forty metres out, inside the gunner's fifty-metre call and outside the
	/// fifty-metre call of the mark that lands on the player — otherwise this measures the end of the
	/// chain rather than its first step, because the mark speaks second and speaks louder.
	/// </remarks>
	[Fact]
	public void TheGunnersCallSendsTheCannonAtHisQuarry()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Kurmata, 300f, 300f, 200f);
		Npc cannon = harness.Spawn(Cannon, 340f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 260f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, cannon);
		BossAiHarness.MakeMutuallyKnown(cannon, player);
		Assert.Null(cannon.GetTarget());

		harness.Engage(boss, player);

		Assert.Same(player, cannon.GetTarget());
	}

	/// <summary>His death clears every mark he left standing.</summary>
	[Fact]
	public void DyingClearsTheMarks()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;
		Assert.Equal(1, Standing(harness, Mark));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Standing(harness, Mark));
	}

	/// <summary>
	/// <b>Ten thousand hate points is what keeps the cannon on the mark.</b> Retail gives that number
	/// to both of its branches, and it is the reason a raid cannot pull the cannon off by hitting it.
	/// </summary>
	/// <remarks>
	/// Written first with the whole chain running, which measured the wrong thing: the gunner's own
	/// call gives the player ten thousand as well, so a thousand more on top puts the player ahead
	/// fairly. Narrowed to the mark's call alone, which is where the number has to do its work.
	/// </remarks>
	[Fact]
	public void NothingPullsTheCannonOffTheMark()
	{
		using BossAiHarness harness = NewHarness();
		Npc cannon = harness.Spawn(Cannon, 300f, 300f, 200f);
		Npc mark = harness.Spawn(Mark, 306f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(cannon, mark);
		BossAiHarness.MakeMutuallyKnown(cannon, player);

		// A thousand hate from somebody hitting it, then the mark speaks.
		BossAiHarness.Rehate(cannon, player);
		NpcMessageBus.Broadcast(mark, SupplyBaseFlameCannonAI.MarkLanded, mark, 50f);

		Assert.Same(mark, cannon.GetAggroList().GetTarget(
			Aion.GameServer.Controllers.Attack.AggroTarget.MOST_HATED));
	}
}
