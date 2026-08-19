using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Drill instructor diplito's lich guard, which no data in this port ever spawned.
/// </summary>
/// <remarks>
/// Retail's <c>IDRaksha_Re_A_KJS</c> calls one guard per band, each exactly once — both rungs consume a
/// <c>set_flag_var</c>:
/// <list type="bullet">
/// <item>below 75 — one <b>855908</b>, the skeleton guard;</item>
/// <item>below 35 — one <b>855909</b>, the lich guard.</item>
/// </list>
/// Both at <c>spawn_range 5</c>.
/// <para>
/// Our block had a single band at 40 calling the skeleton at distance 2. So the lich guard never
/// appeared — and it is not a reskin: <c>855908</c> is melee with a 2m attack range, <c>855909</c> is a
/// caster with a 16m one and its own <c>npc_skills</c> row. The second half of this fight was a melee
/// add standing in for a caster.
/// </para>
/// <para>
/// Fourth summon-data row opened, fourth time the defect was a missing npc rather than the number the
/// row complained about. See <c>docs/retail-ai-fidelity.md</c>.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DrillInstructorDiplitoTests
{
	private const int RaksangRuins = 300610000;

	private const int Diplito = 236303;
	private const int SkeletonGuard = 855908;
	private const int LichGuard = 855909;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(RaksangRuins).WithWorldSize(2048)
			.WithAi(typeof(SummonerAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static void AtPercent(BossAiHarness harness, int percent)
	{
		Npc boss = harness.Spawn(Diplito, 400f, 400f, 200f);
		Player player = harness.SpawnPlayer(404f, 400f, 200f);
		harness.Engage(boss, player);
		BossAiHarness.SetExactPercent(boss, percent);
		boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, boss);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>The upper band brings the skeleton guard.</b> Retail's below-75 rung — ours fired at 40, so
	/// the first add arrived thirty-five points of health late.
	/// </summary>
	[Fact]
	public void TheUpperBandBringsTheSkeletonGuard()
	{
		using BossAiHarness harness = NewHarness();
		AtPercent(harness, 74);

		Assert.Equal(1, Count(harness, SkeletonGuard));
		Assert.Equal(0, Count(harness, LichGuard));
	}

	/// <summary>
	/// <b>The lower band brings the lich guard.</b> The npc that never existed in play.
	/// </summary>
	[Fact]
	public void TheLowerBandBringsTheLichGuard()
	{
		using BossAiHarness harness = NewHarness();
		AtPercent(harness, 34);

		Assert.Equal(1, Count(harness, LichGuard));
	}
}
