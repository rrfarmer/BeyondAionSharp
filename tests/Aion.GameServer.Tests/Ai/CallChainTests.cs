using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Tests.Ai;

/// <summary>Shouts when it is hit. The head of the chain.</summary>
[AIName("chain_probe_caller")]
public class ChainProbeCallerAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(Branch(1, "call, once", [When.FirstTime(1)],
			Do.BroadcastAboutAttacker(CallChainTests.First, 30f))),
	};

	public ChainProbeCallerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Answers with <c>add_hate_point</c> — no target switch — and shouts again on entering the fight.
/// </summary>
/// <remarks>
/// This is the shape every chained call in the retail data has: the answer puts the caller's enemy on
/// this NPC's hate list, and it is <em>entering combat</em> that makes it call in turn.
/// </remarks>
[AIName("chain_probe_relay")]
public class ChainProbeRelayAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(1, "heard the first", [When.Message(CallChainTests.First)],
			Do.HateMessageParam(CallChainTests.Payload))),

		OnEnterAttack = Of(Branch(1, "and pass it on", [],
			Do.Broadcast(CallChainTests.Second, 30f, aboutTarget: true))),
	};

	public ChainProbeRelayAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>The far end, out of the caller's reach and only inside the relay's.</summary>
[AIName("chain_probe_tail")]
public class ChainProbeTailAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(1, "heard the second", [When.Message(CallChainTests.Second)],
			Do.HateMessageParam(CallChainTests.Payload))),
	};

	public ChainProbeTailAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Pins the step every chained call in the retail data depends on: <b>an NPC given hate by a call joins
/// the fight, and joining the fight is what makes it call in turn.</b>
/// </summary>
/// <remarks>
/// <c>AggroList.AddHate</c> ends in <c>CreatureController.OnAddHate</c>, which raises an
/// <c>Attack</c> event on the owner — so hate alone is meant to be enough. That claim had never been
/// pinned, and an encounter that appeared to disprove it (the Ophidan bridge chain) was using
/// <c>Do.HateMessageTarget</c>, whose forced target reached the same place by a different road.
/// <para>
/// These pins use the faithful action, <c>Do.HateMessageParam</c>, so the chain here is carried by hate
/// and nothing else. They live apart from any one encounter because the property belongs to the engine.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class CallChainTests
{
	internal const int First = 60101;
	internal const int Second = 60102;
	internal const int Payload = 7;

	private const int Map = 300250000;
	private const int Body = 217182;

	private static BossAiHarness Harness() =>
		BossAiHarness.For(Map).WithWorldSize(2048)
			.WithAi(typeof(ChainProbeCallerAI), typeof(ChainProbeRelayAI), typeof(ChainProbeTailAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>Hate alone brings an NPC into the fight.</b> The relay is never targeted at and never hit; it
	/// is only named by somebody else's call, and that is enough to fire its entry branch.
	/// </summary>
	[Fact]
	public void HateAloneBringsAnNpcIntoTheFight()
	{
		using BossAiHarness harness = Harness();
		Npc caller = harness.SpawnWithAi(Body, "chain_probe_caller", 300f, 300f, 200f);
		Npc relay = harness.SpawnWithAi(Body, "chain_probe_relay", 310f, 300f, 200f);
		Npc tail = harness.SpawnWithAi(Body, "chain_probe_tail", 320f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(caller, relay);
		BossAiHarness.MakeMutuallyKnown(relay, tail);
		BossAiHarness.MakeMutuallyKnown(relay, raider);
		BossAiHarness.MakeMutuallyKnown(tail, raider);

		harness.Engage(caller, raider);

		// The relay took the call...
		Assert.True(relay.GetAggroList().GetHate(raider) > 0, "the call never reached the relay");
		// ...and the tail heard the relay's own, which only an entry into combat can have sent.
		Assert.Equal(Payload, tail.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And the answer does not turn the relay.</b> Retail's <c>add_hate_point</c> leaves the target
	/// alone; the relay joins the fight through its aggro list, not by being pointed at somebody.
	/// </summary>
	/// <remarks>
	/// The distinction matters because the two are easy to confuse from outside: a forced target also
	/// produces a fighting NPC, which is how a chain built on the wrong action passed its pins.
	/// </remarks>
	[Fact]
	public void AndTheAnswerDoesNotTurnIt()
	{
		using BossAiHarness harness = Harness();
		Npc caller = harness.SpawnWithAi(Body, "chain_probe_caller", 300f, 300f, 200f);
		Npc relay = harness.SpawnWithAi(Body, "chain_probe_relay", 310f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		Player busy = harness.SpawnPlayer(312f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(caller, relay);
		BossAiHarness.MakeMutuallyKnown(relay, raider);
		BossAiHarness.MakeMutuallyKnown(relay, busy);

		// The relay is already fighting somebody it hates far more than the call is worth.
		harness.Engage(relay, busy);
		Assert.Same(busy, relay.GetTarget());

		harness.Engage(caller, raider);

		Assert.True(relay.GetAggroList().GetHate(raider) > 0, "the call never landed");
		Assert.Same(busy, relay.GetTarget());
	}

}
