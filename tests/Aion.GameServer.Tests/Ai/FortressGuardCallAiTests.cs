using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the fortress guard call, translated from retail patterns <c>F5_PvP_DGuard_Ra_Ae_Broad</c>,
/// <c>F5_PvPLight_DGuard_Ra_An_Broad</c> and the three <c>_Kn_</c> answerers (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class FortressGuardCallAiTests
{
	private const int Reshanta = 400010000;

	// Asmodian side: a caller and an answerer that share a fortress.
	private const int ArchonPatrol = 209672;
	private const int ArchonVeteranPatrol = 209675;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(2048)
			.WithAi(typeof(FortressGuardCallAI), typeof(FortressGuardAnswerAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, Npc, Player) Post(
		int callerId, int answererId, Race raiderRace)
	{
		BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(callerId, 300f, 300f, 200f);
		Npc answerer = harness.Spawn(answererId, 310f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: raiderRace);
		BossAiHarness.MakeMutuallyKnown(caller, answerer);
		return (harness, caller, answerer, raider);
	}

	/// <summary>
	/// <b>Pull one guard and every guard within twenty-five metres comes.</b> This is the fortress
	/// aggro mechanic, and without it a raid picks guards off one at a time.
	/// </summary>
	[Fact]
	public void PullOneGuardAndThePostComes()
	{
		var (harness, caller, answerer, raider) =
			Post(ArchonPatrol, ArchonVeteranPatrol, Race.ELYOS);
		using BossAiHarness _h = harness;

		Assert.Equal(0, answerer.GetAggroList().GetHate(raider));

		harness.Engage(caller, raider);

		Assert.Equal(1, answerer.GetAggroList().GetHate(raider));
		Assert.Same(raider, answerer.GetTarget());
	}

	/// <summary>
	/// <b>A guard already fighting takes a hundred instead of one.</b> Idle, a single point is enough
	/// because there is nothing else on its list; busy, retail wants a real claim.
	/// </summary>
	[Fact]
	public void AGuardAlreadyFightingTakesAHundred()
	{
		var (harness, caller, answerer, raider) =
			Post(ArchonPatrol, ArchonVeteranPatrol, Race.ELYOS);
		using BossAiHarness _h = harness;

		Player other = harness.SpawnPlayer(312f, 300f, 200f, race: Race.ELYOS);
		harness.Engage(answerer, other);

		harness.Engage(caller, raider);

		Assert.Equal(100, answerer.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>It checks the player named, not who spoke — which is what lets one message number carry both
	/// factions.</b> An Asmodian raider pulls an Asmodian-side guard, and the answerer standing beside
	/// it does nothing, because the player named is not its enemy.
	/// </summary>
	/// <remarks>
	/// Retail's guard is <c>is_enemy who=OBJI_MESSAGE_PARAM</c>. Written the obvious way — check the
	/// sender is a friend — the family would have needed a number per faction, and it uses one.
	/// <para>
	/// <b>The target, not the hate, is what this pin measures, and the reason is worth keeping.</b>
	/// A mutation deleting the guard from the idle branch changed no hate at all: this port's
	/// <c>AggroList.AddHate</c> already drops hate aimed at a creature that is not an enemy, so retail's
	/// condition is enforced a second time one layer down. It was measured rather than assumed — a probe
	/// adding fifty points to a friendly and a hostile player read back zero and fifty.
	/// </para>
	/// <para>
	/// <b>But the turn is not protected.</b> <c>HateMessageTarget</c> faces its target whether or not
	/// the hate landed, so a guard with the condition deleted swings round to a friendly player and
	/// stands there. That is the observable difference, and it is what fails when the guard goes.
	/// </para>
	/// </remarks>
	[Fact]
	public void ItChecksThePlayerNamedRatherThanWhoSpoke()
	{
		var (harness, caller, answerer, friendly) =
			Post(ArchonPatrol, ArchonVeteranPatrol, Race.ASMODIANS);
		using BossAiHarness _h = harness;

		harness.Engage(caller, friendly);

		Assert.Equal(0, answerer.GetAggroList().GetHate(friendly));
		Assert.NotSame(friendly, answerer.GetTarget());
	}

	/// <summary>
	/// <b>And only within twenty-five metres</b>, which is retail's range — so a call brings its own
	/// post and not the whole fortress.
	/// </summary>
	[Fact]
	public void AndOnlyWithinTwentyFiveMetres()
	{
		var (harness, caller, answerer, raider) =
			Post(ArchonPatrol, ArchonVeteranPatrol, Race.ELYOS);
		using BossAiHarness _h = harness;

		Npc distant = harness.Spawn(ArchonVeteranPatrol, 340f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(caller, distant);

		harness.Engage(caller, raider);

		Assert.Equal(1, answerer.GetAggroList().GetHate(raider));
		Assert.Equal(0, distant.GetAggroList().GetHate(raider));
	}

	/// <summary><b>The message number and the range are retail's, not ours.</b></summary>
	[Fact]
	public void TheNumbersAreRetails()
	{
		Assert.Equal(23200, FortressGuardCallAI.ThisOne);
		Assert.Equal(25f, FortressGuardCallAI.CallReach);
	}

	// The Light-side twins, on 23100. Elyos guards, so an Asmodian raider is their enemy.
	private const int GarrisonWatchguard = 234081;
	private const int GuardianVeteranPatrol = 209669;

	private static BossAiHarness LightHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(2048)
			.WithAi(typeof(GarrisonGuardCallAI), typeof(GarrisonGuardAnswerAI),
				typeof(FortressGuardCallAI), typeof(FortressGuardAnswerAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>The Light-side guards do the same thing on their own number.</b> Same range, same one point
	/// idle and hundred while fighting, same <c>is_enemy</c> on the player named.
	/// </summary>
	[Fact]
	public void TheLightSideGuardsCallTheSameWay()
	{
		using BossAiHarness harness = LightHarness();
		Npc caller = harness.Spawn(GarrisonWatchguard, 300f, 300f, 200f);
		Npc answerer = harness.Spawn(GuardianVeteranPatrol, 310f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(caller, answerer);

		harness.Engage(caller, raider);

		Assert.Equal(1, answerer.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And the two families do not hear each other, which is what the second number is for.</b> A
	/// Dark-side caller pulling a player its Light-side neighbour also counts as an enemy leaves that
	/// neighbour standing — because it is listening on <c>23100</c> and the call went out on
	/// <c>23200</c>.
	/// </summary>
	/// <remarks>
	/// This is why these are two classes rather than one listening to both numbers. The
	/// <c>is_enemy</c> guard would not have separated them: both families' Elyos-side guards have the
	/// same enemies, so a merged class would have had them answering each other's calls.
	/// </remarks>
	[Fact]
	public void AndTheTwoFamiliesDoNotHearEachOther()
	{
		using BossAiHarness harness = LightHarness();
		Npc darkCaller = harness.Spawn(ArchonPatrol, 300f, 300f, 200f);
		Npc lightAnswerer = harness.Spawn(GuardianVeteranPatrol, 310f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(darkCaller, lightAnswerer);

		harness.Engage(darkCaller, raider);

		Assert.Equal(0, lightAnswerer.GetAggroList().GetHate(raider));
	}

	/// <summary><b>And the Light-side number is retail's too.</b></summary>
	[Fact]
	public void TheLightSideNumberIsRetails()
	{
		Assert.Equal(23100, GarrisonGuardCallAI.ThisOne);
		Assert.NotEqual(FortressGuardCallAI.ThisOne, GarrisonGuardCallAI.ThisOne);
	}
}
