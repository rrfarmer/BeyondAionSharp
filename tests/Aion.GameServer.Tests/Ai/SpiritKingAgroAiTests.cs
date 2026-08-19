using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Spirit King Agro's two underling types, one of which never appeared.
/// </summary>
/// <remarks>
/// Retail's <c>ND2_PeC</c> summons a different underling depending on health:
/// <list type="bullet">
/// <item>between 30 and 75 — two of <b>280772</b>, from the rung on <c>BTIMERI_INDEX_0</c>;</item>
/// <item>below 30 — two of <b>280771</b>, whose timer is armed only by a rung guarded on
/// <c>is_hp_lower_than 30</c>.</item>
/// </list>
/// Both at <c>spawn_range 6</c>.
/// <para>
/// Our <c>spawn_helpers.xml</c> summoned 280771 in both bands at distance 10, so <b>280772 never
/// appeared</b> — it exists in <c>npc_templates.xml</c> with its own <c>npc_skills</c> and nothing in the
/// port ever spawned it — and the mid-fight underlings were the wrong ones, spread four metres too wide.
/// </para>
/// <para>
/// The second row found by <c>audit_summon_numbers.py</c>, and the second of that pair to turn out to be
/// a missing npc rather than the number the row complained about.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SpiritKingAgroAiTests
{
	private const int Morheim = 220020000;

	private const int Agro = 211908;

	/// <summary>Retail's two tiers, written out rather than read from the data file.</summary>
	private const int MidBandUnderling = 280772;
	private const int LowBandUnderling = 280771;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Morheim).WithWorldSize(4096)
			.WithAi(typeof(SummonerAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static Npc AtPercent(BossAiHarness harness, int percent)
	{
		Npc agro = harness.Spawn(Agro, 500f, 500f, 200f);
		Player player = harness.SpawnPlayer(504f, 500f, 200f);
		harness.Engage(agro, player);
		BossAiHarness.SetExactPercent(agro, percent);
		agro.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, agro);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		return agro;
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>The mid band brings 280772.</b> This is the npc that never existed in play: our data summoned
	/// 280771 here, which is the below-thirty tier.
	/// </summary>
	[Fact]
	public void TheMidBandBringsTheSecondUnderling()
	{
		using BossAiHarness harness = NewHarness();
		AtPercent(harness, 74);

		Assert.Equal(2, Count(harness, MidBandUnderling));
		Assert.Equal(0, Count(harness, LowBandUnderling));
	}

	/// <summary>
	/// <b>And below thirty brings the first.</b> Retail arms that timer only from a rung guarded on
	/// <c>is_hp_lower_than 30</c>, so the two tiers do not overlap.
	/// </summary>
	[Fact]
	public void BelowThirtyBringsTheFirstUnderling()
	{
		using BossAiHarness harness = NewHarness();
		AtPercent(harness, 29);

		Assert.Equal(2, Count(harness, LowBandUnderling));
	}
}
