using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="PrincessKaremiwenAI"/>, translated from retail pattern
/// <c>ND2_WhF</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// One mechanic: a three-minute fuse written as three turns of a sixty-second timer, only the last of
/// which calls the maids. The pins are the timing, the once-only, and the fact that nothing arrives
/// early.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PrincessKaremiwenAiTests
{
	private const int AdmaStronghold = 320130000;
	private const int Karemiwen = 214695;
	private const int VampireMaid = 281051;
	private const int BansheeMaid = 281052;

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(AdmaStronghold).WithWorldSize(2048)
			.WithAi(typeof(PrincessKaremiwenAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(Karemiwen, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
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

	private static int Maids(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() is VampireMaid or BansheeMaid);

	/// <summary>
	/// Two turns of the timer pass with nothing to show for them. A port that spawned on the first
	/// tick would put the maids out at sixty seconds instead of a hundred and eighty.
	/// </summary>
	[Fact]
	public void NoMaidArrivesInTheFirstTwoMinutes()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 179);

		Assert.Equal(0, Maids(harness));
	}

	[Fact]
	public void BothMaidsArriveOnTheThirdTurnOfTheTimer()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 181);

		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == BansheeMaid));
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == VampireMaid));
	}

	/// <summary>
	/// The third branch does not re-arm, so the ladder is spent. Without that the timer would keep
	/// turning and a long fight would fill the room with maids.
	/// </summary>
	[Fact]
	public void TheLadderIsSpentAndNoMoreArrive()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;
		var seen = new HashSet<Npc>();

		for (int i = 0; i < 420; i++)
		{
			Advance(harness, boss, player, 1);
			foreach (Npc maid in harness.LiveNpcs()
				.Where(n => n.GetNpcId() is VampireMaid or BansheeMaid))
				seen.Add(maid);
		}

		Assert.Equal(2, seen.Count);
	}

	[Fact]
	public void TheMaidsStayFiveMinutes()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 181);
		List<Npc> arrived = harness.LiveNpcs()
			.Where(n => n.GetNpcId() is VampireMaid or BansheeMaid).ToList();
		Assert.Equal(2, arrived.Count);

		Advance(harness, boss, player, 296);
		Assert.All(arrived, m => Assert.True(m.IsSpawned(), "should still stand short of five minutes"));

		Advance(harness, boss, player, 6);
		Assert.All(arrived, m => Assert.False(m.IsSpawned(), "should have gone at five minutes"));
	}

	[Fact]
	public void DyingClearsTheMaids()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 181);
		Assert.Equal(2, Maids(harness));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Maids(harness));
	}

	[Fact]
	public void LeavingTheFightClearsThemAndResetsTheFuse()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 181);
		Assert.Equal(2, Maids(harness));

		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);
		Assert.Equal(0, Maids(harness));

		// A fresh pull starts the ladder over, which is what clearing the flags on reset is for.
		harness.Engage(boss, player);
		Advance(harness, boss, player, 179);
		Assert.Equal(0, Maids(harness));
		Advance(harness, boss, player, 2);
		Assert.Equal(2, Maids(harness));
	}
}
