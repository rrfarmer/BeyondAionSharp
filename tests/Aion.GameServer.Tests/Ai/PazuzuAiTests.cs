using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pazuzu's water worms, which used to arrive once and then stand in the room for the rest of the fight.
/// </summary>
/// <remarks>
/// Retail <c>IDAbRe_Core_NamedC</c> gives them <c>live_time</c> 71 and re-arms the summoning branch at 72
/// seconds. This class had neither, and the <b>"only if none are standing" guard</b> it used instead is
/// what made that expensive: with worms that never die the guard never passes again, so <b>the cycle ran
/// exactly once per fight.</b>
/// <para>
/// Retail also rolls 30 percent and splits the branch across HP bands. This class models neither, so
/// these pins are about the lifetime and the rhythm only.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PazuzuAiTests
{
	private const int AbyssalSplinter = 300220000;
	private const int Pazuzu = 216951;
	private const int WaterWorm = 281909;

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(AbyssalSplinter).WithWorldSize(2048)
			.WithAi(typeof(PazuzuAI), typeof(UnstablePazuzuWormAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI)).Build();
		Npc boss = harness.Spawn(Pazuzu, 669.5757f, 335.1355f, 467.42245f);
		Player player = harness.SpawnPlayer(671f, 337f, 467.4f);
		harness.Engage(boss, player);

		// His worm clock starts on the first blow landed on him, not on entering combat.
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
		return (harness, boss, player);
	}

	private static int Worms(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == WaterWorm);

	/// <summary><b>Five worms arrive when he engages.</b></summary>
	[Fact]
	public void EngagingBringsFiveWorms()
	{
		var (harness, _, _) = Engaged();
		using BossAiHarness _h = harness;

		Assert.Equal(5, Worms(harness));
	}

	/// <summary>
	/// <b>And they leave at seventy-one seconds.</b> The pin the whole change is about: before it, the
	/// five that arrived at the pull were still standing at the boss's death.
	/// </summary>
	[Fact]
	public void TheWormsLeaveAtSeventyOneSeconds()
	{
		var (harness, _, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(70));
		Assert.Equal(5, Worms(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.Equal(0, Worms(harness));
	}

	/// <summary>
	/// <b>A fresh batch follows a second later.</b> Retail's 72-second timer against its 71-second
	/// lifetime, which is what makes the class's "only if none are standing" guard harmless rather than
	/// fatal — it now finds an empty room every time it looks.
	/// </summary>
	[Fact]
	public void ASecondBatchArrivesOnRetailsCycle()
	{
		var (harness, _, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(72));

		Assert.Equal(5, Worms(harness));
	}
}
