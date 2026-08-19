using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Head kutol's five waves, three of which were wrong.
/// </summary>
/// <remarks>
/// Retail's <c>IDForest_ManduriT_Leaf_Named</c> has five bands — 90, 75, 60, 45, 30 — and each brings
/// <b>one</b> counter npc and a growing number of <c>Sum1</c>: 1+2, 1+2, 1+3, 1+4, and then twenty
/// <c>Sum2</c> at thirty.
/// <para>
/// The 60 and 45 bands were already right. The top two had the counts <b>the wrong way round</b> — two
/// counters and one Sum1 — the second fired at 70 rather than 75, and the finale placed five where
/// retail places twenty: <b>a quarter of the wave the fight ends on</b>.
/// </para>
/// <para>
/// <b>Not expressible in this schema:</b> retail hangs every band on <c>on_arrived_at_waypoint</c> as
/// well as the health threshold, so a band only opens when he reaches a point on his round. Ours fire
/// the moment the threshold is crossed. Recorded in docs/retail-ai-fidelity.md rather than papered over.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class HeadKutolAiTests
{
	private const int TheForest = 320070000;

	private const int HeadKutol = 217277;
	private const int Attendant = 282302;   // Sum1
	private const int Swarm = 282303;       // Sum2
	private const int CounterTwo = 282304;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TheForest).WithWorldSize(2048)
			.WithAi(typeof(SummonerAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static void AtPercent(BossAiHarness harness, int percent)
	{
		Npc kutol = harness.Spawn(HeadKutol, 500f, 500f, 200f);
		Player player = harness.SpawnPlayer(504f, 500f, 200f);
		harness.Engage(kutol, player);
		BossAiHarness.SetExactPercent(kutol, percent);
		kutol.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, kutol);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>The first band brings one counter and two attendants, not the other way round.</b>
	/// </summary>
	[Fact]
	public void TheFirstBandBringsOneCounterAndTwoAttendants()
	{
		using BossAiHarness harness = NewHarness();
		AtPercent(harness, 89);

		Assert.Equal(1, Count(harness, CounterTwo));
		Assert.Equal(2, Count(harness, Attendant));
	}

	/// <summary>
	/// <b>The second band opens at seventy-five, not seventy.</b>
	/// </summary>
	/// <remarks>
	/// Measured as the <i>delta</i> across the threshold, because bands accumulate: dropping straight to
	/// 74 opens the 90 band and the 75 band together, so an absolute count there says nothing about
	/// where the second one starts. Stepping 89 to 74 and asking what was added does.
	/// <para>
	/// With the old 70, nothing is added at 74 and this fails — which is the whole defect: between 75
	/// and 71 he brought nothing where retail brings a wave.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheSecondBandOpensAtSeventyFive()
	{
		using BossAiHarness harness = NewHarness();
		Npc kutol = harness.Spawn(HeadKutol, 500f, 500f, 200f);
		Player player = harness.SpawnPlayer(504f, 500f, 200f);
		harness.Engage(kutol, player);

		BossAiHarness.SetExactPercent(kutol, 89);
		kutol.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, kutol);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		int countersAfterFirst = Count(harness, CounterTwo);
		int attendantsAfterFirst = Count(harness, Attendant);

		BossAiHarness.SetExactPercent(kutol, 74);
		kutol.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, kutol);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(1, Count(harness, CounterTwo) - countersAfterFirst);
		Assert.Equal(2, Count(harness, Attendant) - attendantsAfterFirst);
	}

	/// <summary>
	/// <b>The finale is twenty.</b> The single largest number in this encounter and it was five.
	/// </summary>
	[Fact]
	public void TheFinaleIsTwenty()
	{
		using BossAiHarness harness = NewHarness();
		AtPercent(harness, 29);

		Assert.Equal(20, Count(harness, Swarm));
	}
}
