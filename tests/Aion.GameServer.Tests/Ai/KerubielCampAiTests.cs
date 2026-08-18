using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the kerubiel and kerubian camps, translated from retail patterns <c>ND2_AnE</c>,
/// <c>ND2_AnL</c>, <c>ND2_AnJ</c> and <c>ND2_AnJ_BR</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// <b>The npc chosen to stand for a pattern has to be one whose tribe can hate a player.</b>
/// <c>ND2_AnJ_BR</c>'s membership is mixed — fourteen <c>TAURIC</c>, seven <c>MONSTER</c>, three
/// aggressive, and one <c>GENERAL_DARK</c> — and the <c>GENERAL_DARK</c> one happens to sort first.
/// Picked as the pin's gark, every hunter-side assertion read zero, because <c>AggroList.AddHate</c>
/// drops hate aimed at a creature that is not an enemy and an Asmodian-side npc is not the enemy of an
/// Elyos raider by that tribe.
/// <para>
/// That is the third time this rule has bitten in this log, after the fortress guards and the
/// Panesterra slayers. <b>It is worth stating as a habit: when a pin over a broadcast reads zero, check
/// the answerer's tribe before checking the branch.</b>
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class KerubielCampAiTests
{
	private const int Verteron = 210030000;

	private const int KerubielBandit = 211062;   // calls on 2001 at 15m
	private const int KerubielFighter = 210977;  // answers with 101
	private const int KerubianHunter = 210976;   // calls on 2005 at 20m
	// 210774, not the 204811 that heads the pattern's membership: that one is tribe GENERAL_DARK, an
	// Asmodian-side npc whose aggro list refuses hate aimed at a player it is not hostile to, so the
	// answer lands as zero however correct the branch is. See the class remarks.
	private const int Gark = 210774;             // answers with 200

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Verteron).WithWorldSize(2048)
			.WithAi(typeof(KerubielBanditAI), typeof(KerubielFighterAI), typeof(KerubianHunterAI),
				typeof(KerubianGarkAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, Npc, Player) Camp(int callerId, int answererId, float apart = 6f)
	{
		BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(callerId, 300f, 300f, 200f);
		Npc answerer = harness.Spawn(answererId, 300f + apart, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(caller, answerer);
		harness.Engage(caller, raider);
		return (harness, caller, answerer, raider);
	}

	/// <summary>
	/// <b>Below half health the caller names whoever is beating it, and its camp comes.</b> Both camps,
	/// their own numbers, their own payloads.
	/// </summary>
	[Theory]
	[InlineData(KerubielBandit, KerubielFighter, 101)]
	[InlineData(KerubianHunter, Gark, 200)]
	public void BelowHalfTheCallerNamesAndTheCampComes(int callerId, int answererId, int expected)
	{
		var (harness, caller, answerer, raider) = Camp(callerId, answererId);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(caller, 70);
		caller.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		int beforeTheCall = answerer.GetAggroList().GetHate(raider);
		Assert.True(beforeTheCall < expected, "the caller called above half health");

		BossAiHarness.SetExactPercent(caller, 40);
		caller.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(beforeTheCall + expected, answerer.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And it calls on every blow, not once.</b> Retail puts no flag var on either branch — the first
	/// caller in this log that does not call once, and the difference is the mechanic: a camp does not
	/// answer a call, it answers continuously.
	/// </summary>
	[Theory]
	[InlineData(KerubielBandit, KerubielFighter, 101)]
	[InlineData(KerubianHunter, Gark, 200)]
	public void AndItCallsOnEveryBlowNotOnce(int callerId, int answererId, int each)
	{
		var (harness, caller, answerer, raider) = Camp(callerId, answererId);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(caller, 40);
		caller.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		int afterOne = answerer.GetAggroList().GetHate(raider);

		caller.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		caller.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(afterOne + (2 * each), answerer.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The garks hit twice as hard as the fighters.</b> Retail gives a gark an
	/// <c>add_hate_point</c> of a hundred where a kerubiel fighter gets one — two camps, the same call
	/// shape, and the pets committed twice as far as the soldiers.
	/// </summary>
	[Fact]
	public void TheGarksHitTwiceAsHardAsTheFighters()
	{
		var (banditCamp, bandit, fighter, raider) = Camp(KerubielBandit, KerubielFighter);
		using BossAiHarness _b = banditCamp;
		BossAiHarness.SetExactPercent(bandit, 40);
		bandit.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		var (hunterCamp, hunter, gark, other) = Camp(KerubianHunter, Gark);
		using BossAiHarness _h = hunterCamp;
		BossAiHarness.SetExactPercent(hunter, 40);
		hunter.GetAi().OnCreatureEvent(AiEventType.Attack, other);

		Assert.Equal(101, fighter.GetAggroList().GetHate(raider));
		Assert.Equal(200, gark.GetAggroList().GetHate(other));
	}

	/// <summary>
	/// <b>The two camps do not hear each other</b> — each has its own number, so a bandit's call leaves
	/// the garks standing.
	/// </summary>
	[Fact]
	public void TheTwoCampsDoNotHearEachOther()
	{
		var (harness, bandit, gark, raider) = Camp(KerubielBandit, Gark);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(bandit, 40);
		bandit.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(0, gark.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>Fifteen metres for a bandit and twenty for a hunter</b>, which is retail's.
	/// </summary>
	[Fact]
	public void TheHunterCallCarriesFurther()
	{
		var (near, bandit, close, raider) = Camp(KerubielBandit, KerubielFighter);
		using BossAiHarness _n = near;
		Npc far = near.Spawn(KerubielFighter, 318f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(bandit, far);

		BossAiHarness.SetExactPercent(bandit, 40);
		bandit.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		Assert.Equal(101, close.GetAggroList().GetHate(raider));
		Assert.Equal(0, far.GetAggroList().GetHate(raider));

		var (wide, hunter, alsoClose, other) = Camp(KerubianHunter, Gark);
		using BossAiHarness _w = wide;
		Npc alsoFar = wide.Spawn(Gark, 318f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(hunter, alsoFar);

		BossAiHarness.SetExactPercent(hunter, 40);
		hunter.GetAi().OnCreatureEvent(AiEventType.Attack, other);

		Assert.Equal(200, alsoFar.GetAggroList().GetHate(other));
	}

	/// <summary><b>The message numbers and both ranges are retail's, not ours.</b></summary>
	[Fact]
	public void TheNumbersAreRetails()
	{
		Assert.Equal(2001, KerubielBanditAI.GetHim);
		Assert.Equal(2005, KerubianHunterAI.GetHim);
		Assert.Equal(15f, KerubielBanditAI.CallReach);
		Assert.Equal(20f, KerubianHunterAI.CallReach);
	}
}
