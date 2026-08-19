using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Sematariux's thunder shields, which used to be pulled one at a time.
/// </summary>
/// <remarks>
/// All three retail patterns (<c>LF4_DramataG1</c>..<c>G3</c>) carry the same pair: entering attack
/// state broadcasts 10010 to fifty metres naming whoever pulled them, and <c>on_message 10010</c>
/// answers with <c>add_hate_point 1</c> and <c>attack_most_hating</c>. Six shields stand around the
/// boss and each splits twice, so the call is what turns twenty-four objects into a field.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SematariuxThunderShieldTests
{
	private const int Inggison = 210050000;

	/// <summary>The three sizes, largest first. They share one AI and one message.</summary>
	private const int Large = 281931;
	private const int Medium = 281932;
	private const int Small = 281933;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Inggison).WithWorldSize(2048)
			.WithAi(typeof(SematariuxThunderShieldAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>Pulling one shield brings its neighbour.</b>
	/// </summary>
	/// <remarks>
	/// The neighbour is never touched — only the message reaches it — so nothing but the call can put it
	/// into the fight.
	/// </remarks>
	[Fact]
	public void PullingOneShieldBringsItsNeighbour()
	{
		using BossAiHarness harness = NewHarness();
		Npc pulled = harness.Spawn(Large, 120f, 2130f, 441f);
		Npc neighbour = harness.Spawn(Large, 140f, 2130f, 441f);
		Player player = harness.SpawnPlayer(122f, 2130f, 441f);
		BossAiHarness.MakeMutuallyKnown(pulled, neighbour);
		BossAiHarness.MakeMutuallyKnown(neighbour, player);
		Assert.Null(neighbour.GetTarget());

		pulled.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CREATURE_AGGRO, player);

		Assert.Equal(player, neighbour.GetTarget());
	}

	/// <summary>
	/// <b>Every size answers, and every size calls.</b>
	/// </summary>
	/// <remarks>
	/// The two smaller sizes only exist after a split, which is when the field is most crowded — a call
	/// implemented on the large one alone would go quiet exactly when it matters most.
	/// </remarks>
	[Theory]
	[InlineData(Large, Small)]
	[InlineData(Medium, Large)]
	[InlineData(Small, Medium)]
	public void EverySizeAnswersAndEverySizeCalls(int caller, int answerer)
	{
		using BossAiHarness harness = NewHarness();
		Npc pulled = harness.Spawn(caller, 120f, 2130f, 441f);
		Npc neighbour = harness.Spawn(answerer, 140f, 2130f, 441f);
		Player player = harness.SpawnPlayer(122f, 2130f, 441f);
		BossAiHarness.MakeMutuallyKnown(pulled, neighbour);
		BossAiHarness.MakeMutuallyKnown(neighbour, player);

		pulled.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CREATURE_AGGRO, player);

		Assert.Equal(player, neighbour.GetTarget());
	}

	/// <summary>
	/// <b>A shield past fifty metres does not hear it.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>range_as_meter</c> is fifty.
	/// <para>
	/// <b>This shows that a distant shield stays out, but it does not pin the fifty.</b> Measured across
	/// the harness: a shield at forty-five units hears the call and one at fifty-five does not —
	/// <b>whether the constant is fifty or five hundred</b>. The message bus's own reach ends within a
	/// few units of retail's number, so widening the range is invisible here and the mutation that does
	/// it survives. The constant is held by review, not by this pin.
	/// </para>
	/// </remarks>
	[Fact]
	public void AShieldPastFiftyMetresDoesNotHearIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc pulled = harness.Spawn(Large, 120f, 2130f, 441f);
		Npc distant = harness.Spawn(Large, 185f, 2130f, 441f);
		Player player = harness.SpawnPlayer(122f, 2130f, 441f);
		BossAiHarness.MakeMutuallyKnown(pulled, distant);
		BossAiHarness.MakeMutuallyKnown(distant, player);

		pulled.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CREATURE_AGGRO, player);

		Assert.Null(distant.GetTarget());
	}

	/// <summary>
	/// <b>And a shield ignores a message that is not the call.</b>
	/// </summary>
	/// <remarks>
	/// Message numbers are per encounter. Sematariux's own wake-up broadcasts 7021 to thirty metres,
	/// which a shield keyed on "any message" would take as an order to attack whatever it named.
	/// </remarks>
	[Fact]
	public void AndAShieldIgnoresAMessageThatIsNotTheCall()
	{
		using BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(Large, 120f, 2130f, 441f);
		Npc shield = harness.Spawn(Large, 140f, 2130f, 441f);
		Player player = harness.SpawnPlayer(122f, 2130f, 441f);
		BossAiHarness.MakeMutuallyKnown(shield, player);
		// Without this the broadcast never reaches the shield and the pin holds for a class that
		// answers everything -- which is exactly how it passed the first time.
		BossAiHarness.MakeMutuallyKnown(caller, shield);

		NpcMessageBus.Broadcast(caller, 7021, player, 50f);

		Assert.Null(shield.GetTarget());
	}
}
