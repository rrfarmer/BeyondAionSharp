using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the marked drakes and their drakies, translated from retail patterns <c>ND2_Bst_38</c> and
/// <c>ND2_Bst_41</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class DrakeMarkAiTests
{
	private const int Theobomos = 210050000;

	private const int LonghornDrake = 215605;
	private const int LonghornDrakie = 215606;
	private const int NadukaWardrake = 216033;
	private const int NadukaDrakie = 216035;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Theobomos).WithWorldSize(2048)
			.WithAi(typeof(DrakeMarkAI), typeof(DrakieMarkAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>A drake fighting a raider, with one of its drakies close enough to hear it.</summary>
	private static (BossAiHarness, Npc, Npc, Player) Nest(
		int drakeId = LonghornDrake, int drakieId = LonghornDrakie)
	{
		BossAiHarness harness = NewHarness();
		Npc drake = harness.Spawn(drakeId, 300f, 300f, 200f);
		Npc drakie = harness.Spawn(drakieId, 305f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(drake, drakie);
		harness.Engage(drake, raider);
		return (harness, drake, drakie, raider);
	}

	/// <summary>
	/// <b>Below half health the drake calls, and its drakies come.</b> The drake alone is an ordinary
	/// monster and the drakies alone are harmless; the call is the mechanic.
	/// </summary>
	[Theory]
	[InlineData(LonghornDrake, LonghornDrakie)]
	[InlineData(NadukaWardrake, NadukaDrakie)]
	public void BelowHalfTheDrakeCallsAndItsDrakiesCome(int drakeId, int drakieId)
	{
		var (harness, drake, drakie, raider) = Nest(drakeId, drakieId);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(drake, 60);
		drake.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		Assert.Equal(0, drakie.GetAggroList().GetHate(raider));

		BossAiHarness.SetExactPercent(drake, 40);
		drake.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(100, drakie.GetAggroList().GetHate(raider));
		Assert.Same(raider, drakie.GetTarget());
	}

	/// <summary>
	/// <b>A spell calls them too, and the flag is shared.</b> Retail writes the branch on both handlers
	/// with one <c>FLAGVARI_ALPHA_1</c> across them.
	/// </summary>
	[Fact]
	public void ASpellCallsThemTooAndTheFlagIsShared()
	{
		var (harness, drake, drakie, raider) = Nest();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(drake, 40);
		drake.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);
		Assert.Equal(100, drakie.GetAggroList().GetHate(raider));

		drake.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(100, drakie.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And only within twelve metres</b>, which is retail's range — so a drake calls its own nest and
	/// not the next one along.
	/// </summary>
	[Fact]
	public void AndOnlyWithinTwelveMetres()
	{
		var (harness, drake, drakie, raider) = Nest();
		using BossAiHarness _h = harness;

		Npc distant = harness.Spawn(LonghornDrakie, 330f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(drake, distant);

		BossAiHarness.SetExactPercent(drake, 40);
		drake.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(100, drakie.GetAggroList().GetHate(raider));
		Assert.Equal(0, distant.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A drakie that sees a player runs from it.</b> Retail's <c>flee_from</c> with
	/// <c>from=OBJI_SEEN</c> — from what came into view, not from whatever it was fighting.
	/// </summary>
	/// <remarks>
	/// <b>The distinction this pin exists for.</b> <c>Do.Flee</c> reads <c>CurrentTarget</c>, so it is a
	/// no-op for an npc that has never fought — which is exactly the creature a flee action exists for.
	/// <c>Do.FleeFromSeen</c> reads what came into view instead. A drakie has no target when it runs,
	/// so only one of those two can be right, and this pin is what says which.
	/// <para>
	/// It was skipped as impossible in four files. <c>PatternAi.FleeingTo</c> records the destination
	/// the flee computed and is public: the movement is unobservable, the decision never was.
	/// </para>
	/// </remarks>
	[Fact]
	public void ADrakieThatSeesAPlayerRunsFromIt()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc drakie = harness.Spawn(LonghornDrakie, 300f, 300f, 200f);
		// Inside the drakie's own srange of seven. Ten metres worked only while on_see_user ignored
		// sight range, which is the thing this pin is now written against.
		Player passer = harness.SpawnPlayer(305f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(drakie, passer);

		Aion.GameServer.Ai.Pattern.PatternAi ai =
			Assert.IsAssignableFrom<Aion.GameServer.Ai.Pattern.PatternAi>(drakie.GetAi());

		// No explicit sighting is needed: MakeMutuallyKnown is itself what a drakie reacts to, which is
		// the mechanic working rather than the pin cheating. Raised again anyway, so the assertion below
		// is about the branch and not about the setup.
		Assert.Null(drakie.GetTarget());
		drakie.GetAi().OnCreatureEvent(AiEventType.CreatureSee, passer);

		(float X, float Y)? destination = ai.FleeingTo;
		Assert.NotNull(destination);

		// It has no target, so a target-based flee would have done nothing at all.
		Assert.True(destination.Value.X < 300f, "the drakie ran towards the player it saw");
	}

	/// <summary><b>The message number is retail's, not ours.</b></summary>
	[Fact]
	public void TheMessageNumberIsRetails()
	{
		Assert.Equal(6511, DrakeMarkAI.AllOfYou);
	}
}
