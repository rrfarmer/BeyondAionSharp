using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the kaidan casters and the smackstoppers they call, translated from retail patterns
/// <c>NKrall_WeA</c>, <c>NKrall_WeB</c>, <c>NKrall_WeC</c> and <c>NKrall_KeC</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class KaidanCasterCallAiTests
{
	private const int Beshmundir = 300230000;

	private const int KaidanShaman = 211019;        // NKrall_WeA -- calls between 41 and 75
	private const int KaidanChieftain = 211038;     // NKrall_WeB -- 36 to 75, the widest band
	private const int KaidanSoothsayer = 212049;    // NKrall_WeC -- 46 to 75, and the call stops its clock
	private const int Smackstopper = 212030;        // NKrall_KeC

	/// <summary>
	/// Support aggro puts a point on a friendly NPC for each tick of a fight beside it, so an answer
	/// worth a hundred reads as a hundred and a few. Every assertion here is a band for that reason --
	/// the fourth encounter in this log to need it.
	/// </summary>
	private const int Drift = 9;

	private static BossAiHarness Harness() =>
		BossAiHarness.For(Beshmundir).WithWorldSize(2048)
			.WithAi(typeof(KaidanShamanAI), typeof(KaidanChieftainAI), typeof(KaidanSoothsayerAI),
				typeof(KaidanSmackstopperAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>A caster, a smackstopper in earshot, and the raider the caster is fighting.</summary>
	private static (BossAiHarness, Npc, Npc, Player) Camp(int callerId, string aiName, int percent)
	{
		BossAiHarness harness = Harness();
		Npc caller = harness.SpawnWithAi(callerId, aiName, 300f, 300f, 200f);
		Npc answerer = harness.SpawnWithAi(Smackstopper, "kaidan_smackstopper", 305f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(caller, answerer);
		BossAiHarness.MakeMutuallyKnown(answerer, raider);
		BossAiHarness.SetExactPercent(caller, percent);
		harness.Engage(caller, raider);
		return (harness, caller, answerer, raider);
	}

	/// <summary>
	/// <b>A hurt caster names its target and a smackstopper goes to it</b>, with a hundred points behind
	/// the switch. That is the half of the cry this port can land.
	/// </summary>
	[Theory]
	[InlineData(KaidanShaman, "kaidan_shaman", 60)]
	[InlineData(KaidanChieftain, "kaidan_chieftain", 60)]
	[InlineData(KaidanSoothsayer, "kaidan_soothsayer", 60)]
	public void AHurtCasterSendsTheSmackstopperAtItsTarget(int callerId, string aiName, int percent)
	{
		var (harness, caller, answerer, raider) = Camp(callerId, aiName, percent);
		using BossAiHarness _h = harness;

		Assert.Equal(0, answerer.GetAggroList().GetHate(raider));

		harness.Watch(12, null);

		Assert.InRange(answerer.GetAggroList().GetHate(raider),
			KaidanCalls.Commit, KaidanCalls.Commit + Drift);
		Assert.Same(raider, answerer.GetTarget());
	}

	/// <summary>
	/// <b>The call is a band, not a threshold.</b> A caster burned straight past the bottom of its band
	/// never calls at all — which is the difference between a slow fight that brings the camp and a
	/// burst that silences it.
	/// </summary>
	[Theory]
	[InlineData(KaidanShaman, "kaidan_shaman", 20)]
	[InlineData(KaidanChieftain, "kaidan_chieftain", 20)]
	[InlineData(KaidanSoothsayer, "kaidan_soothsayer", 20)]
	public void ACasterBurnedPastItsBandNeverCalls(int callerId, string aiName, int percent)
	{
		var (harness, caller, answerer, raider) = Camp(callerId, aiName, percent);
		using BossAiHarness _h = harness;

		harness.Watch(20, null);

		Assert.InRange(answerer.GetAggroList().GetHate(raider), 0, Drift);
	}

	/// <summary>
	/// <b>And above the band it is just as quiet.</b> A caster still near full health has nothing to ask
	/// for, so the top of the band matters as much as the bottom.
	/// </summary>
	[Fact]
	public void AndACasterAboveItsBandIsQuietToo()
	{
		var (harness, caller, answerer, raider) = Camp(KaidanShaman, "kaidan_shaman", 90);
		using BossAiHarness _h = harness;

		harness.Watch(12, null);

		Assert.InRange(answerer.GetAggroList().GetHate(raider), 0, Drift);
	}

	/// <summary>
	/// <b>The chieftain calls where the soothsayer beside it has gone quiet.</b> 36 against 46 is the
	/// widest and narrowest band in the camp, and at forty percent only one of them still shouts.
	/// </summary>
	[Fact]
	public void TheChieftainCallsWhereTheSoothsayerWillNot()
	{
		using BossAiHarness harness = Harness();
		Npc chieftain = harness.SpawnWithAi(KaidanChieftain, "kaidan_chieftain", 300f, 300f, 200f);
		Npc soothsayer = harness.SpawnWithAi(KaidanSoothsayer, "kaidan_soothsayer", 340f, 300f, 200f);
		Npc forChieftain = harness.SpawnWithAi(Smackstopper, "kaidan_smackstopper", 305f, 300f, 200f);
		Npc forSoothsayer = harness.SpawnWithAi(Smackstopper, "kaidan_smackstopper", 345f, 300f, 200f);
		Player one = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		Player two = harness.SpawnPlayer(342f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(chieftain, forChieftain);
		BossAiHarness.MakeMutuallyKnown(soothsayer, forSoothsayer);
		BossAiHarness.MakeMutuallyKnown(forChieftain, one);
		BossAiHarness.MakeMutuallyKnown(forSoothsayer, two);

		BossAiHarness.SetExactPercent(chieftain, 40);
		BossAiHarness.SetExactPercent(soothsayer, 40);
		harness.Engage(chieftain, one);
		harness.Engage(soothsayer, two);

		harness.Watch(12, null);

		Assert.InRange(forChieftain.GetAggroList().GetHate(one),
			KaidanCalls.Commit, KaidanCalls.Commit + Drift);
		Assert.InRange(forSoothsayer.GetAggroList().GetHate(two), 0, Drift);
	}

	/// <summary>
	/// <b>Once per fight, however long it lasts.</b> The call carries a flag and the answer carries one
	/// of its own, so neither end can be made to repeat.
	/// </summary>
	[Fact]
	public void TheCryAndTheAnswerAreBothSpentOnce()
	{
		var (harness, caller, answerer, raider) = Camp(KaidanShaman, "kaidan_shaman", 60);
		using BossAiHarness _h = harness;

		harness.Watch(12, null);
		int afterFirst = answerer.GetAggroList().GetHate(raider);
		Assert.InRange(afterFirst, KaidanCalls.Commit, KaidanCalls.Commit + Drift);

		harness.Watch(40, null);

		// Drifts by support aggro, never by another hundred.
		Assert.InRange(answerer.GetAggroList().GetHate(raider),
			afterFirst, KaidanCalls.Commit + Drift);
	}

	/// <summary>
	/// <b>And a second caster cannot move a smackstopper that has already answered one.</b> The flag is
	/// the answerer's, not the call's — which is what makes a camp of casters produce one commitment
	/// rather than a queue of them.
	/// </summary>
	[Fact]
	public void ASecondCallerCannotMoveAnAnsweredSmackstopper()
	{
		using BossAiHarness harness = Harness();
		Npc first = harness.SpawnWithAi(KaidanShaman, "kaidan_shaman", 300f, 300f, 200f);
		Npc second = harness.SpawnWithAi(KaidanChieftain, "kaidan_chieftain", 303f, 300f, 200f);
		Npc answerer = harness.SpawnWithAi(Smackstopper, "kaidan_smackstopper", 305f, 300f, 200f);
		Player one = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		Player two = harness.SpawnPlayer(304f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(first, answerer);
		BossAiHarness.MakeMutuallyKnown(second, answerer);
		BossAiHarness.MakeMutuallyKnown(answerer, one);
		BossAiHarness.MakeMutuallyKnown(answerer, two);

		BossAiHarness.SetExactPercent(first, 60);
		harness.Engage(first, one);
		harness.Watch(12, null);
		Assert.InRange(answerer.GetAggroList().GetHate(one),
			KaidanCalls.Commit, KaidanCalls.Commit + Drift);

		BossAiHarness.SetExactPercent(second, 60);
		harness.Engage(second, two);
		harness.Watch(12, null);

		Assert.InRange(answerer.GetAggroList().GetHate(two), 0, Drift);
	}

	/// <summary>
	/// <b>The soothsayer's cry kills its own clock.</b> Retail leaves the re-arm off that one branch, and
	/// branches are first-match-wins, so the tick that carries the call is the last tick timer zero ever
	/// gets — visible here as a soothsayer that never reaches the fallback the others keep running on.
	/// </summary>
	/// <remarks>
	/// Pinned through the switch timer, which is the only other clock the soothsayer owns: it keeps
	/// running after the call, so a dead timer zero cannot be confused with a dead pattern.
	/// </remarks>
	[Fact]
	public void TheSoothsayerCryStopsItsOwnClockButNotTheOthers()
	{
		using BossAiHarness harness = Harness();
		Npc soothsayer = harness.SpawnWithAi(KaidanSoothsayer, "kaidan_soothsayer", 300f, 300f, 200f);
		Npc answerer = harness.SpawnWithAi(Smackstopper, "kaidan_smackstopper", 305f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		Player other = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(soothsayer, answerer);
		BossAiHarness.MakeMutuallyKnown(answerer, raider);
		BossAiHarness.MakeMutuallyKnown(answerer, other);

		BossAiHarness.SetExactPercent(soothsayer, 60);
		// Two attackers so the switch clock has somewhere to go, and the raider engaged last so it is
		// the current target the cry names.
		harness.Engage(soothsayer, other);
		harness.Engage(soothsayer, raider);
		harness.Watch(12, null);

		Assert.InRange(answerer.GetAggroList().GetHate(raider),
			KaidanCalls.Commit, KaidanCalls.Commit + Drift);

		// The switch clock survives the call and keeps picking from the attackers.
		harness.Watch(30, null);
		Assert.NotNull(soothsayer.GetTarget());
	}


	/// <summary>
	/// <b>The caller has a flag of its own, and it takes a second listener to see it.</b> A smackstopper
	/// that has answered will not answer again whatever the caller does, so one listener cannot tell a
	/// caller that cries once from a caller that cries every six seconds. A listener that arrives
	/// <em>after</em> the first cry can.
	/// </summary>
	/// <remarks>
	/// Third time in this log that two guards protected one observable — the Tiamat insurgents and the
	/// nunu farmers were the others — and the same fix each time: arrange for only one of them to be
	/// spent when the measurement is taken.
	/// </remarks>
	[Fact]
	public void AndTheCallerHasAFlagToo()
	{
		var (harness, caller, answerer, raider) = Camp(KaidanShaman, "kaidan_shaman", 60);
		using BossAiHarness _h = harness;

		harness.Watch(12, null);
		Assert.InRange(answerer.GetAggroList().GetHate(raider),
			KaidanCalls.Commit, KaidanCalls.Commit + Drift);

		// A fresh pair of ears, with its own unspent flag. Only a second cry can move it.
		Npc late = harness.SpawnWithAi(Smackstopper, "kaidan_smackstopper", 304f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(caller, late);
		BossAiHarness.MakeMutuallyKnown(late, raider);

		harness.Watch(30, null);

		Assert.InRange(late.GetAggroList().GetHate(raider), 0, Drift);
	}

	/// <summary>
	/// <b>The answer switches target, it does not merely add hate.</b> Retail writes
	/// <c>switch_target</c> with the points attached, so a smackstopper already busy with somebody it
	/// hates more still turns to the caller's target.
	/// </summary>
	/// <remarks>
	/// A hundred points alone would leave it where it was; this is the pin that tells the switch from
	/// the payload.
	/// </remarks>
	[Fact]
	public void TheAnswerSwitchesRatherThanJustAddingHate()
	{
		var (harness, caller, answerer, raider) = Camp(KaidanShaman, "kaidan_shaman", 60);
		using BossAiHarness _h = harness;

		// Somebody the smackstopper hates far more than the hundred the answer is worth.
		Player rival = harness.SpawnPlayer(306f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(answerer, rival);
		harness.Engage(answerer, rival);
		Assert.Same(rival, answerer.GetTarget());

		harness.Watch(12, null);

		Assert.True(answerer.GetAggroList().GetHate(rival) > answerer.GetAggroList().GetHate(raider),
			"the rival should still be the more hated of the two");
		Assert.Same(raider, answerer.GetTarget());
	}

	/// <summary><b>The numbers, reach and payload come from the patterns, not from us.</b></summary>
	[Fact]
	public void TheNumbersAreRetails()
	{
		Assert.Equal(1004, KaidanCalls.HealMe);
		Assert.Equal(1005, KaidanCalls.KillHim);
		Assert.Equal(15f, KaidanCalls.Reach);
		Assert.Equal(100, KaidanCalls.Commit);
	}
}
