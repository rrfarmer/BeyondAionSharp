using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for Commander Bakarma's promotion ladder, translated from retail patterns
/// <c>IDDF3_DrakanFiBossD</c>, <c>NDrakan_ChSlave1</c> and <c>NDrakan_ChSlave2</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class CommanderBakarmaAiTests
{
	private const int DraupnirCave = 300030000;

	private const int Bakarma = 213780;
	private const int Legionary = 280685;
	private const int Vanguard = 280686;
	private const int RelicGuardian = 280687;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DraupnirCave).WithWorldSize(2048)
			.WithAi(typeof(CommanderBakarmaAI), typeof(BakarmaLegionaryAI), typeof(BakarmaVanguardAI),
				typeof(BakarmaRelicGuardianAI), typeof(SummonerAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	private static List<Npc> Live(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	private static void Strike(Npc target, Creature attacker) =>
		target.GetAi().OnCreatureEvent(AiEventType.Attack, attacker);

	/// <summary>
	/// Hands one add the call directly, so a pin on what the <em>rung</em> does is not also a pin on
	/// what Bakarma's summon table lays at that percentage.
	/// </summary>
	private static void Tell(Npc listener, Npc sender, int message) =>
		((Aion.GameServer.Ai.INpcMessageListener)listener.GetAi()).OnNpcMessage(sender, message, null);

	private static (BossAiHarness, Npc, Player) Cave()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Bakarma, 813f, 432f, 318f);
		Player raider = harness.SpawnPlayer(815f, 432f, 318f);
		harness.Engage(boss, raider);
		return (harness, boss, raider);
	}

	/// <summary>
	/// <b>Between twenty-six and fifty percent, every legionary becomes a vanguard where it stands.</b>
	/// Nothing new is summoned — the add replaces itself, so the count holds and the fight grows.
	/// </summary>
	[Fact]
	public void AtFiftyTheLegionariesTakeTheNextRank()
	{
		var (harness, boss, raider) = Cave();
		using BossAiHarness _h = harness;

		Npc one = harness.Spawn(Legionary, 815f, 435f, 318f);
		Npc two = harness.Spawn(Legionary, 816f, 436f, 318f);
		BossAiHarness.MakeMutuallyKnown(boss, one);
		BossAiHarness.MakeMutuallyKnown(boss, two);

		BossAiHarness.SetExactPercent(boss, 49);
		Strike(boss, raider);

		Assert.Empty(Live(harness, Legionary));
		Assert.Equal(2, Live(harness, Vanguard).Count);
	}

	/// <summary>
	/// <b>Below twenty-five, a vanguard takes six seconds to become a relic guardian</b> — and the
	/// first rung took none. That asymmetry is retail's, and it is the only window in the ladder a
	/// raid can act inside.
	/// </summary>
	[Fact]
	public void AtTwentyFiveTheVanguardsCountSixSeconds()
	{
		var (harness, boss, raider) = Cave();
		using BossAiHarness _h = harness;

		Npc vanguard = harness.Spawn(Vanguard, 815f, 435f, 318f);
		BossAiHarness.MakeMutuallyKnown(boss, vanguard);

		// Retail's countdown is a battle timer, so it runs only while the vanguard is fighting --
		// which in the cave it always is, and in a harness has to be said.
		harness.Engage(vanguard, raider);

		Tell(vanguard, boss, CommanderBakarmaAI.TakeTheLast);

		harness.Clock.Advance(TimeSpan.FromMilliseconds(5000));
		Assert.True(vanguard.IsSpawned());
		Assert.Empty(Live(harness, RelicGuardian));

		harness.Clock.Advance(TimeSpan.FromMilliseconds(2000));

		Assert.False(vanguard.IsSpawned());
		Assert.Single(Live(harness, RelicGuardian));
	}

	/// <summary>
	/// <b>A vanguard killed inside its six seconds never promotes.</b> The other half of the window,
	/// and the reason the countdown is worth having rather than merely copying.
	/// </summary>
	[Fact]
	public void AVanguardKilledInsideItsSixSecondsNeverPromotes()
	{
		var (harness, boss, raider) = Cave();
		using BossAiHarness _h = harness;

		Npc vanguard = harness.Spawn(Vanguard, 815f, 435f, 318f);
		BossAiHarness.MakeMutuallyKnown(boss, vanguard);

		// Retail's countdown is a battle timer, so it runs only while the vanguard is fighting --
		// which in the cave it always is, and in a harness has to be said.
		harness.Engage(vanguard, raider);

		Tell(vanguard, boss, CommanderBakarmaAI.TakeTheLast);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(3000));
		vanguard.GetController().Delete();

		harness.Clock.Advance(TimeSpan.FromMilliseconds(10000));

		Assert.Empty(Live(harness, RelicGuardian));
	}

	/// <summary>
	/// <b>The first call does not reach the second rung and the second does not reach the first.</b>
	/// Retail gives the two rungs different numbers, so a vanguard standing when the first call goes
	/// out waits for its own, and a legionary that survived to the second is stuck where it is.
	/// </summary>
	/// <remarks>
	/// Told directly rather than driven through Bakarma's health, because his summon table lays adds
	/// at those same percentages and a pin that counted them would be measuring two things.
	/// </remarks>
	[Fact]
	public void EachCallReachesOnlyItsOwnRung()
	{
		var (harness, boss, raider) = Cave();
		using BossAiHarness _h = harness;

		Npc vanguard = harness.Spawn(Vanguard, 815f, 435f, 318f);
		Npc legionary = harness.Spawn(Legionary, 816f, 436f, 318f);
		harness.Engage(vanguard, raider);
		harness.Engage(legionary, raider);

		Tell(vanguard, boss, CommanderBakarmaAI.TakeTheNextRank);
		Tell(legionary, boss, CommanderBakarmaAI.TakeTheLast);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(10000));

		Assert.True(vanguard.IsSpawned());
		Assert.True(legionary.IsSpawned());
		Assert.Empty(Live(harness, RelicGuardian));
	}

	/// <summary>
	/// <b>And only within fifty metres</b>, which is retail's range on both calls.
	/// </summary>
	[Fact]
	public void AndOnlyWithinFiftyMetres()
	{
		var (harness, boss, raider) = Cave();
		using BossAiHarness _h = harness;

		Npc near = harness.Spawn(Legionary, 815f, 435f, 318f);
		Npc far = harness.Spawn(Legionary, 813f, 500f, 318f);
		BossAiHarness.MakeMutuallyKnown(boss, near);
		BossAiHarness.MakeMutuallyKnown(boss, far);

		BossAiHarness.SetExactPercent(boss, 49);
		Strike(boss, raider);

		Assert.False(near.IsSpawned());
		Assert.True(far.IsSpawned());
	}

	/// <summary>
	/// <b>The message numbers are retail's, not ours.</b> Sender and both listeners share the
	/// constants, so nothing else here would notice them changing.
	/// </summary>
	[Fact]
	public void TheMessageNumbersAreRetails()
	{
		Assert.Equal(5001, CommanderBakarmaAI.TakeTheNextRank);
		Assert.Equal(5002, CommanderBakarmaAI.TakeTheLast);
	}

	/// <summary>
	/// <b>Kill one legionary in front of the others and the rest leave.</b> Retail's
	/// <c>on_see_friend_killed_by_user</c>, which our AI layer had no event for until now — and which
	/// is the raid's whole answer to the promotion ladder.
	/// </summary>
	[Fact]
	public void KillOneInFrontOfTheOthersAndTheRestLeave()
	{
		var (harness, boss, raider) = Cave();
		using BossAiHarness _h = harness;

		Npc doomed = harness.Spawn(Legionary, 815f, 435f, 318f);
		Npc watcher = harness.Spawn(Legionary, 816f, 436f, 318f);
		BossAiHarness.MakeMutuallyKnown(doomed, watcher);

		// The notice rather than the whole controller death path: NpcController.OnDie reaches
		// SiegeService, which a harness has no world for. The one line that calls this from OnDie is
		// in NpcController; what it calls is what these pins measure.
		Aion.GameServer.Ai.FriendDeathNotice.Raise(doomed, raider);

		Assert.False(watcher.IsSpawned());
	}

	/// <summary>
	/// <b>Killed by anything but a player and nobody moves.</b> Retail's handler is
	/// <c>..._killed_by_user</c>, so an add finished off by its own live time, by another NPC, or by a
	/// boss clearing the board is not what it is about — and a ladder that emptied itself on those
	/// would be a very different fight.
	/// </summary>
	[Fact]
	public void KilledByAnythingButAPlayerAndNobodyMoves()
	{
		var (harness, boss, raider) = Cave();
		using BossAiHarness _h = harness;

		Npc doomed = harness.Spawn(Legionary, 815f, 435f, 318f);
		Npc watcher = harness.Spawn(Legionary, 816f, 436f, 318f);
		BossAiHarness.MakeMutuallyKnown(doomed, watcher);

		Aion.GameServer.Ai.FriendDeathNotice.Raise(doomed, boss);

		Assert.True(watcher.IsSpawned());
	}

	/// <summary>
	/// <b>An enemy dying in front of it means nothing.</b> Retail's word is <c>friend</c>, and this is
	/// the tribe test the aggro layer already uses.
	/// </summary>
	[Fact]
	public void AnEnemyDyingInFrontOfItMeansNothing()
	{
		var (harness, boss, raider) = Cave();
		using BossAiHarness _h = harness;

		Npc watcher = harness.Spawn(Legionary, 816f, 436f, 318f);
		Npc stranger = harness.Spawn(Bakarma, 815f, 435f, 318f);
		BossAiHarness.MakeMutuallyKnown(stranger, watcher);

		Aion.GameServer.Ai.FriendDeathNotice.Raise(stranger, raider);

		Assert.True(watcher.IsSpawned());
	}
}
