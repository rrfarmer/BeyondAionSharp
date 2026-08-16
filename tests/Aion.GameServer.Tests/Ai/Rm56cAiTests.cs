using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="Rm56cAI"/>, translated from retail pattern <c>NLehpar_BhC</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The whole port is the trap ladder — one, two, three or four by health band, each band laying its
/// arrangement once and then re-laying it on roughly every other cycle of its own timer.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class Rm56cAiTests
{
	private const int AzoturanFortress = 310100000;
	private const int Rm56c = 214802;
	private const int CompleteTrap = 281281;

	private const float BossX = 300f;
	private const float BossY = 300f;

	private static (BossAiHarness, Npc, Player) Engaged(int hpPercent)
	{
		BossAiHarness harness = BossAiHarness.For(AzoturanFortress).WithWorldSize(2048)
			.WithAi(typeof(Rm56cAI), typeof(TrapNpcAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(Rm56c, BossX, BossY, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		BossAiHarness.SetHpPercent(boss, hpPercent);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static void Advance(BossAiHarness harness, Npc boss, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Traps(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == CompleteTrap);

	/// <summary>The ladder itself: the arrangement thickens as it loses ground.</summary>
	[Theory]
	[InlineData(90, 0)]
	[InlineData(70, 1)]
	[InlineData(50, 2)]
	[InlineData(30, 3)]
	[InlineData(15, 4)]
	public void EachBandLaysItsOwnNumberOfTraps(int hpPercent, int expected)
	{
		var (harness, boss, player) = Engaged(hpPercent);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 6);

		Assert.Equal(expected, Traps(harness));
	}

	/// <summary>
	/// The four below 20 sit on the corners of an eight-metre square, which is the only arrangement
	/// where both offsets are non-zero.
	/// </summary>
	[Fact]
	public void TheLowestBandsFourSitOnTheCornersOfASquare()
	{
		var (harness, boss, player) = Engaged(15);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 6);

		List<(float, float)> corners = harness.LiveNpcs()
			.Where(n => n.GetNpcId() == CompleteTrap)
			.Select(n => (n.GetPosition().GetX() - BossX, n.GetPosition().GetY() - BossY))
			.OrderBy(p => p.Item1).ThenBy(p => p.Item2)
			.ToList();

		Assert.Equal([(-4f, -4f), (-4f, 4f), (4f, -4f), (4f, 4f)], corners);
	}

	/// <summary>
	/// Each band lays once on entering it. Without the flag the timer-0 heartbeat would re-lay every
	/// five seconds instead of leaving it to the band's own slower timer.
	/// </summary>
	/// <remarks>
	/// Counted by identity: the traps expire after twelve seconds, so a live count cannot tell "laid
	/// once" from "laid four times and expired three".
	/// </remarks>
	[Fact]
	public void ABandLaysItsArrangementOnceNotOnEveryHeartbeat()
	{
		var (harness, boss, player) = Engaged(70);
		using BossAiHarness _h = harness;
		var seen = new HashSet<Npc>();

		// Four timer-0 heartbeats at five seconds each, well short of the band timer's twenty-five.
		for (int i = 0; i < 22; i++)
		{
			Advance(harness, boss, player, 1);
			foreach (Npc trap in harness.LiveNpcs().Where(n => n.GetNpcId() == CompleteTrap))
				seen.Add(trap);
		}

		Assert.Single(seen);
	}

	[Fact]
	public void TheTrapsLiveTwelveSecondsAndGo()
	{
		var (harness, boss, player) = Engaged(50);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 6);
		List<Npc> first = harness.LiveNpcs().Where(n => n.GetNpcId() == CompleteTrap).ToList();
		Assert.Equal(2, first.Count);

		Advance(harness, boss, player, 10);
		Assert.All(first, t => Assert.True(t.IsSpawned(), "should still stand short of twelve seconds"));

		Advance(harness, boss, player, 3);
		Assert.All(first, t => Assert.False(t.IsSpawned(), "should have gone once twelve seconds passed"));
	}

	/// <summary>
	/// Retail's bands are below-20 and 21-40, so <b>exactly 20 belongs to neither</b>. Only the
	/// heartbeat keeps the chain alive through it, and no trap is laid until it drops another point.
	/// </summary>
	[Fact]
	public void AtExactlyTwentyNoBandClaimsIt()
	{
		var (harness, boss, player) = Engaged(30);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 6);
		Assert.Equal(3, Traps(harness));
		Advance(harness, boss, player, 14);
		Assert.Equal(0, Traps(harness));

		SetExactPercent(boss, 20);
		Advance(harness, boss, player, 30);
		Assert.Equal(0, Traps(harness));

		SetExactPercent(boss, 19);
		Advance(harness, boss, player, 6);
		Assert.Equal(4, Traps(harness));
	}

	/// <summary>Sets health so the AI reads back exactly the percentage asked for.</summary>
	private static void SetExactPercent(Npc npc, int percent)
	{
		var life = npc.GetLifeStats();
		int max = life.GetMaxHp();
		int hp = (int)Math.Ceiling(max * percent / 100.0);
		while (hp < max && (int)(100f * hp / max) < percent)
			hp++;
		life.SetCurrentHp(hp);
		Assert.Equal(percent, life.GetHpPercentage());
	}

	[Fact]
	public void DyingClearsTheTraps()
	{
		var (harness, boss, player) = Engaged(15);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 6);
		Assert.Equal(4, Traps(harness));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Traps(harness));
	}

	[Fact]
	public void LeavingTheFightClearsThemToo()
	{
		var (harness, boss, player) = Engaged(15);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 6);
		Assert.NotEqual(0, Traps(harness));

		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Equal(0, Traps(harness));
	}
}
