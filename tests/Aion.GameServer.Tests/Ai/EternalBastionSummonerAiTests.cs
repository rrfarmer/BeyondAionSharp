using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The Eternal Bastion's summoner, which never summoned.
/// </summary>
/// <remarks>
/// The class overrode <c>Ask</c> and nothing else. Retail's <c>IDF5_TD_Nor_Pr</c> gives it one rung, on
/// <c>on_attacked</c> and again on <c>on_spelled</c>: below fifty per cent, once, one revitalizing
/// servant (284441) at its own point within five metres.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class EternalBastionSummonerAiTests
{
	private const int EternalBastion = 301500000;

	private const int Summoner = 231128;

	/// <summary>Retail's ids and threshold, written out rather than read from the class under test.</summary>
	private const int Servant = 284441;
	private const int Threshold = 50;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(EternalBastion).WithWorldSize(2048)
			.WithAi(typeof(EternalBastionSummonerAI), typeof(SummonerAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI), typeof(ServantNpcAI))
			.Build();

	private static Npc Engaged(BossAiHarness harness)
	{
		Npc summoner = harness.Spawn(Summoner, 500f, 500f, 100f);
		Player player = harness.SpawnPlayer(504f, 500f, 100f);
		harness.Engage(summoner, player);
		return summoner;
	}

	private static int Servants(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == Servant);

	private static void Struck(Npc npc) =>
		npc.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, npc);

	/// <summary>
	/// <b>Above half health he calls nobody.</b> Retail's guard is <c>is_hp_lower_than 50</c>, so fifty
	/// itself is not below fifty.
	/// </summary>
	[Fact]
	public void AboveHalfHealthHeCallsNobody()
	{
		using BossAiHarness harness = NewHarness();
		Npc summoner = Engaged(harness);

		BossAiHarness.SetExactPercent(summoner, Threshold);
		Struck(summoner);

		Assert.Equal(0, Servants(harness));
	}

	/// <summary>
	/// <b>Below half he calls one servant.</b>
	/// </summary>
	[Fact]
	public void BelowHalfHeCallsOneServant()
	{
		using BossAiHarness harness = NewHarness();
		Npc summoner = Engaged(harness);

		BossAiHarness.SetExactPercent(summoner, Threshold - 1);
		Struck(summoner);

		Assert.Equal(1, Servants(harness));
	}

	/// <summary>
	/// <b>And only one, however long the fight runs.</b> Retail pairs the threshold with
	/// <c>set_flag_var</c>, which is test-and-set. Without that this rung fires on every blow below half
	/// and one add becomes a stream — the same defect shape as Laksyaka's three-per-cent roll.
	/// </summary>
	[Fact]
	public void HeCallsOnlyOneHoweverLongTheFightRuns()
	{
		using BossAiHarness harness = NewHarness();
		Npc summoner = Engaged(harness);
		BossAiHarness.SetExactPercent(summoner, Threshold - 1);

		for (int blow = 0; blow < 12; blow++)
			Struck(summoner);

		Assert.Equal(1, Servants(harness));
	}

	/// <summary>
	/// <b>Being spelled calls him too.</b> Retail hangs the same rung on <c>on_spelled</c>, and a caster
	/// group that never lands a physical blow would otherwise never see the servant.
	/// </summary>
	[Fact]
	public void BeingSpelledCallsTheServantAsWell()
	{
		using BossAiHarness harness = NewHarness();
		Npc summoner = Engaged(harness);

		BossAiHarness.SetExactPercent(summoner, Threshold - 1);
		summoner.GetAi().OnEffectApplied(BossAiHarness.EffectOf(summoner, summoner, 8291));

		Assert.Equal(1, Servants(harness));
	}
}
