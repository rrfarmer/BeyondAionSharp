using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Tests.Ai;

/// <summary>A test-only caller: broadcasts <c>90001</c> when pulled.</summary>
[AIName("relay_probe_caller")]
public class RelayProbeCallerAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(1, "pulled", [], Do.Broadcast(90001, 50f, aboutTarget: true))),
	};

	public RelayProbeCallerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>A test-only relay: hears <c>90001</c>, re-broadcasts <c>90002</c> a second later.</summary>
[AIName("relay_probe_relay")]
public class RelayProbeRelayAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(2, "heard it, passing it on in a second", [When.Message(90001)],
			Do.ArmTimer(10, 1_000))),

		OnBattleTimer = Of(Branch(1, "passing it on", [When.Timer(10)],
			Do.Broadcast(90002, 50f, aboutTarget: true))),
	};

	public RelayProbeRelayAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>A test-only listener: answers <c>90002</c> with a hundred.</summary>
[AIName("relay_probe_listener")]
public class RelayProbeListenerAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(1, "heard the relay", [When.Message(90002)],
			Do.HateMessageTarget(100))),
	};

	public RelayProbeListenerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Pins for <b>message relay</b> — one NPC hearing a broadcast and re-broadcasting it from a battle
/// timer, so a call reaches beyond the caller's own circle.
/// </summary>
/// <remarks>
/// <b>These exist because a real encounter could not be shipped without them.</b> The shulack
/// mercenaries of the Danuar Sanctuary are built on exactly this shape and their second hop would not
/// go green; without a pin on the primitive there was no way to tell an engine limitation from a
/// mistake in the translation. See docs/retail-ai-fidelity.md.
/// <para>
/// <b>They rule the engine out.</b> Relay works across every combination tried — near and far, two
/// different maps, and npc ids from both encounters — so a relay that fails in a specific class is that
/// class's problem. That is worth a permanent test rather than a throwaway probe: the next relay
/// encounter will want it, and so would anyone reading the entry that describes the failure.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class MessageRelayTests
{
	private const int Altgard = 220030000;
	private const int DanuarSanctuary = 301500000;

	/// <summary>
	/// <b>A message relays: the listener is pulled in by the relay, not by the caller.</b> Tried at
	/// six metres out to forty-five, on two maps, with npc ids from two different encounters.
	/// </summary>
	[Theory]
	[InlineData(Altgard, 210160, 210161, 210145, 6f)]
	[InlineData(Altgard, 210160, 210161, 210145, 45f)]
	[InlineData(DanuarSanctuary, 210160, 210161, 210145, 40f)]
	[InlineData(DanuarSanctuary, 235656, 235565, 235589, 40f)]
	[InlineData(Altgard, 235656, 235565, 235589, 40f)]
	public void AMessageRelaysThroughTheMiddleNpc(
		int map, int callerId, int relayId, int listenerId, float listenerAt)
	{
		using BossAiHarness harness = BossAiHarness.For(map).WithWorldSize(2048)
			.WithAi(typeof(RelayProbeCallerAI), typeof(RelayProbeRelayAI), typeof(RelayProbeListenerAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

		Npc caller = harness.SpawnWithAi(callerId, "relay_probe_caller", 300f, 300f, 200f);
		Npc relay = harness.SpawnWithAi(relayId, "relay_probe_relay", 304f, 300f, 200f);
		Npc listener = harness.SpawnWithAi(
			listenerId, "relay_probe_listener", 304f + listenerAt, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(caller, relay);
		BossAiHarness.MakeMutuallyKnown(relay, listener);
		BossAiHarness.MakeMutuallyKnown(listener, raider);

		// The relay carries its hop on a battle timer, and battle timers only run in combat.
		harness.Engage(relay, raider);
		harness.Engage(caller, raider);

		Assert.True(listener.GetAggroList().GetHate(raider) < 100, "the listener heard the caller");

		harness.Watch(4, null);

		Assert.True(listener.GetAggroList().GetHate(raider) >= 100,
			"the relay did not reach the listener");
	}
}
