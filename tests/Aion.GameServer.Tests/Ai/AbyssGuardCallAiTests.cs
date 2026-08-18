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
		Player player = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);
		Assert.Null(listener.GetTarget());

		harness.Engage(crier, player);

		Assert.Same(player, listener.GetTarget());
	}

	/// <summary><b>And only within its own range.</b> The ranger's cry carries twenty-five metres.</summary>
	[Fact]
	public void AndOnlyWithinItsOwnRange()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(Crier, 300f, 300f, 200f);
		Npc near = harness.Spawn(Listener, 320f, 300f, 200f);
		Npc far = harness.Spawn(SecondListener, 340f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(crier, near);
		BossAiHarness.MakeMutuallyKnown(crier, far);

		harness.Engage(crier, player);

		Assert.Same(player, near.GetTarget());
		Assert.Null(far.GetTarget());
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
		Player player = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);

		harness.Engage(crier, player);

		Assert.Same(player, listener.GetTarget());
	}

	/// <summary><b>A guard that only listens never cries.</b> Most of them are of that kind.</summary>
	[Fact]
	public void AGuardThatOnlyListensNeverCries()
	{
		using BossAiHarness harness = NewHarness();
		Npc pulled = harness.Spawn(Listener, 300f, 300f, 200f);
		Npc beside = harness.Spawn(SecondListener, 310f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(pulled, beside);

		harness.Engage(pulled, player);

		Assert.Null(beside.GetTarget());
	}

	/// <summary>
	/// <b>An idle guard answers the call by turning on the player named.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>point_to_add</c> here is <c>1</c> in every one of the forty-seven answering
	/// patterns, and <b>that one point is not pinned, because our engine will not keep it.</b> A guard
	/// given a single hate point and nothing else has no reason to stay in the fight: it goes home on
	/// the next think and <c>AggroList</c> clears itself on the way, so the value is gone before any
	/// assertion can read it. That is arguably retail's intent expressed by our engine — one point is a
	/// nudge to join, not a claim on the player, and a guard with no other reason to fight will drift
	/// back — but it is a behaviour we inferred rather than one we measured, and it is written up in
	/// the log as such.
	/// <para>
	/// The call is delivered by hand from a guard that is not fighting, because a real pull puts the
	/// listener into combat first — our engine's own see-a-friend-attacked does that before the message
	/// arrives — and then retail's <em>fighting</em> half runs instead.
	/// </para>
	/// </remarks>
	[Fact]
	public void AnIdleGuardTurnsOnThePlayerNamed()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(Crier, 300f, 300f, 200f);
		Npc listener = harness.Spawn(Listener, 320f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);
		Assert.Null(listener.GetTarget());

		NpcMessageBus.Broadcast(crier, AbyssGuardCallAI.CallForHelp, player, 25f);

		Assert.Same(player, listener.GetTarget());
	}

	/// <summary>
	/// <b>A guard already fighting only turns.</b> Retail splits the answer on npc state, and the
	/// fighting half takes no hate at all — so the player it was already on keeps it.
	/// </summary>
	[Fact]
	public void AGuardAlreadyFightingOnlyTurns()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(Crier, 300f, 300f, 200f);
		Npc listener = harness.Spawn(Listener, 320f, 300f, 200f);
		Player pulled = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		Player itsOwn = harness.SpawnPlayer(322f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);

		// Its own fight first. The player the cry names is one the listener has never seen.
		harness.Engage(listener, itsOwn);
		harness.Engage(crier, pulled);

		// It turns — and the turn carried no hate at all, so its own fight is untouched.
		Assert.Same(pulled, listener.GetTarget());
		Assert.Equal(0, listener.GetAggroList().GetHate(pulled));
		Assert.True(listener.GetAggroList().GetHate(itsOwn) > 0);
	}
}
