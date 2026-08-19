using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// King Consierd's condors, which came late, too often, and never stopped.
/// </summary>
/// <remarks>
/// Retail gives them a battle timer of their own — <c>BTIMERI_INDEX_4</c> at 30000, guarded by
/// <c>is_hp_in_boundary larger_than=26 less_than=100</c> and first armed by the rung that fires below
/// fifty-five per cent. This class hung them off its own twenty-five second skill task behind an
/// <c>hp &lt;= 50</c> test, so they started five per cent late, arrived a fifth too often, and carried
/// on to the end of the fight.
/// <para>
/// Found by <c>audit_hp_phases.py</c>.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class KingConsierdCondorTests
{
	private const int EmpyreanCrucible = 300300000;
	private const int Consierd = 217595;
	private const int Condor = 282378;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(EmpyreanCrucible).WithWorldSize(2048)
			.WithAi(typeof(KingConsierdAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	/// <summary>Engages him and walks his health down to <paramref name="toPercent"/>.</summary>
	private static (BossAiHarness, Npc) Wounded(int toPercent)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Consierd, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		harness.Engage(boss, player);
		for (int hp = 99; hp >= toPercent; hp--)
		{
			BossAiHarness.SetExactPercent(boss, hp);
			boss.SetTarget(player);
			boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);
		}

		return (harness, boss);
	}

	/// <summary>
	/// <b>Above fifty-five per cent he calls no condors.</b>
	/// </summary>
	/// <remarks>
	/// Counted as they arrive: a condor stands ten minutes, so a survivor count would say the same thing
	/// whether or not any were called.
	/// </remarks>
	[Fact]
	public void AboveFiftyFivePerCentHeCallsNoCondors()
	{
		var (harness, _) = Wounded(60);
		using BossAiHarness _h = harness;

		Assert.Equal(0, harness.WatchNew(120, null, Condor).Total);
	}

	/// <summary>
	/// <b>And below it they start.</b> Retail's rung arms the timer at fifty-five, not fifty.
	/// </summary>
	[Fact]
	public void AndBelowItTheyStart()
	{
		var (harness, _) = Wounded(54);
		using BossAiHarness _h = harness;

		Assert.Equal(KingConsierdAI.CondorsPerWave, harness.WatchNew(35, null, Condor).Total);
	}

	/// <summary>
	/// <b>Two every thirty seconds, not every twenty-five.</b>
	/// </summary>
	/// <remarks>
	/// Two windows, because one cannot tell a period from a first firing — the same trap that hid a
	/// halved cycle on Celestius and a reversed cascade on Beritra.
	/// </remarks>
	[Fact]
	public void TwoEveryThirtySeconds()
	{
		var (harness, _) = Wounded(54);
		using BossAiHarness _h = harness;

		Assert.Equal(2, harness.WatchNew(35, null, Condor).Total);
		Assert.Equal(0, harness.WatchNew(20, null, Condor).Total);
		Assert.Equal(2, harness.WatchNew(10, null, Condor).Total);
	}

	/// <summary>
	/// <b>And below twenty-six per cent they stop.</b>
	/// </summary>
	/// <remarks>
	/// Retail's guard is <c>larger_than=26</c>, so the last quarter of the fight is deliberately clear.
	/// This class had no floor at all: condors kept coming until he died.
	/// </remarks>
	[Fact]
	public void AndBelowTwentySixPerCentTheyStop()
	{
		var (harness, boss) = Wounded(54);
		using BossAiHarness _h = harness;
		harness.Clock.Advance(TimeSpan.FromSeconds(35));

		// Held down each second: over two virtual minutes his own regeneration lifts him back above
		// the floor, and the condors correctly resume — which reads as the guard not working at all.
		Assert.Equal(0, harness.WatchNew(120, () => BossAiHarness.SetExactPercent(boss, 20), Condor).Total);
	}

	/// <summary>
	/// <b>They scatter about him rather than landing on him.</b> Retail's <c>spawn_range</c> is ten.
	/// </summary>
	/// <remarks>
	/// Both used to appear at his exact point, which stacks two birds inside the boss and gives a melee
	/// group nothing to move for.
	/// </remarks>
	[Fact]
	public void TheyScatterAboutHim()
	{
		var (harness, boss) = Wounded(54);
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(35));

		List<Npc> condors = harness.LiveNpcs().Where(n => n.GetNpcId() == Condor).ToList();
		Assert.Equal(KingConsierdAI.CondorsPerWave, condors.Count);
		Assert.All(condors, c =>
			Assert.NotEqual(0d, Math.Sqrt(Math.Pow(c.GetX() - boss.GetX(), 2) + Math.Pow(c.GetY() - boss.GetY(), 2)), 3));
	}
}
