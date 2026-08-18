using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the marabata booster's call and the Anuhart answer, translated from retail patterns
/// <c>ND2_WhHS1</c>–<c>_3</c> and the eight <c>Lizardman_*_IDLF1</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The player is Asmodian because the aggro list refuses hate between friends.
/// <para>
/// <b>These pins measure the hate the call adds, not the hate a guard has.</b> A guard standing seven
/// metres from a player is an aggressive npc next to an enemy, and it will find them on its own
/// eventually — which is right, and is not what any of this is about. An absolute figure here is a
/// race between the call and the guard's own aggro scan, and it lost that race about one full-suite
/// run in seven before the assertions became deltas. See docs/retail-ai-fidelity.md.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AnuhartGuardAiTests
{
	private const int DarkPoeta = 300040000;

	private const int Booster = 700439;
	private const int Scalewatch = 214844;
	private const int Guardian = 214847;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DarkPoeta).WithWorldSize(2048)
			// DrakanHealingServantAI and EnemyServantAI are here because the guardian can make them:
			// AnuhartMedicAI extends the Java-parity drakanmedic, which rolls three percent on every
			// blow to call a servant. Without them registered the harness threw "No AI found for name
			// drakanhealingservant" about one run in twenty -- a class the encounter can produce and
			// the harness did not know about. Rule: WithAi must list what the fight can spawn, not
			// just what the test spawns.
			.WithAi(typeof(MarabataControllerAI), typeof(AnuhartGuardAI), typeof(AnuhartMedicAI),
				typeof(DrakanHealingServantAI), typeof(EnemyServantAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <remarks>
	/// <b>The player stands outside the listeners' own sight</b> — their <c>srange</c> is seven and
	/// eight metres and the raider is eleven away — so the call, which reaches twenty-five and fifty,
	/// is doing all the work rather than sharing it with an aggro scan.
	/// <para>
	/// Worth having, and <em>not</em> the fix for the flake it was first written for: the seven-in-fifty
	/// failures measured here turned out to be a poisoned <c>SiegeService</c> type initialiser, not this
	/// geometry. See <c>SiegeServiceTestInit</c> and docs/retail-ai-fidelity.md.
	/// </para>
	/// </remarks>
	private static (BossAiHarness, Npc, Npc, Player) Chamber()
	{
		BossAiHarness harness = NewHarness();
		Npc booster = harness.Spawn(Booster, 660f, 370f, 99f);
		Npc guard = harness.Spawn(Scalewatch, 670f, 370f, 99f);
		Player raider = harness.SpawnPlayer(659f, 370f, 99f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(booster, guard);
		return (harness, booster, guard, raider);
	}

	private static void Strike(Npc target, Creature attacker) =>
		target.GetAi().OnCreatureEvent(AiEventType.Attack, attacker);

	/// <summary>
	/// <b>Attacking a booster brings the room.</b> Retail puts nothing else on these three patterns'
	/// <c>on_attacked</c>, and nothing else on the eight Anuhart patterns at all.
	/// </summary>
	[Fact]
	public void AttackingABoosterBringsTheRoom()
	{
		var (harness, booster, guard, raider) = Chamber();
		using BossAiHarness _h = harness;
		Assert.Null(guard.GetTarget());

		int before = guard.GetAggroList().GetHate(raider);
		Strike(booster, raider);

		Assert.Same(raider, guard.GetTarget());
		Assert.Equal(300, guard.GetAggroList().GetHate(raider) - before);
	}

	/// <summary>
	/// <b>A guard already in a fight is bid for harder.</b> Three hundred on an empty aggro list is
	/// already the top of it; five hundred is what it takes to move a guard that is busy.
	/// </summary>
	[Fact]
	public void AGuardAlreadyFightingIsBidForHarder()
	{
		var (harness, booster, guard, raider) = Chamber();
		using BossAiHarness _h = harness;

		Player other = harness.SpawnPlayer(671f, 370f, 99f, race: Race.ASMODIANS);
		harness.Engage(guard, other);
		int before = guard.GetAggroList().GetHate(raider);

		Strike(booster, raider);

		Assert.Equal(0, before);
		Assert.Equal(500, guard.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>Every blow calls again, and the call escalates itself.</b> The first one finds the guard
	/// standing and pays three hundred; the guard is fighting by the time the second arrives, so it
	/// pays five. Retail wrote the split for a guard that some <em>other</em> fight had already
	/// claimed, and because the answer commits the guard it applies to the second blow on the same
	/// booster too — 300 then 500, not 300 twice.
	/// </summary>
	[Fact]
	public void EveryBlowCallsAgainAndTheSecondBidsHigher()
	{
		var (harness, booster, guard, raider) = Chamber();
		using BossAiHarness _h = harness;

		int before = guard.GetAggroList().GetHate(raider);
		Strike(booster, raider);
		Assert.Equal(300, guard.GetAggroList().GetHate(raider) - before);

		Strike(booster, raider);

		Assert.Equal(800, guard.GetAggroList().GetHate(raider) - before);
	}

	/// <summary>
	/// <b>Fifty metres, bracketed from both sides.</b> A guard forty metres from the booster answers
	/// and one sixty metres away does not — the range is retail's own number on all three patterns,
	/// and sixteen of the eight npcs' sixty-four Dark Poeta spawn spots fall inside it.
	/// </summary>
	/// <remarks>
	/// The raider stands beside the far pair rather than beside the booster, because a guard forty
	/// metres from the <em>player</em> cannot take hate on them however well it heard the call — which
	/// is the trap the Ophidan Bridge pins fell into.
	/// </remarks>
	[Fact]
	public void FiftyMetresBracketedFromBothSides()
	{
		using BossAiHarness harness = NewHarness();
		Npc booster = harness.Spawn(Booster, 660f, 370f, 99f);
		Npc middling = harness.Spawn(Scalewatch, 700f, 370f, 99f);
		Npc distant = harness.Spawn(Scalewatch, 720f, 370f, 99f);
		Player raider = harness.SpawnPlayer(702f, 370f, 99f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(booster, middling);
		BossAiHarness.MakeMutuallyKnown(booster, distant);

		int before = middling.GetAggroList().GetHate(raider);
		Strike(booster, raider);

		Assert.Equal(300, middling.GetAggroList().GetHate(raider) - before);
		Assert.Equal(0, distant.GetAggroList().GetHate(raider));
		Assert.Null(distant.GetTarget());
	}

	/// <summary>
	/// <b>The message number is retail's, not ours.</b> Sender and listeners share one constant, so
	/// nothing else in these pins would notice it changing — and <c>6821</c> is data read out of the
	/// pattern dump rather than a value we chose.
	/// </summary>
	[Fact]
	public void TheMessageNumberIsRetails()
	{
		Assert.Equal(6821, MarabataControllerAI.BoosterUnderAttack);
	}

	/// <summary>
	/// <b>The guardian answers too, and it is on a different class.</b> Retail gives 214847 the same
	/// pattern as the other seven, but the npc is a <c>drakanmedic</c> that seventy-nine npcs share —
	/// so the answer is a subclass, and this is what says the healer does not get left behind.
	/// </summary>
	[Fact]
	public void TheGuardianAnswersFromItsOwnClass()
	{
		using BossAiHarness harness = NewHarness();
		Npc booster = harness.Spawn(Booster, 660f, 370f, 99f);
		Npc guardian = harness.Spawn(Guardian, 670f, 370f, 99f);
		Player raider = harness.SpawnPlayer(659f, 370f, 99f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(booster, guardian);

		int before = guardian.GetAggroList().GetHate(raider);
		Strike(booster, raider);

		Assert.Same(raider, guardian.GetTarget());
		Assert.Equal(300, guardian.GetAggroList().GetHate(raider) - before);
	}
}
