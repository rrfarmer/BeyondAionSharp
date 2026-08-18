using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the black claw lycans and their taygas, translated from retail patterns <c>Lycan_HeA</c>,
/// <c>Lycan_HnA</c>, <c>Lycan_HeB</c> and <c>D2_FnM</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class BlackClawLycanAiTests
{
	private const int Morheim = 220020000;

	private const int BrutalBreeder = 210542;   // Lycan_HeA
	private const int FeralHunter = 210463;     // Lycan_HnA
	private const int BrutalTamer = 210543;     // Lycan_HeB
	private const int JahamaTheRuthless = 210607;
	private const int TamedTayga = 210465;
	private const int FierceTayga = 210545;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Morheim).WithWorldSize(2048)
			.WithAi(typeof(BlackClawHunterAI), typeof(BlackClawTamerAI), typeof(TamedTaygaAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>A lycan, the tayga at its heel, and the player it is about to pick.</summary>
	private static (BossAiHarness, Npc, Npc, Player) Camp(
		int lycanId = BrutalBreeder, int taygaId = TamedTayga)
	{
		BossAiHarness harness = NewHarness();
		Npc lycan = harness.Spawn(lycanId, 300f, 300f, 200f);
		Npc tayga = harness.Spawn(taygaId, 304f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(lycan, tayga);
		return (harness, lycan, tayga, raider);
	}

	/// <summary>
	/// <b>When the lycan picks a fight it names the player, and its tayga comes.</b> The lycan is what a
	/// player pulls; the tayga is what arrives.
	/// </summary>
	[Theory]
	[InlineData(BrutalBreeder, TamedTayga)]
	[InlineData(FeralHunter, TamedTayga)]
	[InlineData(BrutalTamer, FierceTayga)]
	[InlineData(JahamaTheRuthless, FierceTayga)]
	public void TheLycanNamesItsTargetAndTheTaygaComes(int lycanId, int taygaId)
	{
		var (harness, lycan, tayga, raider) = Camp(lycanId, taygaId);
		using BossAiHarness _h = harness;

		Assert.Equal(0, tayga.GetAggroList().GetHate(raider));

		harness.Engage(lycan, raider);

		Assert.Equal(101, tayga.GetAggroList().GetHate(raider));
		Assert.Same(raider, tayga.GetTarget());
	}

	/// <summary>
	/// <b>A hundred and one, not a hundred.</b> Retail takes a single point and then switches with a
	/// hundred, in that order, and both land — so this is retail's number rather than a rounding of
	/// ours.
	/// </summary>
	[Fact]
	public void ItIsAHundredAndOneBecauseRetailWritesTwoActions()
	{
		var (harness, lycan, tayga, raider) = Camp();
		using BossAiHarness _h = harness;

		harness.Engage(lycan, raider);

		Assert.NotEqual(100, tayga.GetAggroList().GetHate(raider));
		Assert.Equal(101, tayga.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And only within fifteen metres</b>, which is retail's range — so a lycan calls its own tayga.
	/// </summary>
	[Fact]
	public void AndOnlyWithinFifteenMetres()
	{
		var (harness, lycan, tayga, raider) = Camp();
		using BossAiHarness _h = harness;

		Npc distant = harness.Spawn(TamedTayga, 330f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(lycan, distant);

		harness.Engage(lycan, raider);

		Assert.Equal(101, tayga.GetAggroList().GetHate(raider));
		Assert.Equal(0, distant.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>Kill one tayga in front of another and the survivor comes for whoever did it.</b> Retail's
	/// <c>on_sense_friend_killed_by_user</c>, naming <c>OBJI_KILLER</c> — the branch that made the
	/// friend-killed handler carry a killer at all.
	/// </summary>
	/// <remarks>
	/// <b>Two taygas rather than a tayga and its lycan, and the difference is not cosmetic.</b> The
	/// notice reaches a watcher only if <c>TribeRelationService.IsFriend</c> says so, and
	/// <c>LYCAN_PET</c> and <c>LYCAN_HUNTER</c> are related by <c>support</c>, not by <c>friend</c> — so
	/// a tayga does not hear its own tamer fall. Whether retail means the wider word is an open
	/// question; see docs/retail-ai-fidelity.md. Taygas share a tribe, so what is pinned here is the
	/// branch itself rather than a guess about the relation.
	/// <para>
	/// <b>The watcher is deliberately left out of the fight first.</b> An earlier version engaged the
	/// lycan, which put a hundred and one points on the raider through the <c>2301</c> call before the
	/// kill — so the killer was already the watcher's most-hated and the pin passed whether or not
	/// <c>OBJI_KILLER</c> reached it. Two mutations survived it.
	/// </para>
	/// <para>
	/// <b>An idle watcher pays the killer both payloads, and a busy one splits them.</b> Retail's point
	/// on the killer is what gives an idle tayga a target at all, so the <c>switch_target</c> that
	/// follows finds the killer already sitting there — a hundred and one. The next pin is the same
	/// branch on a tayga that was already fighting, where the two actions land on two players. That
	/// asymmetry is retail's, and it falls straight out of writing the two actions in retail's order.
	/// </para>
	/// </remarks>
	[Fact]
	public void KillOneTaygaAndTheOtherComesForTheKiller()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc doomed = harness.Spawn(TamedTayga, 300f, 300f, 200f);
		Npc watcher = harness.Spawn(FierceTayga, 302f, 300f, 200f);
		Player killer = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		Player bystander = harness.SpawnPlayer(304f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(doomed, watcher);

		Assert.Equal(0, watcher.GetAggroList().GetHate(killer));

		// The notice rather than the whole controller death path -- NpcController.OnDie reaches
		// SiegeService, which a harness has no world for. See the Bakarma pins for the same idiom.
		Aion.GameServer.Ai.FriendDeathNotice.Raise(doomed, killer);

		// A hundred and one, not one: see the remarks -- the point makes the killer this tayga's target,
		// and retail's second action then pays the target.
		Assert.Equal(101, watcher.GetAggroList().GetHate(killer));
		Assert.Equal(0, watcher.GetAggroList().GetHate(bystander));
	}

	/// <summary>
	/// <b>And the hundred goes to whoever it was already fighting, not to the killer.</b> Retail writes
	/// a point on <c>OBJI_KILLER</c> and then a hundred on <c>OBJI_CUR_TARGET</c>, which for a tayga
	/// mid-fight are two different players — so killing one tayga does not pull the next one off the
	/// person tanking it.
	/// </summary>
	/// <remarks>
	/// <b>This pin caught a real bug.</b> <c>HateFriendsKiller</c> was written to mirror
	/// <c>HateAttacker</c>, which turns to face its target — and turning first would have made the
	/// killer the current target, so retail's second action would have put its hundred on the killer
	/// too. Retail's action is a bare <c>add_hate_point</c>. It does not turn.
	/// </remarks>
	[Fact]
	public void AndTheHundredGoesToWhoeverItWasAlreadyFighting()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc doomed = harness.Spawn(TamedTayga, 300f, 300f, 200f);
		Npc watcher = harness.Spawn(FierceTayga, 302f, 300f, 200f);
		Player killer = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		Player tank = harness.SpawnPlayer(304f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(doomed, watcher);
		harness.Engage(watcher, tank);
		int held = watcher.GetAggroList().GetHate(tank);

		Aion.GameServer.Ai.FriendDeathNotice.Raise(doomed, killer);

		Assert.Equal(1, watcher.GetAggroList().GetHate(killer));
		Assert.Equal(held + 100, watcher.GetAggroList().GetHate(tank));
	}

	/// <summary>
	/// <b>A tamer whose tayga names its killer runs from that player.</b> Retail's <c>flee_from</c> with
	/// <c>from=OBJI_MESSAGE_PARAM</c> — not from whatever the tamer was fighting, from whoever did it.
	/// </summary>
	/// <remarks>
	/// <b>This pin used to be skipped as impossible.</b> The movement is indeed unobservable here, but
	/// <c>PatternAi.FleeingTo</c> records the destination the flee computed and is public — so the
	/// decision, and which player it was made about, were always in reach. See the klaw sentinels' pin.
	/// </remarks>
	[Fact]
	public void ATamerRunsFromWhoeverKilledItsTayga()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc tamer = harness.Spawn(BrutalTamer, 300f, 300f, 200f);
		Npc tayga = harness.Spawn(FierceTayga, 302f, 300f, 200f);
		Player killer = harness.SpawnPlayer(310f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(tamer, tayga);
		BossAiHarness.MakeMutuallyKnown(tamer, killer);

		Aion.GameServer.Ai.Pattern.PatternAi ai =
			Assert.IsAssignableFrom<Aion.GameServer.Ai.Pattern.PatternAi>(tamer.GetAi());
		Assert.Null(ai.FleeingTo);

		Aion.GameServer.Ai.NpcMessageBus.Broadcast(tayga, TamedTaygaAI.ItWasThem, killer, 20f);

		(float X, float Y)? destination = ai.FleeingTo;
		Assert.NotNull(destination);

		// The killer stands at 310 and the tamer at 300: away from it is the negative direction.
		Assert.True(destination.Value.X < 300f,
			"the tamer fled towards the killer rather than away from it");
	}

	/// <summary><b>The message numbers are retail's, not ours.</b></summary>
	[Fact]
	public void TheMessageNumbersAreRetails()
	{
		Assert.Equal(2301, BlackClawHunterAI.ThatOneIsMine);
		Assert.Equal(2307, TamedTaygaAI.ItWasThem);
		Assert.Equal(15f, BlackClawHunterAI.CallReach);
	}
}
