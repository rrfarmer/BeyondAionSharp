using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins the four answers that used to be one branch each: <b>an npc already fighting answers a call
/// differently from one standing idle</b>, and retail writes a separate branch for each state.
/// </summary>
/// <remarks>
/// These live together rather than in each encounter's own file because they all pin the same claim, and
/// because the claim went in unpinned. Three classes were unfolded across two commits — the Anuhart pet,
/// its subordinates, the Vritra rearguard — and the suite stayed green throughout, because the idle path
/// is what the existing pins exercise and the idle path still worked. **A green suite was not evidence.**
/// <para>
/// Every pin here gives the answerer a fight of its own first, which is the only arrangement in which the
/// two branches are distinguishable. Same shape as
/// <c>PanesterraGuardAiTests.ACaptainIsObeyedAndAGuardIsOnlyNoted</c>.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class StateFoldedAnswersTests
{
	private const int Map = 300100000;

	private const int AnuhartPet = 215250;          // XD_EPet
	private const int AnuhartSubordinate = 281249;  // LastBoss_Su
	private const int VritraRearguard = 233477;     // IDF5_U1_War_Vri_Def01_Ra_SN_65_Ae

	private static BossAiHarness Harness() =>
		BossAiHarness.For(Map).WithWorldSize(2048)
			.WithAi(typeof(AnuhartPetAI), typeof(AnuhartSubordinateAI), typeof(VritraRearguardAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>The answerer, somebody it is already fighting, and the one a call will name.</summary>
	private static (BossAiHarness, Npc, Player busy, Player named) Busy(int npcId, string aiName)
	{
		BossAiHarness harness = Harness();
		Npc answerer = harness.SpawnWithAi(npcId, aiName, 300f, 300f, 200f);
		Player busy = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		Player named = harness.SpawnPlayer(304f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(answerer, busy);
		BossAiHarness.MakeMutuallyKnown(answerer, named);
		harness.Engage(answerer, busy);
		return (harness, answerer, busy, named);
	}

	private static void Send(Npc answerer, int message, Creature param)
		=> ((Aion.GameServer.Ai.INpcMessageListener)answerer.GetAi())
			.OnNpcMessage(answerer, message, param);

	/// <summary>
	/// <b>An idle pet joins; a fighting pet is yanked.</b> Retail's <c>XD_EPet</c> answers <c>3406</c>
	/// from two branches — <c>attack_most_hating</c> when idle, a forced <c>switch_target</c> when
	/// already in a fight — and both spend a hundred points.
	/// </summary>
	[Fact]
	public void AFightingPetIsYankedOffItsTarget()
	{
		var (harness, pet, busy, named) = Busy(AnuhartPet, "anuhart_pet");
		using BossAiHarness _h = harness;

		Assert.Same(busy, pet.GetTarget());

		Send(pet, AnuhartPetAI.GoForThisOne, named);

		Assert.Same(named, pet.GetTarget());
	}

	/// <summary>
	/// <b>And an idle pet keeps no-one else's fight.</b> The idle branch adds hate and picks its most
	/// hated, which on an empty list is the one just named — so the observable is the same target by a
	/// different route, and this pin exists to show the idle branch is still there after the split.
	/// </summary>
	[Fact]
	public void AnIdlePetGoesWhereItIsPointed()
	{
		using BossAiHarness harness = Harness();
		Npc pet = harness.SpawnWithAi(AnuhartPet, "anuhart_pet", 300f, 300f, 200f);
		Player named = harness.SpawnPlayer(304f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(pet, named);

		Assert.Null(pet.GetTarget());

		Send(pet, AnuhartPetAI.GoForThisOne, named);

		Assert.Same(named, pet.GetTarget());
	}

	/// <summary>
	/// <b>A subordinate mid-fight is yanked, on either order.</b> <c>LastBoss_Su</c> writes both
	/// <c>6833</c> and <c>6834</c> twice, once per state, and the fighting pair spends a hundred against
	/// the idle pair's single point.
	/// </summary>
	[Theory]
	[InlineData(AnuhartSubordinateAI.TakeThisOne)]
	[InlineData(AnuhartSubordinateAI.GoForThisOne)]
	public void AFightingSubordinateIsYankedOnEitherOrder(int order)
	{
		var (harness, sub, busy, named) = Busy(AnuhartSubordinate, "anuhart_subordinate");
		using BossAiHarness _h = harness;

		Assert.Same(busy, sub.GetTarget());

		Send(sub, order, named);

		Assert.Same(named, sub.GetTarget());
	}

	/// <summary>
	/// <b>A rearguard already fighting only notes the call.</b> Retail puts the state guard on one branch
	/// and leaves the fallback unguarded, so a busy rearguard matches the guarded branch, takes the hate
	/// and <b>keeps its own target</b> — the one answer in this file that does not move.
	/// </summary>
	/// <remarks>
	/// This is the pin that would have caught a tidy-up: writing the pair as two symmetric state guards
	/// gives the same idle behaviour and the wrong busy behaviour.
	/// </remarks>
	[Fact]
	public void AFightingRearguardNotesTheCallAndKeepsItsTarget()
	{
		var (harness, rearguard, busy, named) = Busy(VritraRearguard, "vritra_rearguard");
		using BossAiHarness _h = harness;

		Assert.Same(busy, rearguard.GetTarget());

		Send(rearguard, VritraRearguardAI.Target, named);

		Assert.True(rearguard.GetAggroList().GetHate(named) > 0, "the call never landed");
		Assert.Same(busy, rearguard.GetTarget());
	}

	/// <summary>
	/// <b>And an idle rearguard joins.</b> It falls past the guarded branch to the unguarded fallback,
	/// which adds the same hundred and then attacks its most hated.
	/// </summary>
	[Fact]
	public void AnIdleRearguardJoins()
	{
		using BossAiHarness harness = Harness();
		Npc rearguard = harness.SpawnWithAi(VritraRearguard, "vritra_rearguard", 300f, 300f, 200f);
		Player named = harness.SpawnPlayer(304f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(rearguard, named);

		Assert.Null(rearguard.GetTarget());

		Send(rearguard, VritraRearguardAI.Target, named);

		Assert.Same(named, rearguard.GetTarget());
	}
}
