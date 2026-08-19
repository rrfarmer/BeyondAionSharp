using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Queen Alukina's servants, which arrived in the wrong place, and her blobbles, which never arrived.
/// </summary>
/// <remarks>
/// See <see cref="QueenAlukinaBeluslanAI"/> for the translation. The two defects pinned here are independent:
/// the servant landed at distance 10 from the queen instead of on the player she was fighting, and the
/// seven death blobbles could not be expressed by the <c>&lt;summons&gt;</c> schema at all.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class QueenAlukinaBeluslanAiTests
{
	private const int Beluslan = 220040000;

	private const int Alukina = 213747;
	private const int FaithfulServant = 280712;
	private const int AzureBlobble = 280713;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Beluslan).WithWorldSize(4096)
			.WithAi(typeof(QueenAlukinaBeluslanAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (Npc Queen, Player Target) Engaged(BossAiHarness harness, int percent)
	{
		Npc queen = harness.Spawn(Alukina, 400f, 400f, 200f);
		// Deliberately far from the queen: the whole point of spawn_on_target is that the servant
		// appears by the player, so a target standing next to her could not tell the two apart.
		Player target = harness.SpawnPlayer(430f, 400f, 200f);
		harness.Engage(queen, target);
		BossAiHarness.SetExactPercent(queen, percent);
		return (queen, target);
	}

	private static void Advance(BossAiHarness harness, Npc queen, Player target, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(queen, target);
			BossAiHarness.KeepAlive(target);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static List<Npc> Of(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	/// <summary>
	/// <b>The servant lands on the player, not on the queen.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>spawn_on_target ... spawn_range=2</c>. This is the pin that the old data could never
	/// have passed: it placed the servant at <c>distance="10"</c> from the queen, thirty metres from the
	/// player standing here.
	/// </remarks>
	[Fact]
	public void TheServantLandsOnWhoeverSheIsFighting()
	{
		using BossAiHarness harness = NewHarness();
		(Npc queen, Player target) = Engaged(harness, 40);

		// Sixty seconds, not forty: the loop's second beat (timer 5, armed at twenty-five by the first)
		// does not come round inside forty, and a mutation moving that branch back to the queen's feet
		// survived a shorter window.
		Advance(harness, queen, target, 60);

		List<Npc> servants = Of(harness, FaithfulServant);
		Assert.NotEmpty(servants);
		foreach (Npc servant in servants)
		{
			double toPlayer = Distance(servant, target.GetX(), target.GetY());
			Assert.True(toPlayer <= 3.0,
				$"servant arrived {toPlayer:F1}m from the player it was supposed to land on");
		}
	}

	/// <summary>
	/// <b>One at a time.</b> Retail's rungs all carry <c>num_to_spawn=1</c>; ours placed three at once.
	/// </summary>
	[Fact]
	public void TheServantsArriveOneAtATime()
	{
		using BossAiHarness harness = NewHarness();
		(Npc queen, Player target) = Engaged(harness, 40);

		Advance(harness, queen, target, 11);

		Assert.Equal(1, Of(harness, FaithfulServant).Count);
	}

	/// <summary>
	/// <b>Below twenty-five she stops summoning.</b> No spawn rung passes under 27, and the rung that
	/// crosses twenty-five clears the group.
	/// </summary>
	/// <remarks>
	/// The three-band ladder inverted this: it summoned most at 40 and kept going to the floor.
	/// </remarks>
	[Fact]
	public void BelowTwentyFiveTheServantsStop()
	{
		using BossAiHarness harness = NewHarness();
		(Npc queen, Player target) = Engaged(harness, 40);
		Advance(harness, queen, target, 40);
		Assert.NotEmpty(Of(harness, FaithfulServant));

		BossAiHarness.SetExactPercent(queen, 20);
		Advance(harness, queen, target, 40);

		Assert.Empty(Of(harness, FaithfulServant));
	}

	/// <summary>
	/// <b>Seven azure blobbles when she dies.</b> The mechanic the data schema could not hold.
	/// </summary>
	/// <remarks>
	/// <b>Raised as an AI event rather than through <c>BossAiHarness.Kill</c>, and that is not laziness.</b>
	/// <c>Kill</c> reaches death observers — it was added so the captured Drakan scientists could be
	/// released by counting two eyes — but it does not reach the dying NPC's own <c>HandleDied</c>, so no
	/// death branch of any pattern runs through it. Reproduced against the pre-existing
	/// <see cref="QueenAlukinaAI"/>, which bursts the same seven blobbles from a hand-written
	/// <c>HandleDied</c>: through <c>Kill</c> it produces none, and raising <c>Died</c> on the same
	/// already-dead NPC produces all seven. That is recorded in docs/retail-ai-fidelity.md as work owed
	/// on the harness; this pin uses the seam that works and says so rather than appearing to test more
	/// than it does.
	/// </remarks>
	[Fact]
	public void SevenBlobblesArriveWhenSheDies()
	{
		using BossAiHarness harness = NewHarness();
		(Npc queen, Player target) = Engaged(harness, 30);

		queen.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Equal(7, Of(harness, AzureBlobble).Count);
	}

	/// <summary>
	/// <b>And they arrive at her, not at the player.</b> Retail's death rung is
	/// <c>SPAWN_LOCATION_MY_POINT</c> where every servant rung is on the target, so a translation that
	/// reused the wrong one would still put seven blobbles down and put them in the wrong room.
	/// </summary>
	[Fact]
	public void TheBlobblesArriveWhereSheFell()
	{
		using BossAiHarness harness = NewHarness();
		(Npc queen, Player target) = Engaged(harness, 30);
		float x = queen.GetX();
		float y = queen.GetY();

		queen.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		foreach (Npc blobble in Of(harness, AzureBlobble))
			Assert.True(Distance(blobble, x, y) <= 10.0,
				"a blobble arrived away from where the queen fell");
	}

	private static double Distance(Npc npc, float x, float y)
	{
		double dx = npc.GetX() - x;
		double dy = npc.GetY() - y;
		return Math.Sqrt((dx * dx) + (dy * dy));
	}
}
