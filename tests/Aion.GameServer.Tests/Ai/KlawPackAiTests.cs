using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the klaw pack, translated from retail patterns <c>ND2_CnD_BR1</c>, <c>ND2_CnD_BR3</c> and
/// <c>ND2_CnD_RE1</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class KlawPackAiTests
{
	private const int Beluslan = 220030000;

	private const int KlawWarden = 211119;      // ND2_CnD_BR1 -- calls below half, answers always
	private const int KingKlawtan = 212330;     // ND2_CnD_BR1 -- the named one
	private const int KlawSentinel = 211146;    // ND2_CnD_BR3 -- calls at a third, then runs
	private const int NannyNuk = 212822;        // ND2_CnD_BR3 -- the named one
	private const int KlawGatherer = 211118;    // ND2_CnD_RE1 -- only ever answers
	private const int KlawSpy = 211502;         // ND2_CnD_RE1

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Beluslan).WithWorldSize(2048)
			.WithAi(typeof(KlawCallerAI), typeof(KlawSentinelAI), typeof(KlawEscortAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>A wounded klaw, one escort within earshot, and the player it is fighting.</summary>
	private static (BossAiHarness, Npc, Npc, Player) Camp(int callerId = KlawWarden)
	{
		BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(callerId, 300f, 300f, 200f);
		Npc escort = harness.Spawn(KlawGatherer, 305f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(caller, escort);
		harness.Engage(caller, raider);
		return (harness, caller, escort, raider);
	}

	/// <summary>
	/// <b>Below half health an ordinary klaw names its target to the camp, and the camp commits.</b>
	/// </summary>
	[Theory]
	[InlineData(KlawWarden)]
	[InlineData(KingKlawtan)]
	public void BelowHalfTheKlawCallsAndTheCampCommits(int callerId)
	{
		var (harness, caller, escort, raider) = Camp(callerId);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(caller, 60);
		caller.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		Assert.Equal(0, escort.GetAggroList().GetHate(raider));

		BossAiHarness.SetExactPercent(caller, 40);
		caller.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(1000, escort.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A sentinel calls at a third rather than a half</b>, which is the only threshold in the family
	/// that differs — and the reason a mixed camp does not answer all at once.
	/// </summary>
	[Theory]
	[InlineData(KlawSentinel)]
	[InlineData(NannyNuk)]
	public void ASentinelHoldsOutUntilAThird(int sentinelId)
	{
		var (harness, sentinel, escort, raider) = Camp(sentinelId);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(sentinel, 45);
		sentinel.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		Assert.Equal(0, escort.GetAggroList().GetHate(raider));

		BossAiHarness.SetExactPercent(sentinel, 30);
		sentinel.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(1000, escort.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A thousand from an escort against one from a caller.</b> That difference is the pack: the
	/// klaws that answer hardest are the peons and gatherers, not the wardens.
	/// </summary>
	[Fact]
	public void TheEscortCommitsAndTheOtherCallersOnlyGlance()
	{
		var (harness, caller, escort, raider) = Camp();
		using BossAiHarness _h = harness;

		Npc otherCaller = harness.Spawn(KlawWarden, 307f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(caller, otherCaller);

		BossAiHarness.SetExactPercent(caller, 40);
		caller.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(1000, escort.GetAggroList().GetHate(raider));
		Assert.Equal(1, otherCaller.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A caller answers whether or not it is busy; a sentinel only answers when it is not.</b> Retail
	/// gives the ordinary klaw's answer no state guard at all and the sentinel's an idle guard, so a cry
	/// landing in a camp mid-fight pulls the wardens and leaves the sentinels alone.
	/// </summary>
	[Fact]
	public void TheCallerAnswersWhileBusyAndTheSentinelDoesNot()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc caller = harness.Spawn(KlawWarden, 300f, 300f, 200f);
		Npc busyCaller = harness.Spawn(KlawWarden, 305f, 300f, 200f);
		Npc busySentinel = harness.Spawn(KlawSentinel, 306f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ASMODIANS);
		Player elsewhere = harness.SpawnPlayer(310f, 300f, 200f, race: Race.ASMODIANS);

		BossAiHarness.MakeMutuallyKnown(caller, busyCaller);
		BossAiHarness.MakeMutuallyKnown(caller, busySentinel);
		harness.Engage(caller, raider);
		harness.Engage(busyCaller, elsewhere);
		harness.Engage(busySentinel, elsewhere);

		BossAiHarness.SetExactPercent(caller, 40);
		caller.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(1, busyCaller.GetAggroList().GetHate(raider));
		Assert.Equal(0, busySentinel.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And an escort that is already fighting does not commit either</b> — it scatters instead,
	/// switching to a random one of its own attackers. Same cry, opposite effect, decided entirely by
	/// whether the klaw was busy.
	/// </summary>
	[Fact]
	public void AnEscortAlreadyFightingScattersInsteadOfCommitting()
	{
		var (harness, caller, escort, raider) = Camp();
		using BossAiHarness _h = harness;

		Player elsewhere = harness.SpawnPlayer(310f, 300f, 200f, race: Race.ASMODIANS);
		harness.Engage(escort, elsewhere);

		BossAiHarness.SetExactPercent(caller, 40);
		caller.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(0, escort.GetAggroList().GetHate(raider));
	}

	/// <summary><b>A spell calls the camp too, and the flag is shared across both provocations.</b></summary>
	[Fact]
	public void ASpellCallsTheCampTooAndTheFlagIsShared()
	{
		var (harness, caller, escort, raider) = Camp();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(caller, 40);
		caller.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);
		Assert.Equal(1000, escort.GetAggroList().GetHate(raider));

		Npc late = harness.Spawn(KlawSpy, 302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(caller, late);
		caller.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(0, late.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And only within twenty metres</b>, which is retail's range on every caller in the family — so
	/// a cry pulls its own camp and not the next one along the ridge.
	/// </summary>
	[Fact]
	public void AndOnlyWithinTwentyMetres()
	{
		var (harness, caller, escort, raider) = Camp();
		using BossAiHarness _h = harness;

		Npc distant = harness.Spawn(KlawGatherer, 340f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(caller, distant);

		BossAiHarness.SetExactPercent(caller, 40);
		caller.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(1000, escort.GetAggroList().GetHate(raider));
		Assert.Equal(0, distant.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>At a third the sentinel runs, and it runs away from the player.</b> Retail's <c>flee_from</c>
	/// with <c>from=OBJI_CUR_TARGET</c> — three seconds when it is hit, four when it is cast at.
	/// </summary>
	/// <remarks>
	/// <b>This pin says the opposite of what it used to.</b> It was skipped here and in three other
	/// files with the note that a flee "moves the npc through the move controller, which this harness
	/// does not simulate" — true, and beside the point: <c>PatternAi.FleeingTo</c> records the
	/// destination the flee computed, and it is public. <b>The movement is unobservable; the decision to
	/// flee, and its direction, never were.</b>
	/// </remarks>
	[Fact]
	public void AtAThirdTheSentinelRunsFromThePlayer()
	{
		var (harness, sentinel, escort, raider) = Camp(KlawSentinel);
		using BossAiHarness _h = harness;

		Aion.GameServer.Ai.Pattern.PatternAi ai =
			Assert.IsAssignableFrom<Aion.GameServer.Ai.Pattern.PatternAi>(sentinel.GetAi());
		Assert.Null(ai.FleeingTo);

		float startedAt = sentinel.GetPosition().GetX();
		BossAiHarness.SetExactPercent(sentinel, 30);
		sentinel.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		(float X, float Y)? destination = ai.FleeingTo;
		Assert.NotNull(destination);

		// The sentinel stands at 300 and the raider at 303: away is the negative direction.
		Assert.True(destination.Value.X < startedAt,
			"the sentinel fled towards the player rather than away from it");
	}

	/// <summary><b>The message number is retail's, not ours, and the family shares it.</b></summary>
	[Fact]
	public void TheMessageNumberIsRetails()
	{
		Assert.Equal(2003, KlawCallerAI.HurtingMe);
		Assert.Equal(20f, KlawCallerAI.CallReach);
	}
}
