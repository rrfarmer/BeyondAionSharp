using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the Tiamat Remnant insurgents, translated from retail patterns
/// <c>TR_Drakan_As_Broad_First_solo</c> and <c>TR_Lizard_Basic_First</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class TiamatInsurgentAiTests
{
	private const int TiamatStronghold = 600100000;

	private const int InsurgentScout = 230888;
	private const int InsurgentInfantry = 230880;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(InsurgentScoutAI), typeof(InsurgentInfantryAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, Npc, Player) Camp(float apart = 8f)
	{
		BossAiHarness harness = NewHarness();
		Npc scout = harness.Spawn(InsurgentScout, 300f, 300f, 200f);
		Npc infantry = harness.Spawn(InsurgentInfantry, 300f + apart, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(scout, infantry);
		harness.Engage(scout, raider);
		return (harness, scout, infantry, raider);
	}

	/// <summary>
	/// <b>Twelve seconds into a fight the scout names its target, and the infantry commit three
	/// hundred</b> — the largest answer to a field call anywhere in this log.
	/// </summary>
	/// <remarks>
	/// Twelve is not a number retail writes down. Engaging arms a five-second timer, that arms a
	/// seven-second one, and the seven-second one carries the call.
	/// </remarks>
	[Fact]
	public void TwelveSecondsInTheScoutCallsAndTheInfantryCommit()
	{
		var (harness, scout, infantry, raider) = Camp();
		using BossAiHarness _h = harness;

		harness.Watch(10, null);
		int beforeTheCall = infantry.GetAggroList().GetHate(raider);
		Assert.True(beforeTheCall < 300, "the scout called inside ten seconds");

		harness.Watch(10, null);

		Assert.Equal(beforeTheCall + 300, infantry.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>Each side does it once, and these are two different claims.</b> The scout's call timer is
	/// never re-armed, and retail flags the infantry's answer.
	/// </summary>
	/// <remarks>
	/// <b>One pin could not tell them apart, and the mutation sweep is what showed it.</b> Re-arming
	/// the scout's call timer changed nothing, because the infantry's flag refused the second call;
	/// deleting the infantry's flag changed nothing, because the scout never made a second call. <b>Each
	/// mutation was hidden by the other's mechanism.</b> The two pins below break the symmetry: one
	/// gives an infantryman two callers, the other gives a second call a listener that has not spent
	/// its flag.
	/// </remarks>
	[Fact]
	public void AndEachSideDoesItOnce()
	{
		var (harness, scout, infantry, raider) = Camp();
		using BossAiHarness _h = harness;

		harness.Watch(20, null);
		int afterTheCall = infantry.GetAggroList().GetHate(raider);
		Assert.True(afterTheCall >= 300);

		// Long enough for the rotation timers to come round several times over.
		harness.Watch(120, null);

		Assert.Equal(afterTheCall, infantry.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>An infantryman answers the first call it hears and no other</b> — retail's flag, measured
	/// against two scouts rather than one, which is the only way it is visible.
	/// </summary>
	[Fact]
	public void AnInfantrymanAnswersOnlyItsFirstCall()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc first = harness.Spawn(InsurgentScout, 300f, 300f, 200f);
		Npc second = harness.Spawn(InsurgentScout, 302f, 300f, 200f);
		Npc infantry = harness.Spawn(InsurgentInfantry, 306f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(first, infantry);
		BossAiHarness.MakeMutuallyKnown(second, infantry);
		harness.Engage(first, raider);
		harness.Engage(second, raider);

		harness.Watch(25, null);

		// Two scouts called; three hundred landed once, not six hundred.
		Assert.InRange(infantry.GetAggroList().GetHate(raider), 300, 599);
	}

	/// <summary>
	/// <b>And the scout calls once</b> — measured with an infantryman that arrives after the first call
	/// and therefore still has its flag. If the call timer re-armed, this one would be commanded too.
	/// </summary>
	[Fact]
	public void AndTheScoutCallsOnlyOnce()
	{
		var (harness, scout, infantry, raider) = Camp();
		using BossAiHarness _h = harness;

		harness.Watch(20, null);
		Assert.True(infantry.GetAggroList().GetHate(raider) >= 300, "the first call never landed");

		Npc late = harness.Spawn(InsurgentInfantry, 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(scout, late);

		harness.Watch(120, null);

		// Under 300, not zero: a fight running beside a friendly npc puts a support-aggro point on the
		// attacker, as the tursin bigmouth's pin records. Three hundred is what a call is worth.
		Assert.True(late.GetAggroList().GetHate(raider) < 300,
			"a second call reached an infantryman that arrived after the first");
	}

	/// <summary>
	/// <b>And only within twenty metres</b>, which is retail's.
	/// </summary>
	[Fact]
	public void AndOnlyWithinTwentyMetres()
	{
		var (harness, scout, near, raider) = Camp();
		using BossAiHarness _h = harness;

		Npc distant = harness.Spawn(InsurgentInfantry, 330f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(scout, distant);

		harness.Watch(20, null);

		Assert.True(near.GetAggroList().GetHate(raider) >= 300);
		Assert.Equal(0, distant.GetAggroList().GetHate(raider));
	}

	/// <summary><b>The message number and the range are retail's, not ours.</b></summary>
	[Fact]
	public void TheNumbersAreRetails()
	{
		Assert.Equal(22001, InsurgentScoutAI.GetHim);
		Assert.Equal(20f, InsurgentScoutAI.CallReach);
	}
}
