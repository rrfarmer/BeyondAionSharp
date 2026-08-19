using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The arena saam, which was given four health bands it does not have.
/// </summary>
/// <remarks>
/// See <see cref="ArenaSaamAI"/>. Retail's whole mechanic is one <c>on_attacked</c> rung on a coin
/// flip: shed one piece ten metres away, count it, and flee for five seconds. Our data had four health
/// bands each placing two, which shares nothing with retail but the npc id.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ArenaSaamAiTests
{
	private const int ArenaOfChaos = 300350000;

	private const int Saam = 217737;
	private const int CutSaam = 217738;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(ArenaOfChaos).WithWorldSize(2048)
			.WithAi(typeof(ArenaSaamAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// Puts it in a fight and reports how many pieces are already down.
	/// </summary>
	/// <remarks>
	/// <b>Engaging is itself hitting it</b>, and how many attack events that delivers is the harness's
	/// business rather than this encounter's. Every pin here measures the <i>delta</i> across its own
	/// hits for that reason; counting absolutely read six for four hits.
	/// </remarks>
	private static (BossAiHarness, Npc, Player, int) Fighting()
	{
		BossAiHarness harness = NewHarness();
		Npc saam = harness.Spawn(Saam, 500f, 500f, 200f);
		Player player = harness.SpawnPlayer(504f, 500f, 200f);
		harness.Engage(saam, player);
		return (harness, saam, player, harness.LiveNpcs().Count(n => n.GetNpcId() == CutSaam));
	}

	private static int Pieces(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == CutSaam);

	/// <summary>
	/// <b>Being hit sheds exactly one piece.</b> Not two, and not on a health threshold.
	/// </summary>
	/// <remarks>
	/// The harness forces rolled guards to pass, so the coin flip is deterministic here — see
	/// <c>BossAiHarness.Deterministic</c>. What is pinned is the count and the trigger, which is what
	/// the old data got wrong in both directions.
	/// </remarks>
	[Fact]
	public void BeingHitShedsOnePiece()
	{
		(BossAiHarness harness, Npc saam, Player player, int before) = Fighting();
		using BossAiHarness _ = harness;

		saam.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);

		Assert.Equal(1, Pieces(harness) - before);
	}

	/// <summary>
	/// <b>Every hit sheds another.</b> Retail has no once-only guard here — the rung is a plain roll, so
	/// the round's score is however many times you connect.
	/// </summary>
	[Fact]
	public void EveryHitShedsAnother()
	{
		(BossAiHarness harness, Npc saam, Player player, int before) = Fighting();
		using BossAiHarness _ = harness;

		for (int i = 0; i < 4; i++)
			saam.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);

		Assert.Equal(4, Pieces(harness) - before);
	}

	/// <summary>
	/// <b>And it counts what it has shed.</b> Retail's <c>increase_intvar</c> is the bonus round's score.
	/// </summary>
	[Fact]
	public void ItCountsThePiecesItHasShed()
	{
		(BossAiHarness harness, Npc saam, Player player, int before) = Fighting();
		using BossAiHarness _ = harness;

		for (int i = 0; i < 3; i++)
			saam.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);

		Assert.Equal(3, ((PatternAi)saam.GetAi()).Counter(0) - before);
	}

	/// <summary>
	/// <b>Its pieces go when it dies.</b> Retail's <c>on_die</c> despawns the group.
	/// </summary>
	[Fact]
	public void ItsPiecesGoWhenItDies()
	{
		(BossAiHarness harness, Npc saam, Player player, int before) = Fighting();
		using BossAiHarness _ = harness;
		saam.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);
		Assert.True(Pieces(harness) > 0, "nothing was shed to despawn");

		BossAiHarness.Kill(saam, player);

		Assert.Equal(0, Pieces(harness));
	}
}
