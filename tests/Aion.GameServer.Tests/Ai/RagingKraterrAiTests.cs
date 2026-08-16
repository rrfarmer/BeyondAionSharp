using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="RagingKraterrAI"/>, translated from retail pattern <c>ND2_ElementalSu</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The fire half of <see cref="ElementalSummonerPattern"/>, and the sender
/// <see cref="ElementalWaveAI"/> shipped without. He ran on <c>summoner</c>, whose table called one
/// elemental at three wrong thresholds; these pins state what the pattern says instead.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class RagingKraterrAiTests
{
	private const int Beluslan = 220040000;

	private const int Kraterr = 211715;
	private const int KraterrTwin = 280332;

	private const int FirstWave = 280333;
	private const int SecondWave = 280334;
	private const int ThirdWave = 280335;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Beluslan).WithWorldSize(2048)
			.WithAi(typeof(RagingKraterrAI), typeof(ElementalWaveAI), typeof(AggressiveNpcAI))
			.Build();

	private static (BossAiHarness, Npc, Player) Engaged(int npcId = Kraterr)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(npcId, 300f, 300f, 200f);
		// Forty-five metres: outside what an elemental can see, inside his fifty-metre order.
		Player quarry = harness.SpawnPlayer(345f, 300f, 200f);
		harness.Engage(boss, quarry);
		return (harness, boss, quarry);
	}

	private static void Advance(BossAiHarness harness, Npc boss, Player quarry, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, quarry);
			BossAiHarness.KeepAlive(quarry);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>A different elemental per wave, four at a time.</b> The summon table he ran on called
	/// 280333 for all three and between two and five of them; retail calls each of the three in turn,
	/// exactly four, and takes the previous wave away as the next arrives.
	/// </summary>
	[Fact]
	public void EachBandCallsItsOwnElemental()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 80);
		Advance(harness, boss, quarry, 12);
		Assert.Equal(4, Count(harness, FirstWave));
		Assert.Equal(0, Count(harness, SecondWave));

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, boss, quarry, 10);
		Assert.Equal(0, Count(harness, FirstWave));
		Assert.Equal(4, Count(harness, SecondWave));

		BossAiHarness.SetExactPercent(boss, 30);
		Advance(harness, boss, quarry, 10);
		Assert.Equal(0, Count(harness, SecondWave));
		Assert.Equal(4, Count(harness, ThirdWave));
	}

	/// <summary>Above ninety no band matches, so the fallback runs and he calls nobody.</summary>
	[Fact]
	public void AboveNinetyHeCallsNobody()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 95);
		Advance(harness, boss, quarry, 60);

		Assert.Equal(0, Count(harness, FirstWave));
	}

	/// <summary>
	/// <b>Below twenty he summons nothing.</b> The deepest rung is casts only, and it still consumes
	/// the tick — a boss dropped straight past every band gets no elementals at all, where the summon
	/// table gave him a wave at twenty-five percent.
	/// </summary>
	[Fact]
	public void BelowTwentyHeCallsNobodyEither()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 15);
		Advance(harness, boss, quarry, 60);

		Assert.Equal(0, Count(harness, FirstWave));
		Assert.Equal(0, Count(harness, SecondWave));
		Assert.Equal(0, Count(harness, ThirdWave));
	}

	/// <summary>Ten minutes, which is retail's <c>live_time</c> on all three waves.</summary>
	[Fact]
	public void AWaveStandsForTenMinutes()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 80);
		Advance(harness, boss, quarry, 12);
		Assert.Equal(4, Count(harness, FirstWave));

		Advance(harness, boss, quarry, 580);
		Assert.Equal(4, Count(harness, FirstWave));

		Advance(harness, boss, quarry, 30);
		Assert.Equal(0, Count(harness, FirstWave));
	}

	/// <summary>
	/// <b>And the wave arrives on the player he names.</b> The order half, driven through him: a
	/// stand-in known to him beforehand hears the broadcast his summoning rung sends. See
	/// <see cref="ElementalWaveAiTests"/> for why the listener cannot be one of the four he places.
	/// </summary>
	[Fact]
	public void HisWaveArrivesOnThePlayerHeNames()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		Npc standIn = harness.Spawn(FirstWave, 304f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, standIn);

		BossAiHarness.SetExactPercent(boss, 80);
		Advance(harness, boss, quarry, 12);

		Assert.Same(quarry, standIn.GetTarget());
	}

	/// <summary>Dying clears every wave, as retail's <c>on_killed_by_user</c> does.</summary>
	[Fact]
	public void DyingClearsEveryWave()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 80);
		Advance(harness, boss, quarry, 12);
		Assert.Equal(4, Count(harness, FirstWave));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Equal(0, Count(harness, FirstWave));
	}

	/// <summary>Both ids retail binds to the pattern run it, not only the one the world places.</summary>
	[Fact]
	public void HisSummonedTwinRunsTheSameFight()
	{
		var (harness, boss, quarry) = Engaged(KraterrTwin);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 80);
		Advance(harness, boss, quarry, 12);

		Assert.Equal(4, Count(harness, FirstWave));
	}
}
