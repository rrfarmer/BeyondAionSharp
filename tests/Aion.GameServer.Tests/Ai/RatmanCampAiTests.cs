using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the ratman camps and the lycans they call, translated from retail patterns
/// <c>Ratman_FnR</c>, <c>Ratman_FnR_LWaSu*</c>, <c>Lycan_KnA</c>, <c>NRatman_FnA</c>,
/// <c>NRatman_RnA</c>, <c>NRatman_FnC</c>, <c>NRatman_RnC</c> and <c>NLycan_KeA</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class RatmanCampAiTests
{
	private const int Altgard = 220030000;
	private const int Beluslan = 220040000;

	private const int DundunFarmer = 210391;   // calls 1007 on every blow
	private const int GrayManeStalker = 210395;// answers with 101
	private const int MunmunWarrior = 211414;  // calls 8001 when pulled
	private const int NunuFarmer = 211582;     // calls 8001 below a third, and for a dead friend
	private const int MunmunPatrol = 212668;   // calls when pulled and again below a third
	// 212670, not the 204400 that heads the pattern: that one is tribe GENERAL_DARK, an Asmodian-side
	// npc whose aggro list refuses hate aimed at a player it is not hostile to, so every answer reads
	// zero however correct the branch is. Fourth time this rule has decided a pin -- see the kerubiel
	// garks, the fortress guards and the Panesterra slayers.
	private const int Kuriuta = 212670;        // answers with 200

	private static BossAiHarness Harness(int map) =>
		BossAiHarness.For(map).WithWorldSize(2048)
			.WithAi(typeof(RatmanFarmerAI), typeof(GrayManeStalkerAI), typeof(MunmunWarriorAI),
				typeof(NunuFarmerAI), typeof(MunmunPatrolAI), typeof(KuriutaAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>The farmer is not the fight — the lycan that owns it is.</b> A dundun beaten below forty-five
	/// names its attacker, and a gray mane stalker commits a hundred and one.
	/// </summary>
	[Fact]
	public void AFarmerUnderAttackCallsItsOwner()
	{
		using BossAiHarness harness = Harness(Altgard);
		Npc farmer = harness.SpawnWithAi(DundunFarmer, "ratman_farmer", 300f, 300f, 200f);
		Npc stalker = harness.SpawnWithAi(GrayManeStalker, "gray_mane_stalker", 306f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(farmer, stalker);

		Assert.Equal(0, stalker.GetAggroList().GetHate(raider));

		// Retail gates the farmers' 1007 call on is_hp_in_boundary less_than 45, so a farmer at full
		// health never calls. Thirty puts it inside the band this file is about.
		BossAiHarness.SetExactPercent(farmer, 30);
		harness.Engage(farmer, raider);

		// A multiple of 101: this farmer calls on every blow and Engage lands one of its own.
		int hate = stalker.GetAggroList().GetHate(raider);
		Assert.True(hate >= 101 && hate % 101 == 0, $"expected a multiple of 101, got {hate}");
	}

	/// <summary>
	/// <b>And below forty-five it calls on every blow.</b> Retail puts no flag on either branch, so a
	/// farmer inside its band keeps naming its attacker for as long as the beating lasts.
	/// </summary>
	[Fact]
	public void AndItCallsOnEveryBlow()
	{
		using BossAiHarness harness = Harness(Altgard);
		Npc farmer = harness.SpawnWithAi(DundunFarmer, "ratman_farmer", 300f, 300f, 200f);
		Npc stalker = harness.SpawnWithAi(GrayManeStalker, "gray_mane_stalker", 306f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(farmer, stalker);

		// Retail gates the farmers' 1007 call on is_hp_in_boundary less_than 45, so a farmer at full
		// health never calls. Thirty puts it inside the band this file is about.
		BossAiHarness.SetExactPercent(farmer, 30);
		harness.Engage(farmer, raider);
		int afterOne = stalker.GetAggroList().GetHate(raider);

		farmer.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		farmer.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(afterOne + 202, stalker.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>Beluslan answers twice as hard.</b> A kuriuta commits two hundred where a gray mane stalker
	/// commits a hundred and one — the same arrangement one zone north, at double the price.
	/// </summary>
	[Fact]
	public void BeluslanAnswersTwiceAsHard()
	{
		using BossAiHarness harness = Harness(Beluslan);
		Npc warrior = harness.SpawnWithAi(MunmunWarrior, "munmun_warrior", 300f, 300f, 200f);
		Npc kuriuta = harness.SpawnWithAi(Kuriuta, "kuriuta", 306f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(warrior, kuriuta);

		harness.Engage(warrior, raider);

		Assert.Equal(200, kuriuta.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The warriors call on being pulled and the nunu on being nearly dead.</b> The warriors announce
	/// a fight; the farmers complain about one.
	/// </summary>
	[Fact]
	public void TheWarriorsAnnounceAndTheFarmersComplain()
	{
		using BossAiHarness harness = Harness(Beluslan);
		Npc nunu = harness.SpawnWithAi(NunuFarmer, "nunu_farmer", 300f, 300f, 200f);
		Npc kuriuta = harness.SpawnWithAi(Kuriuta, "kuriuta", 306f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(nunu, kuriuta);

		BossAiHarness.SetExactPercent(nunu, 60);
		harness.Engage(nunu, raider);
		Assert.Equal(0, kuriuta.GetAggroList().GetHate(raider));

		BossAiHarness.SetExactPercent(nunu, 25);
		nunu.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(200, kuriuta.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And a nunu calls again for a friend's killer</b> — a separate flag, so a nunu beaten low that
	/// then watches a neighbour fall calls twice and never a third time.
	/// </summary>
	[Fact]
	public void AndANunuCallsAgainForAFriendsKiller()
	{
		using BossAiHarness harness = Harness(Beluslan);
		Npc nunu = harness.SpawnWithAi(NunuFarmer, "nunu_farmer", 300f, 300f, 200f);
		Npc doomed = harness.SpawnWithAi(NunuFarmer, "nunu_farmer", 302f, 300f, 200f);
		Npc kuriuta = harness.SpawnWithAi(Kuriuta, "kuriuta", 306f, 300f, 200f);
		Player killer = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(nunu, kuriuta);
		BossAiHarness.MakeMutuallyKnown(nunu, doomed);
		BossAiHarness.MakeMutuallyKnown(kuriuta, killer);

		Assert.Equal(0, kuriuta.GetAggroList().GetHate(killer));

		Aion.GameServer.Ai.FriendDeathNotice.Raise(doomed, killer);

		// The nunu named the killer, not its own attacker -- it has none.
		Assert.Equal(200, kuriuta.GetAggroList().GetHate(killer));
	}

	/// <summary>
	/// <b>And the two calls have two flags, so one nunu can make both.</b> Beaten below a third it
	/// calls; watching a neighbour fall afterwards it calls again. One shared flag would spend the
	/// second on the first.
	/// </summary>
	/// <remarks>
	/// It takes one nunu doing both things in one fight to see this. The pins above use two nunu, one
	/// for each call, and a shared flag survives them untouched — the same symmetry the Tiamat
	/// insurgents' pair of "once" claims had.
	/// </remarks>
	[Fact]
	public void AndTheTwoCallsHaveTwoFlags()
	{
		using BossAiHarness harness = Harness(Beluslan);
		Npc nunu = harness.SpawnWithAi(NunuFarmer, "nunu_farmer", 300f, 300f, 200f);
		Npc doomed = harness.SpawnWithAi(NunuFarmer, "nunu_farmer", 302f, 300f, 200f);
		Npc kuriuta = harness.SpawnWithAi(Kuriuta, "kuriuta", 306f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(nunu, kuriuta);
		BossAiHarness.MakeMutuallyKnown(nunu, doomed);
		BossAiHarness.MakeMutuallyKnown(kuriuta, raider);

		BossAiHarness.SetExactPercent(nunu, 25);
		harness.Engage(nunu, raider);
		int afterHurtCall = kuriuta.GetAggroList().GetHate(raider);
		Assert.True(afterHurtCall >= 200, "the hurt call never landed");

		Aion.GameServer.Ai.FriendDeathNotice.Raise(doomed, raider);

		Assert.InRange(kuriuta.GetAggroList().GetHate(raider),
			afterHurtCall + 200, afterHurtCall + 299);
	}

	/// <summary>
	/// <b>The patrol calls twice for itself</b>: once on being pulled and once below a third, which no
	/// other ratman in either camp does.
	/// </summary>
	[Fact]
	public void ThePatrolCallsTwiceForItself()
	{
		using BossAiHarness harness = Harness(Beluslan);
		Npc patrol = harness.SpawnWithAi(MunmunPatrol, "munmun_patrol", 300f, 300f, 200f);
		Npc kuriuta = harness.SpawnWithAi(Kuriuta, "kuriuta", 306f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(patrol, kuriuta);

		BossAiHarness.SetExactPercent(patrol, 25);
		harness.Engage(patrol, raider);
		int afterPull = kuriuta.GetAggroList().GetHate(raider);
		Assert.Equal(200, afterPull);

		harness.Watch(10, null);

		Assert.InRange(kuriuta.GetAggroList().GetHate(raider), afterPull + 200, afterPull + 299);
	}

	/// <summary>
	/// <b>Twelve metres in Altgard and fifteen in Beluslan</b>, which is retail's.
	/// </summary>
	[Fact]
	public void TheTwoCampsHaveDifferentReach()
	{
		using BossAiHarness harness = Harness(Altgard);
		Npc farmer = harness.SpawnWithAi(DundunFarmer, "ratman_farmer", 300f, 300f, 200f);
		Npc near = harness.SpawnWithAi(GrayManeStalker, "gray_mane_stalker", 306f, 300f, 200f);
		Npc far = harness.SpawnWithAi(GrayManeStalker, "gray_mane_stalker", 316f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(farmer, near);
		BossAiHarness.MakeMutuallyKnown(farmer, far);

		// Retail gates the farmers' 1007 call on is_hp_in_boundary less_than 45, so a farmer at full
		// health never calls. Thirty puts it inside the band this file is about.
		BossAiHarness.SetExactPercent(farmer, 30);
		harness.Engage(farmer, raider);

		Assert.True(near.GetAggroList().GetHate(raider) >= 101);
		Assert.Equal(0, far.GetAggroList().GetHate(raider));
	}

	/// <summary><b>The numbers, ranges and payloads are retail's, not ours.</b></summary>
	[Fact]
	public void TheNumbersAreRetails()
	{
		Assert.Equal(1007, RatmanCalls.Farmers);
		Assert.Equal(8001, RatmanCalls.Camp);
		Assert.Equal(12f, RatmanCalls.FarmerReach);
		Assert.Equal(15f, RatmanCalls.CampReach);
	}
}
