using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Illusionmaster Sharik, who calls two healing statues to himself and called none.
/// </summary>
/// <remarks>
/// Retail's rung fires every thirty-seven seconds above half health and every thirty below it, despawns
/// the pair before it and spawns two more within two metres. They are bound here to <c>servant</c>,
/// which heals its master, so their absence made the fight materially easier — nothing had to be killed
/// to stop him healing.
/// <para>
/// Found by <c>audit_timer_drift.py</c>, which reported 3000/40000 against a pattern whose rungs are
/// 9000, 10000, 30000 and 37000.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class IllusionMasterSharikAiTests
{
	private const int RaksangRuins = 300610000;

	private const int Sharik = 217425;
	private const int StatueDispel = 282576;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(RaksangRuins).WithWorldSize(2048)
			.WithAi(typeof(IllusionMasterSharikAI), typeof(ServantNpcAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Engages him and drops him past the eighty per cent rung that starts his timer.</summary>
	private static Npc Fighting(BossAiHarness harness)
	{
		Npc sharik = harness.Spawn(Sharik, 737f, 290f, 911.9f);
		Player player = harness.SpawnPlayer(741f, 290f, 911.9f);
		harness.Engage(sharik, player);

		BossAiHarness.SetHpPercent(sharik, 79);
		sharik.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);
		return sharik;
	}

	/// <summary>
	/// <b>Nothing arrives for thirty-seven seconds, and then two statues do.</b>
	/// </summary>
	/// <remarks>
	/// The cadence was a flat forty seconds opening at three, so the first pair came far too early and
	/// every pair after it too late. Retail has no forty anywhere in this pattern.
	/// </remarks>
	[Fact]
	public void TwoStatuesArriveAtThirtySeven()
	{
		using BossAiHarness harness = NewHarness();
		Fighting(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(36));
		Assert.Equal(0, Count(harness, StatueDispel));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(2, Count(harness, StatueDispel));
	}

	/// <summary>
	/// <b>And the next turn replaces them rather than adding to them.</b>
	/// </summary>
	/// <remarks>
	/// Retail's rung opens with two despawns, so there are two statues at a time however long the fight
	/// runs.
	/// </remarks>
	[Fact]
	public void EachTurnReplacesThePairBeforeIt()
	{
		using BossAiHarness harness = NewHarness();
		Fighting(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(38));
		Assert.Equal(2, Count(harness, StatueDispel));

		harness.Clock.Advance(TimeSpan.FromSeconds(37));
		Assert.Equal(2, Count(harness, StatueDispel));

		harness.Clock.Advance(TimeSpan.FromSeconds(37));
		Assert.Equal(2, Count(harness, StatueDispel));
	}

	/// <summary>
	/// <b>Below half health the turn comes at thirty seconds instead.</b>
	/// </summary>
	/// <remarks>
	/// Retail switches rungs there — <c>BTIMERI_INDEX_0</c> at thirty rather than
	/// <c>BTIMERI_INDEX_3</c> at thirty-seven — so a wounded Sharik heals more often, not less.
	/// </remarks>
	[Fact]
	public void BelowHalfHealthTheTurnComesSooner()
	{
		using BossAiHarness harness = NewHarness();
		Npc sharik = Fighting(harness);

		BossAiHarness.SetHpPercent(sharik, 40);

		// Counted over a window rather than at one moment. The turn already ticking keeps the delay it
		// was armed with, so the difference between the two rungs only shows in how many turns fit: at
		// thirty seconds a hundred-second window holds three, at thirty-seven only two, and each turn
		// brings two statues.
		// Held down each second: a boss left alone regenerates, and once he crosses back over half health
		// the rung reverts -- which is what made the first version of this pin read four rather than six.
		BossAiHarness.Watched seen = harness.WatchNew(
			100, () => BossAiHarness.SetHpPercent(sharik, 40), StatueDispel);

		Assert.True(seen.Total >= 6,
			$"only {seen.Total} statues in a hundred seconds, which is the thirty-seven second rung");
	}
}
