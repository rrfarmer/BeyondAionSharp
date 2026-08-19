using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Mistress Viloa's three nightmares, which arrived five seconds late and huddled too close.
/// </summary>
/// <remarks>
/// Retail's <c>IDAsteria_IU_world_2Stage_Boss</c> spawns them on <c>on_enter_attack_state</c>, once
/// (<c>set_flag_var</c>), <c>num_to_spawn=3</c>, <c>spawn_range=5</c>, <c>live_time=0</c>, and with no
/// timer of any kind: the three are on the floor as she is pulled.
/// <para>
/// Our <c>spawn_helpers.xml</c> had the count right and the other two wrong — <c>distance="3"</c> and
/// <c>schedule="5000"</c>. The mechanic existed, which is why nothing flagged it until the ranked audit
/// asked whether the boss names every id its pattern spawns.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class MistressViloaAiTests
{
	private const int NightmareCircus = 301240000;

	private const int Viloa = 233459;
	private const int PrimalNightmare = 233456;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(NightmareCircus).WithWorldSize(2048)
			.WithAi(typeof(MistressViloaAI), typeof(SummonerAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI), typeof(AggressiveNoLootNpcAI))
			.Build();

	private static int Nightmares(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == PrimalNightmare);

	private static Npc Pulled(BossAiHarness harness)
	{
		Npc viloa = harness.Spawn(Viloa, 500f, 500f, 200f);
		Player player = harness.SpawnPlayer(504f, 500f, 200f);
		harness.Engage(viloa, player);
		return viloa;
	}

	/// <summary>
	/// <b>All three are there within a second of the pull.</b>
	/// </summary>
	/// <remarks>
	/// Retail hangs this on <c>on_enter_attack_state</c> with no timer at all. <c>SummonerAI</c> always
	/// routes a summon group through the scheduler, so a <c>schedule</c> of zero is immediate in
	/// production but still needs one tick of the harness clock — hence the second here rather than an
	/// assertion at zero. What the second measures is real: with the old <c>schedule="5000"</c> this
	/// finds nothing.
	/// </remarks>
	[Fact]
	public void TheThreeNightmaresArriveWithThePull()
	{
		using BossAiHarness harness = NewHarness();
		Pulled(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(3, Nightmares(harness));
	}

	/// <summary>
	/// <b>And no more arrive later.</b> Retail's rung is guarded by <c>set_flag_var</c>, so it fires once
	/// however long the fight runs.
	/// </summary>
	[Fact]
	public void NoFurtherNightmaresArriveLater()
	{
		using BossAiHarness harness = NewHarness();
		Npc viloa = Pulled(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		for (int blow = 0; blow < 8; blow++)
			viloa.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, viloa);
		harness.Clock.Advance(TimeSpan.FromSeconds(30));

		Assert.Equal(3, Nightmares(harness));
	}
}
