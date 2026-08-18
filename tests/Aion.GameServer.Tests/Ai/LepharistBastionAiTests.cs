using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the Lepharist bastion, translated from retail patterns <c>NLehpar_KnA</c> and
/// <c>NLehpar_LnA</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class LepharistBastionAiTests
{
	private const int Heiron = 210040000;

	private const int Defender = 211013;
	private const int Drudge = 211665;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Heiron).WithWorldSize(2048)
			.WithAi(typeof(LepharistDefenderAI), typeof(BastionDrudgeAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>Five metres is the shortest call in this log.</b> A defender being pulled tells whoever is
	/// standing on top of it and nobody else, and what it buys is a single hate point.
	/// </summary>
	[Fact]
	public void TheWhisperCarriesFiveMetresAndBuysOnePoint()
	{
		using BossAiHarness harness = NewHarness();
		Npc defender = harness.SpawnWithAi(Defender, "lepharist_defender", 300f, 300f, 200f);
		Npc close = harness.SpawnWithAi(Drudge, "bastion_drudge", 303f, 300f, 200f);
		Npc justOutside = harness.SpawnWithAi(Drudge, "bastion_drudge", 308f, 300f, 200f);
		Player raider = harness.SpawnPlayer(301f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(defender, close);
		BossAiHarness.MakeMutuallyKnown(defender, justOutside);

		harness.Engage(defender, raider);

		Assert.Equal(1, close.GetAggroList().GetHate(raider));
		Assert.Equal(0, justOutside.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And the shout it cannot make yet is worth a hundred.</b> The drudges' answer to <c>1018</c>
	/// is built and pinned even though nothing our data places can send it — the sender's branch is
	/// gated on <c>is_skill_count_left</c>, which this port cannot express.
	/// </summary>
	/// <remarks>
	/// Pinned by sending the message directly. That is worth doing rather than leaving the branch
	/// untested: the day the charge guard becomes expressible, the half that answers is already known
	/// to work.
	/// </remarks>
	[Fact]
	public void TheShoutTheyCannotHearYetIsWorthAHundred()
	{
		using BossAiHarness harness = NewHarness();
		Npc drudge = harness.SpawnWithAi(Drudge, "bastion_drudge", 300f, 300f, 200f);
		Npc other = harness.SpawnWithAi(Drudge, "bastion_drudge", 303f, 300f, 200f);
		Player raider = harness.SpawnPlayer(301f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(drudge, other);
		BossAiHarness.MakeMutuallyKnown(other, raider);
		harness.Engage(drudge, raider);

		Aion.GameServer.Ai.NpcMessageBus.Broadcast(drudge, LepharistCalls.Shout, raider, 20f);

		Assert.Equal(100, other.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A drudge below thirty percent runs from a healthy attacker.</b> Retail's <c>flee_from</c>,
	/// five seconds, once.
	/// </summary>
	/// <remarks>
	/// <b>The half of this that is still unpinned is the interesting half.</b> The flee itself is
	/// observable through <c>PatternAi.FleeingTo</c>; what cannot be shown here is the negative case —
	/// that a drudge whose attacker is <em>below</em> forty percent stays and finishes the job — because
	/// that needs a player at a chosen health and the harness's <c>SetExactPercent</c> takes an NPC.
	/// <b>A way to hurt a test player is the missing piece</b>, and it is the only thing between this
	/// file and a complete pin on the one guard in this log that judges the fight rather than the npc.
	/// </remarks>
	[Fact]
	public void ADrudgeBelowThirtyRunsFromAHealthyAttacker()
	{
		using BossAiHarness harness = NewHarness();
		Npc drudge = harness.SpawnWithAi(Drudge, "bastion_drudge", 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(310f, 300f, 200f, race: Race.ELYOS);
		harness.Engage(drudge, raider);

		Aion.GameServer.Ai.Pattern.PatternAi ai =
			Assert.IsAssignableFrom<Aion.GameServer.Ai.Pattern.PatternAi>(drudge.GetAi());
		Assert.Null(ai.FleeingTo);

		BossAiHarness.SetExactPercent(drudge, 20);
		drudge.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		(float X, float Y)? destination = ai.FleeingTo;
		Assert.NotNull(destination);
		Assert.True(destination.Value.X < 300f, "the drudge fled towards its attacker");
	}

	/// <summary>
	/// <b>And a drudge that has nearly killed the player stays and finishes the job.</b> Retail's guard
	/// is <c>is_hp_in_boundary who=OBJI_CUR_TARGET larger_than=40</c> — the only condition in this log
	/// that judges the fight rather than the npc.
	/// </summary>
	/// <remarks>
	/// This is the assertion the previous entry recorded as blocked. It needed a player at a chosen
	/// health, and <c>SetExactPercent</c> took an <c>Npc</c>; it now takes a <c>Creature</c>, which is
	/// the whole fix. <b>One signature was the difference between a guard nobody could test and a guard
	/// with both halves pinned.</b>
	/// </remarks>
	[Fact]
	public void ADrudgeStaysWhenItsAttackerIsNearlyDead()
	{
		using BossAiHarness harness = NewHarness();
		Npc drudge = harness.SpawnWithAi(Drudge, "bastion_drudge", 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(310f, 300f, 200f, race: Race.ELYOS);
		harness.Engage(drudge, raider);

		Aion.GameServer.Ai.Pattern.PatternAi ai =
			Assert.IsAssignableFrom<Aion.GameServer.Ai.Pattern.PatternAi>(drudge.GetAi());

		BossAiHarness.SetExactPercent(drudge, 20);
		BossAiHarness.SetExactPercent(raider, 30);
		drudge.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Null(ai.FleeingTo);
	}

	/// <summary><b>The numbers and the ranges are retail's, not ours.</b></summary>
	[Fact]
	public void TheNumbersAreRetails()
	{
		Assert.Equal(1017, LepharistCalls.Whisper);
		Assert.Equal(1018, LepharistCalls.Shout);
		Assert.Equal(1016, LepharistCalls.Rallied);
		Assert.Equal(5f, LepharistCalls.WhisperReach);
		Assert.Equal(10f, LepharistCalls.RallyReach);
	}
}
