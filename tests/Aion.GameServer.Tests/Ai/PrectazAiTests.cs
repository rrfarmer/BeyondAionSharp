using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="PrectazAI"/>, translated from retail pattern
/// <c>DF5_ItemNamed_24_SSH</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The substance is eight tentacles below 35 in a fixed arrangement, and the chain above 35 that has
/// to keep cycling for a boss fought down from full to ever reach them.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PrectazAiTests
{
	private const int Enshar = 220080000;
	private const int Prectaz = 219934;
	private const int CardinalTentacle = 855911;
	private const int DiagonalTentacle = 856067;

	private const float BossX = 300f;
	private const float BossY = 300f;

	private static (BossAiHarness, Npc, Player) Engaged(int hpPercent)
	{
		BossAiHarness harness = BossAiHarness.For(Enshar).WithWorldSize(2048)
			.WithAi(typeof(PrectazAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(Prectaz, BossX, BossY, 200f);
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

	private static List<Npc> Live(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	[Fact]
	public void AboveThirtyFiveNoTentacleAppears()
	{
		var (harness, boss, player) = Engaged(60);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 90);

		Assert.Empty(Live(harness, CardinalTentacle));
		Assert.Empty(Live(harness, DiagonalTentacle));
	}

	[Fact]
	public void BelowThirtyFiveEightTentaclesComeUp()
	{
		var (harness, boss, player) = Engaged(30);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 7);

		Assert.Equal(4, Live(harness, CardinalTentacle).Count);
		Assert.Equal(4, Live(harness, DiagonalTentacle).Count);
	}

	/// <summary>
	/// Retail's second copy of the summon swaps the two distances and carries identical guards, so it
	/// can never match. Eighteen on the cardinals, ten on the diagonals — not the other way round.
	/// </summary>
	[Fact]
	public void TheCardinalsStandAtEighteenAndTheDiagonalsAtTen()
	{
		var (harness, boss, player) = Engaged(30);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 7);

		foreach (Npc t in Live(harness, CardinalTentacle))
		{
			float dx = t.GetPosition().GetX() - BossX;
			float dy = t.GetPosition().GetY() - BossY;
			Assert.True((Math.Abs(dx) == 18f && dy == 0f) || (Math.Abs(dy) == 18f && dx == 0f),
				$"cardinal tentacle should sit 18m out on an axis, got ({dx}, {dy})");
		}

		foreach (Npc t in Live(harness, DiagonalTentacle))
		{
			float dx = t.GetPosition().GetX() - BossX;
			float dy = t.GetPosition().GetY() - BossY;
			Assert.True(Math.Abs(dx) == 10f && Math.Abs(dy) == 10f,
				$"diagonal tentacle should sit at 10m on both axes, got ({dx}, {dy})");
		}
	}

	[Fact]
	public void TheTentaclesLiveFiftySecondsAndGo()
	{
		var (harness, boss, player) = Engaged(30);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 7);
		List<Npc> first = Live(harness, CardinalTentacle);
		Assert.Equal(4, first.Count);

		Advance(harness, boss, player, 48);
		Assert.All(first, t => Assert.True(t.IsSpawned(), "should still stand a moment short of fifty"));

		Advance(harness, boss, player, 4);
		Assert.All(first, t => Assert.False(t.IsSpawned(), "should have gone once fifty seconds passed"));
	}

	/// <summary>
	/// The chains above 35 exist only to keep timer 0 cycling. Without them a boss pulled at full
	/// health never summons at all — which is every real pull.
	/// </summary>
	/// <remarks>
	/// The health drop has to come <i>after</i> a band has completed a full lap, or the low chain picks
	/// the sequence up mid-flight and the missing step never matters. Both of these ran green against a
	/// version with the looping step deleted until the timings were pushed out past one lap.
	/// </remarks>
	[Theory]
	[InlineData(95, 55)]
	[InlineData(60, 65)]
	public void ABossFoughtDownAfterAFullLapStillReachesItsTentacles(int startHp, int lapSeconds)
	{
		var (harness, boss, player) = Engaged(startHp);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, lapSeconds);
		Assert.Empty(Live(harness, CardinalTentacle));

		// The lap leaves timer 0 armed a few seconds out; twenty is enough to catch the summon and
		// well short of the tentacles' fifty-second life.
		BossAiHarness.SetHpPercent(boss, 30);
		Advance(harness, boss, player, 20);

		Assert.Equal(4, Live(harness, CardinalTentacle).Count);
	}

	/// <summary>
	/// The low chain is 25 + 10 + 14 + 10 + 14, so the tentacles come back at about seventy-nine
	/// seconds — long after the first set has expired at fifty-six. There is a real gap with none on
	/// the field, and shortening any step would close it.
	/// </summary>
	[Fact]
	public void TheTentaclesComeBackOnTheirOwnCycleAndNotSooner()
	{
		var (harness, boss, player) = Engaged(30);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 7);
		Assert.Equal(4, Live(harness, CardinalTentacle).Count);

		// First set gone at 56s, second not due until 79.
		Advance(harness, boss, player, 58);
		Assert.Empty(Live(harness, CardinalTentacle));

		Advance(harness, boss, player, 20);
		Assert.Equal(4, Live(harness, CardinalTentacle).Count);
	}

	[Fact]
	public void DyingClearsTheTentacles()
	{
		var (harness, boss, player) = Engaged(30);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 7);
		Assert.NotEmpty(Live(harness, CardinalTentacle));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Empty(Live(harness, CardinalTentacle));
		Assert.Empty(Live(harness, DiagonalTentacle));
	}

	[Fact]
	public void LeavingTheFightClearsThemToo()
	{
		var (harness, boss, player) = Engaged(30);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 7);
		Assert.NotEmpty(Live(harness, DiagonalTentacle));

		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Empty(Live(harness, DiagonalTentacle));
	}
}
