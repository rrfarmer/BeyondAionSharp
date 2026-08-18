using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the pet drakes and everyone who calls them, translated from <c>Lizardman_BeastA</c>,
/// <c>Lizardman_FeA</c> and the forty <c>*_Reward*</c> patterns (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// <b>The bakarma lookouts are the first encounter built on <c>FriendCombatNotice</c></b>, so these
/// pins are also the pins for retail's <c>on_see_friend_attacked</c> and <c>on_friend_spelled</c>.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PetDrakeCallAiTests
{
	private const int Beluslan = 220040000;

	private const int BakarmaLookout = 213299;   // Lizardman_FeA -- watches its friends
	private const int PetDrake = 213308;         // Lizardman_BeastA -- the only listener
	private const int RanxMasterAtArms = 215102; // an ABRwd officer, calls at 30m

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Beluslan).WithWorldSize(2048)
			.WithAi(typeof(LizardmanWatchAI), typeof(PetDrakeAI), typeof(RewardGuardCallAI),
				typeof(RewardGuardWideCallAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>A lookout watching another lookout, with a drake in earshot and a raider doing it.</summary>
	private static (BossAiHarness, Npc, Npc, Npc, Player) Camp(float drakeAway = 8f)
	{
		BossAiHarness harness = NewHarness();
		Npc watcher = harness.Spawn(BakarmaLookout, 300f, 300f, 200f);
		Npc victim = harness.Spawn(BakarmaLookout, 302f, 300f, 200f);
		Npc drake = harness.Spawn(PetDrake, 300f + drakeAway, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(watcher, victim);
		BossAiHarness.MakeMutuallyKnown(watcher, drake);
		return (harness, watcher, victim, drake, raider);
	}

	/// <summary>
	/// <b>Beat one lookout below three-quarters in front of another, and the drakes come for you.</b>
	/// The lookout that calls is not the one being hit — that is the whole of retail's
	/// <c>on_see_friend_attacked</c>, and this port had no event for it until now.
	/// </summary>
	[Fact]
	public void BeatOneLookoutAndItsWatcherSetsTheDrakesOnYou()
	{
		var (harness, watcher, victim, drake, raider) = Camp();
		using BossAiHarness _h = harness;

		Assert.Equal(0, drake.GetAggroList().GetHate(raider));

		BossAiHarness.SetExactPercent(victim, 60);
		Aion.GameServer.Ai.FriendCombatNotice.Raise(victim, raider, spelled: false);

		Assert.Equal(101, drake.GetAggroList().GetHate(raider));
		Assert.Same(raider, drake.GetTarget());
	}

	/// <summary>
	/// <b>A friend still above three-quarters is not worth calling about.</b> Retail's guard is on the
	/// <em>friend's</em> health, not the watcher's, which is the condition
	/// <c>When.FriendHpBelow</c> exists for.
	/// </summary>
	[Fact]
	public void AFriendAboveThreeQuartersIsNotWorthCallingAbout()
	{
		var (harness, watcher, victim, drake, raider) = Camp();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(victim, 90);
		Aion.GameServer.Ai.FriendCombatNotice.Raise(victim, raider, spelled: false);

		Assert.Equal(0, drake.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A spell calls them too, and the flag is shared</b>, so a lookout calls once for one friend's
	/// beating however it is delivered.
	/// </summary>
	[Fact]
	public void ASpellCallsThemTooAndTheFlagIsShared()
	{
		var (harness, watcher, victim, drake, raider) = Camp();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(victim, 60);
		Aion.GameServer.Ai.FriendCombatNotice.Raise(victim, raider, spelled: true);
		Assert.Equal(101, drake.GetAggroList().GetHate(raider));

		Aion.GameServer.Ai.FriendCombatNotice.Raise(victim, raider, spelled: false);

		Assert.Equal(101, drake.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The watcher has to be able to see it</b> — eight metres for a bakarma lookout, which is its
	/// own <c>srange</c>. The notice uses each observer's sight range rather than one radius chosen for
	/// all of them, the same rule the death notice uses; sharing that decision is worth more than the
	/// decision.
	/// </summary>
	/// <remarks>
	/// <b>Twelve metres, not eighty.</b> The first version put the victim eighty metres off, which is
	/// outside the known list entirely — so the notice never reached the loop and the pin passed with
	/// the range check deleted. Twelve is inside the known list and outside a lookout's eyes, which is
	/// the only gap that measures anything.
	/// </remarks>
	[Fact]
	public void TheWatcherHasToBeAbleToSeeIt()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc watcher = harness.Spawn(BakarmaLookout, 300f, 300f, 200f);
		Npc victim = harness.Spawn(BakarmaLookout, 312f, 300f, 200f);
		Npc drake = harness.Spawn(PetDrake, 305f, 300f, 200f);
		Player raider = harness.SpawnPlayer(313f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(watcher, victim);
		BossAiHarness.MakeMutuallyKnown(watcher, drake);

		BossAiHarness.SetExactPercent(victim, 60);
		Aion.GameServer.Ai.FriendCombatNotice.Raise(victim, raider, spelled: false);

		Assert.Equal(0, drake.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And it has to be a friend.</b> A lookout watching a pet drake take a beating says nothing —
	/// <c>NLIZARDMAN</c> and <c>NLIZARDPET</c> are related by <c>support</c>, not <c>friend</c>, which
	/// is the same distinction the taygas exposed on the death notice.
	/// </summary>
	/// <remarks>
	/// The two notices share <c>TribeRelationService.IsFriend</c> deliberately, so this pin is also the
	/// pin that keeps them sharing it. If the tribe question recorded against the death notice is ever
	/// decided the other way, this is the pin that will say so.
	/// </remarks>
	[Fact]
	public void AndItHasToBeAFriend()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc watcher = harness.Spawn(BakarmaLookout, 300f, 300f, 200f);
		Npc notAFriend = harness.Spawn(PetDrake, 302f, 300f, 200f);
		Npc drake = harness.Spawn(PetDrake, 305f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(watcher, notAFriend);
		BossAiHarness.MakeMutuallyKnown(watcher, drake);

		BossAiHarness.SetExactPercent(notAFriend, 60);
		Aion.GameServer.Ai.FriendCombatNotice.Raise(notAFriend, raider, spelled: false);

		Assert.Equal(0, drake.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And the call itself reaches thirteen metres</b>, which is retail's — so a lookout's drakes
	/// come and the next camp's do not.
	/// </summary>
	[Fact]
	public void AndTheCallReachesThirteenMetres()
	{
		var (harness, watcher, victim, near, raider) = Camp();
		using BossAiHarness _h = harness;

		Npc distant = harness.Spawn(PetDrake, 330f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(watcher, distant);

		BossAiHarness.SetExactPercent(victim, 60);
		Aion.GameServer.Ai.FriendCombatNotice.Raise(victim, raider, spelled: false);

		Assert.Equal(101, near.GetAggroList().GetHate(raider));
		Assert.Equal(0, distant.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The reward camps' officers do it the plain way</b> — pulled, and the drakes are sent, from
	/// thirty metres. Thirty-three retail patterns carry that one action and nothing else.
	/// </summary>
	[Fact]
	public void TheRewardOfficersCallOnBeingPulled()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc officer = harness.Spawn(RanxMasterAtArms, 300f, 300f, 200f);
		Npc drake = harness.Spawn(PetDrake, 320f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(officer, drake);

		harness.Engage(officer, raider);

		Assert.Equal(101, drake.GetAggroList().GetHate(raider));
	}

	/// <summary><b>The message number and both ranges are retail's, not ours.</b></summary>
	[Fact]
	public void TheNumbersAreRetails()
	{
		Assert.Equal(3201, PetDrakeAI.GetThatOne);
		Assert.Equal(13f, LizardmanWatchAI.WatchReach);
		Assert.Equal(30f, RewardGuardCallAI.CallReach);
		Assert.Equal(50f, RewardGuardWideCallAI.CallReach);
	}
}
