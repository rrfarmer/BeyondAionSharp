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
	/// <b>The flee half is built and is not pinned, and this says why.</b>
	/// </summary>
	/// <remarks>
	/// A drakie that sees a player runs from it for three seconds — retail's <c>flee_from</c>, which
	/// this class translates through the new <see cref="Ai.Pattern.AiPattern.Do.FleeFromSeen"/>.
	/// <para>
	/// It cannot be pinned here. <c>Flee</c> computes a destination and hands it to the move
	/// controller, and this harness advances a virtual clock without simulating movement — so the
	/// drakie's position does not change however long the clock runs, whether the branch fired or not.
	/// A pin asserting it had moved would fail for correct code, and one asserting it had not would
	/// pass for broken code.
	/// </para>
	/// <para>
	/// <b>What the attempt did find is worth more than the pin.</b> <c>Do.Flee</c> reads
	/// <c>CurrentTarget</c>, so it is a no-op for an npc that has never fought — which is exactly the
	/// creature a flee action exists for. <c>Do.FleeFromSeen</c> reads what came into view instead, and
	/// is the faithful translation of <c>from=OBJI_SEEN</c>.
	/// </para>
	/// </remarks>
	[Fact(Skip = "flee moves the npc through the move controller, which this harness does not simulate")]
	public void TheFleeHalfIsBuiltAndNotPinned()
	{
	}

	/// <summary><b>The message number is retail's, not ours.</b></summary>
	[Fact]
	public void TheMessageNumberIsRetails()
	{
		Assert.Equal(6511, DrakeMarkAI.AllOfYou);
	}
}
