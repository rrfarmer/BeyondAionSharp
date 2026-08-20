using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="AbyssGuardCallAI"/> — retail message <c>23000</c> across fifty-two guard
/// patterns (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The largest single mechanic in the dump by npc count: three hundred and ninety live guards, fifty
/// of whom cry out as they are pulled and three hundred and eighty-five of whom answer.
/// <para>
/// <b>The players here are Asmodian and the guards are Elyos.</b> Retail guards both halves of the
/// answer with <c>is_enemy</c>, and our aggro list enforces the same thing from underneath —
/// <c>AddHate</c> does nothing for a creature that is not an enemy of the owner. A pin written with the
/// default Elyos player measures a guard refusing to attack its own side, which is correct behaviour
/// and not the mechanic.
/// </para>
/// <para>
/// Every pin
/// keeps the player <b>out of the listener's known list entirely</b>. Introducing the two is enough for
/// an aggressive guard to find the player by itself, which made the first version of these pins pass
/// whether or not the call was ever sent — the same mistake as the decoy that aggroed, in a new place.
/// The call reaches its listener through the crier's known list and puts hate on a player the listener
/// has never seen, which is exactly what a broadcast is for.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AbyssGuardCallAiTests
{
	private const int Reshanta = 400010000;

	/// <summary>A theobomos elite ranger: cries at twenty-five metres, and answers.</summary>
	private const int Crier = 209417;

	/// <summary>A theobomos elite templar: answers and never cries.</summary>
	private const int Listener = 209415;

	/// <summary>A theobomos elite gladiator, so two listeners can be told apart.</summary>
	private const int SecondListener = 209416;

	/// <summary>A pashid bind point commander: cries at fifty metres, and does not answer.</summary>
	private const int FarCrier = 881599;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(AbyssGuardCallAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary><b>Pulling a crier brings the guard beside it onto the same player.</b></summary>
	[Fact]
	public void PullingACrierBringsTheGuardBesideIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(Crier, 300f, 300f, 200f);
		Npc listener = harness.Spawn(Listener, 320f, 300f, 200f);
		Player player = harness.SpawnPlayer(318f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);
		Assert.Equal(0, listener.GetAggroList().GetHate(player));

		harness.Engage(crier, player);

		Assert.True(listener.GetAggroList().GetHate(player) > 0);
	}

	/// <summary><b>And only within its own range.</b> The ranger's cry carries twenty-five metres.</summary>
	[Fact]
	public void AndOnlyWithinItsOwnRange()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(Crier, 300f, 300f, 200f);
		Npc near = harness.Spawn(Listener, 320f, 300f, 200f);
		Npc far = harness.Spawn(SecondListener, 340f, 300f, 200f);
		Player player = harness.SpawnPlayer(318f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(crier, near);
		BossAiHarness.MakeMutuallyKnown(crier, far);

		harness.Engage(crier, player);

		Assert.True(near.GetAggroList().GetHate(player) > 0);
		Assert.Equal(0, far.GetAggroList().GetHate(player));
	}

	/// <summary>
	/// <b>A commander's cry carries twice as far.</b> Retail gives the range per guard, and the
	/// forty-metre listener that a ranger cannot reach is well inside a commander's fifty.
	/// </summary>
	/// <remarks>
	/// Written first with an ahserion troopers commander, which is one of the twenty-two guards in the
	/// roster that already had a bespoke class and was therefore left alone — so it does not cry, and
	/// the pin failed for the reason the log records rather than for the reason it was testing.
	/// </remarks>
	[Fact]
	public void ACommandersCryCarriesTwiceAsFar()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(FarCrier, 300f, 300f, 200f);
		Npc listener = harness.Spawn(Listener, 340f, 300f, 200f);
		Player player = harness.SpawnPlayer(338f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);

		harness.Engage(crier, player);

		Assert.True(listener.GetAggroList().GetHate(player) > 0);
	}

	/// <summary><b>A guard that only listens never cries.</b> Most of them are of that kind.</summary>
	[Fact]
	public void AGuardThatOnlyListensNeverCries()
	{
		using BossAiHarness harness = NewHarness();
		Npc pulled = harness.Spawn(Listener, 300f, 300f, 200f);
		Npc beside = harness.Spawn(SecondListener, 310f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(pulled, beside);

		harness.Engage(pulled, player);

		Assert.Equal(0, beside.GetAggroList().GetHate(player));
	}

	/// <summary>
	/// <b>An idle guard answers the call with hate, not by turning.</b> Retail's idle rung is
	/// <c>add_hate_point points_to_add=1</c> followed by <c>attack_most_hating</c> — never
	/// <c>switch_target</c> — in all 85 of the answering patterns.
	/// </summary>
	/// <remarks>
	/// Retail's one point is a nudge to join, not a claim on the player: the guard goes for whoever it
	/// hates most, which is not necessarily the player just named. That is the whole difference from the
	/// busy rung, which turns unconditionally.
	/// <para>
	/// An earlier version of this pin asserted that the guard <em>turns</em>, which passed only because
	/// the class used <c>switch_target</c> — so the pin was pinning the defect. The note it carried, that
	/// "our engine will not keep" the single point, was an inference and it was wrong: the point is kept.
	/// It vanished because the player stood fifty metres from the listener, and <c>CheckGiveupDistance</c>
	/// fires inside the add — <c>AddHate</c> → <c>OnAddHate</c> → attack event → target-too-far →
	/// giveup → <c>StopHating</c>, all before the assertion runs. Within a real call radius it holds.
	/// </para>
	/// <para>
	/// <b>What this pin cannot check:</b> that the guard does not <em>turn</em>. Adding hate raises the
	/// attack event, and the engine then targets whoever it hates most — which, for a guard that was
	/// idle and so had no hate at all, is the player just named. Plain and switching forms are therefore
	/// indistinguishable here, and a mutation back to <c>switch_target</c> on this rung does not die. The
	/// correction rests on retail's 85 patterns, not on this pin; the busy rung below is the half that
	/// mutation testing can hold.
	/// </para>
	/// </remarks>
	[Fact]
	public void AnIdleGuardTakesHateWithoutTurning()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(Crier, 300f, 300f, 200f);
		Npc listener = harness.Spawn(Listener, 320f, 300f, 200f);
		Player player = harness.SpawnPlayer(318f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);
		BossAiHarness.MakeMutuallyKnown(listener, player);
		Assert.Null(listener.GetTarget());

		NpcMessageBus.Broadcast(crier, AbyssGuardCallAI.CallForHelp, player, 25f);

		// The one point retail gives it, and nothing more.
		Assert.Equal(1, listener.GetAggroList().GetHate(player));
	}

	/// <summary>
	/// <b>A guard already fighting turns, and carries a hundred points with it.</b> Retail guards this
	/// rung with <c>is_npc_state NPC_STATE_ATTACK</c> and answers with
	/// <c>switch_target points_to_add=100</c> in all 85 of them.
	/// </summary>
	/// <remarks>
	/// The old code switched with <b>no</b> hate at all, so the guard faced a player it had no standing
	/// quarrel with and drifted back to its own attacker on the next hit. A hundred points is what makes
	/// the switch survive.
	/// </remarks>
	[Fact]
	public void AGuardAlreadyFightingTurnsAndOutranks()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(Crier, 300f, 300f, 200f);
		Npc listener = harness.Spawn(Listener, 320f, 300f, 200f);
		Player pulled = harness.SpawnPlayer(319f, 300f, 200f, race: Race.ASMODIANS);
		Player itsOwn = harness.SpawnPlayer(322f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);

		// Its own fight first. The player the cry names is one the listener has never seen.
		harness.Engage(listener, itsOwn);
		harness.Engage(crier, pulled);

		Assert.Same(pulled, listener.GetTarget());
		Assert.Equal(100, listener.GetAggroList().GetHate(pulled));
		Assert.True(listener.GetAggroList().GetHate(itsOwn) > 0);
	}
}
