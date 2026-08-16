using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="BulwarkJeshuchiAI"/>, <see cref="WatcherZapielAI"/> and
/// <see cref="DiscipleOfZapielAI"/>, translated from retail patterns <c>ND2_KeD</c>, <c>ND2_KeE</c>
/// and <c>ND2_Ksum3</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Two Heiron watchers on plain <c>aggressive</c>, and the pair are a study in contrast: one summons
/// a wave that grows with every band, the other summons nothing our runtime can place and instead
/// commands the cherubim already standing around him.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class HeironWatcherAiTests
{
	private const int Heiron = 210040000;

	private const int Jeshuchi = 212282;
	private const int Zapiel = 212283;
	private const int JeshuchiDisciple = 280758;
	private const int ZapielDisciple = 280760;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Heiron).WithWorldSize(4096)
			.WithAi(typeof(BulwarkJeshuchiAI), typeof(WatcherZapielAI), typeof(DiscipleOfZapielAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, List<Player>) Engaged(int npcId)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(npcId, 2900f, 2570f, 181f);
		var raid = new List<Player>();
		for (int i = 0; i < 3; i++)
			raid.Add(harness.SpawnPlayer(2904f + i, 2570f, 181f));

		harness.Engage(boss, raid[0]);
		for (int i = 0; i < raid.Count; i++)
			for (int n = raid.Count - i; n > 0; n--)
				BossAiHarness.Rehate(boss, raid[i]);

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

	// ---- Bulwark Jeshuchi -------------------------------------------------------------------------

	/// <summary>Untouched he calls nobody: the ladder hangs off the fight.</summary>
	[Fact]
	public void AnUnpulledJeshuchiCallsNobody()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Jeshuchi, 2900f, 2570f, 181f);

		harness.Clock.Advance(TimeSpan.FromSeconds(120));

		Assert.Equal(0, Count(harness, JeshuchiDisciple));
	}

	/// <summary>
	/// <b>The wave grows with every band: three, then four, then five.</b> Each step once, however long
	/// the fight spends in the band.
	/// </summary>
	[Fact]
	public void ThreeThenFourThenFive()
	{
		var (harness, boss, raid) = Engaged(Jeshuchi);
		using BossAiHarness _h = harness;

		Advance(harness, raid, boss, 11);
		Assert.Equal(3, Count(harness, JeshuchiDisciple));

		Advance(harness, raid, boss, 40);
		Assert.Equal(3, Count(harness, JeshuchiDisciple));

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, raid, boss, 10);
		Assert.Equal(7, Count(harness, JeshuchiDisciple));

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 10);
		Assert.Equal(12, Count(harness, JeshuchiDisciple));

		Advance(harness, raid, boss, 60);
		Assert.Equal(12, Count(harness, JeshuchiDisciple));
	}

	/// <summary>
	/// <b>And the last step changes who he takes.</b> The first two turn him onto the third-most-hated;
	/// crossing thirty-five he goes for whoever is closest to dying instead.
	/// </summary>
	/// <remarks>
	/// The wounded player is <em>not</em> healed while the clock runs, which the ordinary advance here
	/// does — with everybody at full health "closest to dying" is whatever the list happens to return,
	/// and the pin would pass or fail at random.
	/// </remarks>
	[Fact]
	public void TheLastStepTakesTheWeakestRatherThanTheThird()
	{
		var (harness, boss, raid) = Engaged(Jeshuchi);
		using BossAiHarness _h = harness;

		// The off-tank is nearly dead. Nobody is healed from here on.
		raid[1].GetLifeStats().SetCurrentHpPercent(5);

		BossAiHarness.SetExactPercent(boss, 20);
		for (int i = 0; i < 12; i++)
		{
			foreach (Player member in raid)
				BossAiHarness.Rehate(boss, member);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		Assert.Equal(5, Count(harness, JeshuchiDisciple));
		Assert.Same(raid[1], boss.GetTarget());
	}

	/// <summary>Dying takes the disciples with him, and so does going home.</summary>
	[Fact]
	public void BothExitsClearTheDisciples()
	{
		var (harness, boss, raid) = Engaged(Jeshuchi);
		using BossAiHarness _h = harness;

		Advance(harness, raid, boss, 11);
		Assert.Equal(3, Count(harness, JeshuchiDisciple));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.BackHome);
		Assert.Equal(0, Count(harness, JeshuchiDisciple));
	}

	// ---- Watcher Zapiel ---------------------------------------------------------------------------

	/// <summary>
	/// <b>Zapiel commands rather than summons.</b> Every band step points the cherubim standing round
	/// him at whoever he is fighting — here a disciple that was minding its own business.
	/// </summary>
	/// <remarks>
	/// <b>All three steps, and with a decoy.</b> They are three separate branches and a translation can
	/// lose any one of them on its own — and a disciple left near the raid joins the fight by itself,
	/// so a pin that only checks it ended up on somebody passes whether or not the order was ever
	/// given. The disciple here stands forty metres away with its own player beside it: it takes that
	/// one unprompted, and only the order moves it to Zapiel's.
	/// </remarks>
	[Theory]
	[InlineData(90)]
	[InlineData(70)]
	[InlineData(40)]
	public void EachBandStepPointsTheDisciplesAtHisQuarry(int percent)
	{
		var (harness, boss, raid) = Engaged(Zapiel);
		using BossAiHarness _h = harness;

		Npc disciple = harness.Spawn(ZapielDisciple, 2930f, 2600f, 181f);
		Player decoy = harness.SpawnPlayer(2932f, 2600f, 181f);
		BossAiHarness.MakeMutuallyKnown(boss, disciple);
		harness.Engage(disciple, decoy);
		Assert.Same(decoy, disciple.GetTarget());

		BossAiHarness.SetExactPercent(boss, percent);
		Advance(harness, raid, boss, 11);

		Assert.Same(raid[0], disciple.GetTarget());
	}

	/// <summary>And he comes off the tank himself at the same moment.</summary>
	[Fact]
	public void AndHeTurnsOntoTheThirdMostHated()
	{
		var (harness, boss, raid) = Engaged(Zapiel);
		using BossAiHarness _h = harness;

		Assert.Same(raid[0], boss.GetTarget());
		Advance(harness, raid, boss, 11);
		Assert.Same(raid[2], boss.GetTarget());
	}

	/// <summary>
	/// <b>Below thirty he stops stepping and starts repeating.</b> The deep rung does not re-arm the
	/// ladder, and the order loop it opens runs about every thirty-two seconds instead.
	/// </summary>
	[Fact]
	public void BelowThirtyTheOrderRepeatsAndTheLadderStops()
	{
		var (harness, boss, raid) = Engaged(Zapiel);
		using BossAiHarness _h = harness;

		Npc disciple = harness.Spawn(ZapielDisciple, 2930f, 2600f, 181f);
		BossAiHarness.MakeMutuallyKnown(boss, disciple);

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 30);
		Assert.Same(raid[0], disciple.GetTarget());

		// Sent somewhere else by hand, the loop brings it back on its next call.
		Aion.GameServer.Ai.NpcMessageBus.Broadcast(boss, DiscipleOfZapielAI.TakeThisOne, raid[2], 50f);
		Assert.Same(raid[2], disciple.GetTarget());

		Advance(harness, raid, boss, 35);
		Assert.Same(raid[0], disciple.GetTarget());
	}

	/// <summary>A disciple answers both of retail's orders, not only the band one.</summary>
	[Theory]
	[InlineData(DiscipleOfZapielAI.TakeThisOne)]
	[InlineData(DiscipleOfZapielAI.GoForThisOne)]
	public void ADiscipleAnswersEitherOrder(int message)
	{
		using BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(Zapiel, 2900f, 2570f, 181f);
		Npc disciple = harness.Spawn(ZapielDisciple, 2930f, 2600f, 181f);
		Player quarry = harness.SpawnPlayer(2910f, 2570f, 181f);
		BossAiHarness.MakeMutuallyKnown(caller, disciple);

		Assert.Null(disciple.GetTarget());

		Aion.GameServer.Ai.NpcMessageBus.Broadcast(caller, message, quarry, 50f);

		Assert.Same(quarry, disciple.GetTarget());
	}
}
