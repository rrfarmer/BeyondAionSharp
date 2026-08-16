using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="GatekeeperFloxAI"/>, translated from retail pattern
/// <c>LF5_ItemNamed_24_KJS</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The whole substance of this port is two eyes: one between 51 and 75, one below 25, each at one of
/// four cardinal points. Retail writes four branches per eye sharing one flag, which is easy to read as
/// four spawns — so the count and the one-shot are what these pins guard hardest.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GatekeeperFloxAiTests
{
	private const int Cygnea = 210070000;
	private const int Flox = 235975;
	private const int WatchingEye = 855728;

	private const float BossX = 300f;
	private const float BossY = 300f;

	private static (BossAiHarness, Npc, Player) Engaged(int hpPercent)
	{
		BossAiHarness harness = BossAiHarness.For(Cygnea).WithWorldSize(2048)
			.WithAi(typeof(GatekeeperFloxAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(Flox, BossX, BossY, 200f);
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

	private static List<Npc> Eyes(BossAiHarness harness) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == WatchingEye).ToList();

	[Fact]
	public void AboveSeventyFiveNoEyeAppears()
	{
		var (harness, boss, player) = Engaged(90);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 60);

		Assert.Empty(Eyes(harness));
	}

	[Fact]
	public void BetweenFiftyOneAndSeventyFiveExactlyOneEyeAppears()
	{
		var (harness, boss, player) = Engaged(60);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 6);

		Assert.Single(Eyes(harness));
	}

	/// <summary>
	/// The four placement branches share one flag, so the phase puts out one eye and then stops. A
	/// table that spawned all four would put eight eyes out over a fight where retail puts two.
	/// </summary>
	[Fact]
	public void ThatEyeIsAOneShotForTheWholePhase()
	{
		var (harness, boss, player) = Engaged(60);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 6);
		Assert.Single(Eyes(harness));

		// Several more full loops of the 51-75 chain: 5 + 10 + 15 + 10 is a lap, so this is three.
		Advance(harness, boss, player, 130);

		Assert.Single(Eyes(harness));
	}

	[Fact]
	public void TheEyeStandsTwentyMetresOutOnACardinalPoint()
	{
		var (harness, boss, player) = Engaged(60);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 6);

		Npc eye = Assert.Single(Eyes(harness));
		float dx = eye.GetPosition().GetX() - BossX;
		float dy = eye.GetPosition().GetY() - BossY;

		// Exactly one axis carries the offset, and it is twenty metres either way.
		Assert.True((Math.Abs(dx) == 20f && dy == 0f) || (Math.Abs(dy) == 20f && dx == 0f),
			$"expected a cardinal offset of 20m, got ({dx}, {dy})");
	}

	/// <summary>
	/// The low phase has its own flag, so a boss fought all the way down puts out a second eye even
	/// though the first phase already spent its own.
	/// </summary>
	[Fact]
	public void TheLowPhaseAddsASecondEyeOnItsOwnFlag()
	{
		var (harness, boss, player) = Engaged(60);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 6);
		Assert.Single(Eyes(harness));

		BossAiHarness.SetHpPercent(boss, 20);
		Advance(harness, boss, player, 45);

		Assert.Equal(2, Eyes(harness).Count);
	}

	[Fact]
	public void TheChainKeepsComingBackToTimerZero()
	{
		// Straight into the low band. The eye there is only reachable if timer 0 comes round again
		// after the first pass, which is what the phase chain and the catch-all exist to do.
		var (harness, boss, player) = Engaged(20);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 6);
		Assert.Single(Eyes(harness));
	}

	/// <summary>
	/// The band between the two eye phases puts nothing out. Without this, widening the mid eye's band
	/// downwards goes unnoticed — the pins either side of it are at 90 and at 60.
	/// </summary>
	[Fact]
	public void TheBandBetweenTheTwoEyePhasesPutsNothingOut()
	{
		var (harness, boss, player) = Engaged(40);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 60);

		Assert.Empty(Eyes(harness));
	}

	/// <summary>
	/// At full health no phase owns timer 0, so only the catch-all brings the loop back round. Without
	/// it the chain dies on its first tick and a boss fought down from full never reaches either eye —
	/// which is the case that matters, since nobody pulls a world boss at 60%.
	/// </summary>
	[Fact]
	public void AHealthyBossFoughtDownStillReachesItsEye()
	{
		var (harness, boss, player) = Engaged(90);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 20);
		Assert.Empty(Eyes(harness));

		BossAiHarness.SetHpPercent(boss, 60);
		Advance(harness, boss, player, 40);

		Assert.Single(Eyes(harness));
	}

	[Fact]
	public void DyingClearsTheEyes()
	{
		var (harness, boss, player) = Engaged(60);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 6);
		Assert.NotEmpty(Eyes(harness));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Empty(Eyes(harness));
	}

	[Fact]
	public void LeavingTheFightClearsThemToo()
	{
		var (harness, boss, player) = Engaged(60);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 6);
		Assert.NotEmpty(Eyes(harness));

		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Empty(Eyes(harness));
	}
}
