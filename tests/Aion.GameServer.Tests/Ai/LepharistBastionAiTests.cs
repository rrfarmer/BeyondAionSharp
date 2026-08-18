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
	/// <b>The drudges' flee is built and is not pinned, and this says why — twice over.</b>
	/// </summary>
	/// <remarks>
	/// <c>Flee</c> hands a destination to the move controller and this harness advances a clock without
	/// simulating movement, which is the reason every flee in this port is unpinned.
	/// <para>
	/// <b>Its guard is the more interesting loss.</b> Retail flees only when the drudge is below thirty
	/// percent <em>and its attacker is above forty</em> — so <b>a drudge that has nearly killed the
	/// player stays and finishes the job</b>. That is the only guard in this log that judges the fight
	/// rather than the npc, and pinning it needs a player at a chosen health, which this harness has no
	/// helper for: <c>SetExactPercent</c> takes an NPC. The condition is built and kept because retail
	/// wrote it; what is missing is a way to hurt a test player.
	/// </para>
	/// </remarks>
	[Fact(Skip = "flee needs the move controller, and its guard needs a player at a chosen health")]
	public void TheDrudgesFleeIsBuiltAndNotPinned()
	{
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
