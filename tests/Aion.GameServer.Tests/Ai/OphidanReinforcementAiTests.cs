using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="OphidanReinforcementAI"/>, translated from retail patterns
/// <c>BIDF5_U1_SummonSupport_1</c> through <c>_4</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Four invisible posts, and between them the reason Ophidan Bridge is a race: a pair of beritran
/// every sixty seconds, five times, and then the post is spent. The cadence and the stop are what
/// these pin — retail expresses both through one counter whose guard we could not port as written.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class OphidanReinforcementAiTests
{
	private const int OphidanBridge = 300590000;

	private const int FirstPost = 284708;
	private const int ThirdPost = 284710;
	private const int FourthPost = 284711;

	private const int SupportA = 231184;
	private const int SupportB = 231185;
	private const int SupportWind = 231186;
	private const int SupportShadow = 231187;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(OphidanBridge).WithWorldSize(2048)
			.WithAi(typeof(OphidanReinforcementAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Standing(BossAiHarness harness, params int[] npcIds) =>
		harness.LiveNpcs().Count(n => npcIds.Contains(n.GetNpcId()));

	/// <summary>Nothing arrives in the first minute: retail's first wave is sixty seconds out.</summary>
	[Fact]
	public void NothingArrivesForTheFirstMinute()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(FirstPost, 720f, 460f, 600f);

		harness.Clock.Advance(TimeSpan.FromSeconds(55));

		Assert.Equal(0, Standing(harness, SupportA, SupportB));
	}

	/// <summary><b>Then a pair every sixty seconds.</b></summary>
	[Fact]
	public void ThenAPairEverySixtySeconds()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(FirstPost, 720f, 460f, 600f);

		harness.Clock.Advance(TimeSpan.FromSeconds(65));
		Assert.Equal(2, Standing(harness, SupportA, SupportB));

		harness.Clock.Advance(TimeSpan.FromSeconds(60));
		Assert.Equal(4, Standing(harness, SupportA, SupportB));

		harness.Clock.Advance(TimeSpan.FromSeconds(60));
		Assert.Equal(6, Standing(harness, SupportA, SupportB));
	}

	/// <summary>
	/// <b>Five pairs and no more.</b> Retail's counter stops at ten and the post has nothing left to
	/// send, however long the fight runs after it.
	/// </summary>
	[Fact]
	public void FivePairsAndThenThePostIsSpent()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(FirstPost, 720f, 460f, 600f);

		harness.Clock.Advance(TimeSpan.FromSeconds(320));
		Assert.Equal(10, Standing(harness, SupportA, SupportB));

		harness.Clock.Advance(TimeSpan.FromSeconds(600));
		Assert.Equal(10, Standing(harness, SupportA, SupportB));
	}

	/// <summary><b>On two fixed marks</b>, six metres apart, wherever the post itself stands.</summary>
	[Fact]
	public void OnTwoFixedMarks()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(FirstPost, 600f, 600f, 600f);

		harness.Clock.Advance(TimeSpan.FromSeconds(65));

		Assert.Contains(harness.LiveNpcs(),
			n => n.GetNpcId() == SupportA && Math.Abs(n.GetY() - 457f) < 1f);
		Assert.Contains(harness.LiveNpcs(),
			n => n.GetNpcId() == SupportB && Math.Abs(n.GetY() - 463f) < 1f);
	}

	/// <summary>
	/// <b>Each post calls its own kinds.</b> The third sends a wind beritran with an ordinary one, and
	/// the fourth sends two shadows — which is what makes this a table rather than one pattern.
	/// </summary>
	[Fact]
	public void EachPostCallsItsOwnKinds()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(ThirdPost, 645f, 540f, 594f);
		harness.Spawn(FourthPost, 450f, 496f, 603f);

		harness.Clock.Advance(TimeSpan.FromSeconds(65));

		Assert.Equal(1, Standing(harness, SupportWind));
		Assert.Equal(1, Standing(harness, SupportA));
		Assert.Equal(2, Standing(harness, SupportShadow));
		Assert.Equal(0, Standing(harness, SupportB));
	}

	/// <summary>And a post that goes takes everything it called with it.</summary>
	[Fact]
	public void DespawningThePostTakesThemWithIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc post = harness.Spawn(FirstPost, 720f, 460f, 600f);

		harness.Clock.Advance(TimeSpan.FromSeconds(125));
		Assert.Equal(4, Standing(harness, SupportA, SupportB));

		post.GetAi().OnGeneralEvent(AiEventType.Despawned);

		Assert.Equal(0, Standing(harness, SupportA, SupportB));
	}
}
