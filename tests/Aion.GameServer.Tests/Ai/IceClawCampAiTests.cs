using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the brutal ice claw camp of Beluslan, translated from retail patterns <c>nlycan_HeA</c>,
/// <c>NLycan_HeB</c>, <c>NLycan_Pet_A</c> and <c>NLycan_Pet_B</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Every npc is spawned with its class named, for the reason the shulack pins record: a subject chosen
/// off the npc template is a subject the test does not control.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class IceClawCampAiTests
{
	private const int Beluslan = 220040000;

	private const int Hunter = 211436;       // calls 7006 on engaging, 7007 below half
	private const int Tamer = 211456;        // calls 7006 on engaging and below a third
	private const int RuthlessTayga = 211395;// answers both with five hundred
	private const int LesserTayga = 211426;  // answers only the first, with a hundred

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Beluslan).WithWorldSize(2048)
			.WithAi(typeof(IceClawHunterAI), typeof(IceClawTamerAI), typeof(RuthlessTaygaAI),
				typeof(RuthlessTaygaLesserAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>Five hundred, and a hundred for the lesser grade.</b> Two taygas with the same name on the
	/// nameplate, standing in the same camp, answering the same call five times apart.
	/// </summary>
	[Fact]
	public void TheTwoGradesOfTaygaAnswerDifferently()
	{
		using BossAiHarness harness = NewHarness();
		Npc hunter = harness.SpawnWithAi(Hunter, "ice_claw_hunter", 300f, 300f, 200f);
		Npc ruthless = harness.SpawnWithAi(RuthlessTayga, "ruthless_tayga", 306f, 300f, 200f);
		Npc lesser = harness.SpawnWithAi(LesserTayga, "ruthless_tayga_lesser", 308f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(hunter, ruthless);
		BossAiHarness.MakeMutuallyKnown(hunter, lesser);

		harness.Engage(hunter, raider);

		Assert.Equal(500, ruthless.GetAggroList().GetHate(raider));
		Assert.Equal(100, lesser.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A hunter below half health calls again, and only the ruthless grade hears it.</b> The payload
	/// is the same five hundred; what narrows is the audience.
	/// </summary>
	[Fact]
	public void BelowHalfTheHunterCallsAgainAndOnlyTheRuthlessHear()
	{
		using BossAiHarness harness = NewHarness();
		Npc hunter = harness.SpawnWithAi(Hunter, "ice_claw_hunter", 300f, 300f, 200f);
		Npc ruthless = harness.SpawnWithAi(RuthlessTayga, "ruthless_tayga", 306f, 300f, 200f);
		Npc lesser = harness.SpawnWithAi(LesserTayga, "ruthless_tayga_lesser", 308f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(hunter, ruthless);
		BossAiHarness.MakeMutuallyKnown(hunter, lesser);

		harness.Engage(hunter, raider);
		int ruthlessAfterFirst = ruthless.GetAggroList().GetHate(raider);
		int lesserAfterFirst = lesser.GetAggroList().GetHate(raider);

		BossAiHarness.SetExactPercent(hunter, 40);
		harness.Watch(10, null);

		// InRange, not Equal: a fight running beside a friendly npc adds a support-aggro point of its
		// own on the first attack tick. Five hundred is what a call is worth; one point is not a call.
		Assert.InRange(ruthless.GetAggroList().GetHate(raider),
			ruthlessAfterFirst + 500, ruthlessAfterFirst + 599);
		Assert.InRange(lesser.GetAggroList().GetHate(raider), lesserAfterFirst, lesserAfterFirst + 99);
	}

	/// <summary>
	/// <b>And the second call comes once.</b> Retail flags it, so a hunter that stays under half health
	/// does not keep re-committing its taygas.
	/// </summary>
	[Fact]
	public void AndTheSecondCallComesOnce()
	{
		using BossAiHarness harness = NewHarness();
		Npc hunter = harness.SpawnWithAi(Hunter, "ice_claw_hunter", 300f, 300f, 200f);
		Npc ruthless = harness.SpawnWithAi(RuthlessTayga, "ruthless_tayga", 306f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(hunter, ruthless);

		harness.Engage(hunter, raider);
		BossAiHarness.SetExactPercent(hunter, 40);
		harness.Watch(10, null);
		int afterSecondCall = ruthless.GetAggroList().GetHate(raider);

		harness.Watch(60, null);

		Assert.InRange(ruthless.GetAggroList().GetHate(raider), afterSecondCall, afterSecondCall + 99);
	}

	/// <summary>
	/// <b>The tamer calls the same number the hunter does</b>, so its taygas answer with the same five
	/// hundred — and its second call comes below a third rather than a half.
	/// </summary>
	[Fact]
	public void TheTamerCallsTheSameNumber()
	{
		using BossAiHarness harness = NewHarness();
		Npc tamer = harness.SpawnWithAi(Tamer, "ice_claw_tamer", 300f, 300f, 200f);
		Npc ruthless = harness.SpawnWithAi(RuthlessTayga, "ruthless_tayga", 306f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(tamer, ruthless);

		harness.Engage(tamer, raider);
		int afterEngage = ruthless.GetAggroList().GetHate(raider);
		Assert.InRange(afterEngage, 500, 599);

		// Straight to a quarter, without a watch at forty-five first: the tamer's call rides timer zero,
		// and this port does not build the low-priority branch that re-arms that timer when the health
		// guard fails. A watch above the threshold would spend the timer and the call would never come.
		// See docs/retail-ai-fidelity.md -- the fallback is recorded as unbuilt.
		BossAiHarness.SetExactPercent(tamer, 25);
		harness.Watch(10, null);

		Assert.InRange(ruthless.GetAggroList().GetHate(raider),
			afterEngage + 500, afterEngage + 599);
	}

	/// <summary>
	/// <b>And a third is not a half.</b> A tamer sitting at forty-five percent never calls — its guard
	/// is <c>is_hp_lower_than 35</c>, five points tighter than the hunter's, and the difference decides
	/// whether a fight at that health brings five hundred more.
	/// </summary>
	/// <remarks>
	/// A separate fight rather than a second phase of the one above, because the tamer's call rides a
	/// timer this port does not re-arm: the health has to be set before the timer comes round, not
	/// after.
	/// </remarks>
	[Fact]
	public void AndAThirdIsNotAHalf()
	{
		using BossAiHarness harness = NewHarness();
		Npc tamer = harness.SpawnWithAi(Tamer, "ice_claw_tamer", 300f, 300f, 200f);
		Npc ruthless = harness.SpawnWithAi(RuthlessTayga, "ruthless_tayga", 306f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(tamer, ruthless);

		harness.Engage(tamer, raider);
		int afterEngage = ruthless.GetAggroList().GetHate(raider);

		BossAiHarness.SetExactPercent(tamer, 45);
		harness.Watch(12, null);

		Assert.InRange(ruthless.GetAggroList().GetHate(raider), afterEngage, afterEngage + 99);
	}

	/// <summary>
	/// <b>Kill one tayga in front of another and the survivor comes for the killer.</b> Retail's
	/// <c>on_sense_friend_killed_by_user</c>, on both grades — the event the black claw taygas made this
	/// port carry.
	/// </summary>
	[Theory]
	[InlineData(RuthlessTayga, "ruthless_tayga")]
	[InlineData(LesserTayga, "ruthless_tayga_lesser")]
	public void KillOneTaygaAndTheSurvivorComesForTheKiller(int watcherId, string watcherAi)
	{
		using BossAiHarness harness = NewHarness();
		Npc doomed = harness.SpawnWithAi(RuthlessTayga, "ruthless_tayga", 300f, 300f, 200f);
		Npc watcher = harness.SpawnWithAi(watcherId, watcherAi, 302f, 300f, 200f);
		Player killer = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(doomed, watcher);

		Assert.Equal(0, watcher.GetAggroList().GetHate(killer));

		Aion.GameServer.Ai.FriendDeathNotice.Raise(doomed, killer);

		Assert.Equal(100, watcher.GetAggroList().GetHate(killer));
	}

	/// <summary>
	/// <b>And only within fifteen metres</b>, which is retail's range on every call in the camp.
	/// </summary>
	[Fact]
	public void AndOnlyWithinFifteenMetres()
	{
		using BossAiHarness harness = NewHarness();
		Npc hunter = harness.SpawnWithAi(Hunter, "ice_claw_hunter", 300f, 300f, 200f);
		Npc near = harness.SpawnWithAi(RuthlessTayga, "ruthless_tayga", 306f, 300f, 200f);
		Npc far = harness.SpawnWithAi(RuthlessTayga, "ruthless_tayga", 330f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(hunter, near);
		BossAiHarness.MakeMutuallyKnown(hunter, far);

		harness.Engage(hunter, raider);

		Assert.Equal(500, near.GetAggroList().GetHate(raider));
		Assert.Equal(0, far.GetAggroList().GetHate(raider));
	}

	/// <summary><b>The numbers and the payloads are retail's, not ours.</b></summary>
	[Fact]
	public void TheNumbersAreRetails()
	{
		Assert.Equal(7006, IceClawCalls.OnMe);
		Assert.Equal(7007, IceClawCalls.Hurting);
		Assert.Equal(7008, RuthlessTaygaAI.HelpMe);
		Assert.Equal(15f, IceClawCalls.CallReach);
		Assert.Equal(500, IceClawCalls.Ruthless);
		Assert.Equal(100, IceClawCalls.Lesser);
	}
}
