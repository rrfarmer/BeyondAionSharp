using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="MiddleBossFireAI"/>, translated from retail pattern
/// <c>BIDF5_U01_Middle_Boss_Fire</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Four bosses on one class, each with its own signature pair, so the tests run across all four
/// wherever the behaviour is shared. Hakara's missing second trait is pinned deliberately: it is a
/// known upstream data gap, and a test asserting it stays absent is what stops someone "fixing" it
/// with a guessed skill.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class MiddleBossFireAiTests
{
	private const int OphidanBridge = 300590000;
	private const int SwiftEdge = 17332;
	private const int FatalDisease = 21286;
	private const int BoostDeadlyVirulency = 17005;
	private const int MidnightRobe = 20700;

	public static TheoryData<int, int, int> Bosses => new()
	{
		{ 235772, 17900, 0 },      // hakara — no trait 2 in our data
		{ 235773, 18176, 20575 },  // zubala
		{ 235774, 20085, 21145 },  // visha
		{ 235775, 16923, 17250 },  // bahapa
	};

	private static (BossAiHarness, Npc, Player) Engaged(int npcId)
	{
		BossAiHarness harness = BossAiHarness.For(OphidanBridge).WithWorldSize(2048)
			.WithAi(typeof(MiddleBossFireAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static List<int> CastsOver(BossAiHarness harness, Npc boss, Player player, int seconds)
	{
		var cast = new List<int>();
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			cast.AddRange(BossAiHarness.DrainQueuedSkills(boss).Select(c => c.SkillId));
		}
		return cast;
	}

	[Theory]
	[MemberData(nameof(Bosses))]
	public void EachRobesItselfOnWaking(int npcId, int trait1, int trait2)
	{
		using BossAiHarness harness = BossAiHarness.For(OphidanBridge).WithWorldSize(2048)
			.WithAi(typeof(MiddleBossFireAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(npcId, 300f, 300f, 200f);

		BossAiHarness.QueuedCast robe = Assert.Single(BossAiHarness.DrainQueuedSkills(boss));
		Assert.Equal(MidnightRobe, robe.SkillId);
		Assert.Equal(NpcSkillTargetAttribute.ME, robe.Target);
	}

	[Theory]
	[MemberData(nameof(Bosses))]
	public void EachOpensTheTopBandWithItsFirstTrait(int npcId, int trait1, int trait2)
	{
		var (harness, boss, player) = Engaged(npcId);
		using (harness)
		{
			BossAiHarness.DrainQueuedSkills(boss);
			var cast = CastsOver(harness, boss, player, 8);

			Assert.Contains(trait1, cast);
			Assert.DoesNotContain(trait2 == 0 ? -1 : trait2, cast);
		}
	}

	[Theory]
	[MemberData(nameof(Bosses))]
	public void EachSlashesAfterItsTrait(int npcId, int trait1, int trait2)
	{
		var (harness, boss, player) = Engaged(npcId);
		using (harness)
		{
			BossAiHarness.DrainQueuedSkills(boss);

			// The trait lands at 5s and its slash six seconds later. Measuring only to 12s keeps this
			// on the chain's second step: the third step also slashes, so a longer window would pass
			// even with the second step's cast removed.
			Assert.Contains(SwiftEdge, CastsOver(harness, boss, player, 12));
		}
	}

	[Fact]
	public void KeepsItsChainAliveAtExactlyForty()
	{
		var (harness, boss, player) = Engaged(235773);
		using (harness)
		{
			// The bands are 71-100, 41-70 and below-40, so 40 itself matches none of them. Only the
			// catch-all keeps timer 0 armed through it; without one the fight would stop dead for any
			// group that parked him on exactly 40%.
			BossAiHarness.SetHpPercent(boss, 40);
			BossAiHarness.DrainQueuedSkills(boss);
			Assert.Empty(CastsOver(harness, boss, player, 20));

			BossAiHarness.SetHpPercent(boss, 30);
			Assert.NotEmpty(CastsOver(harness, boss, player, 20));
		}
	}

	[Fact]
	public void TheDiseasePairComesTogetherBelowSeventy()
	{
		var (harness, boss, player) = Engaged(235773);
		using (harness)
		{
			BossAiHarness.SetHpPercent(boss, 60);
			BossAiHarness.DrainQueuedSkills(boss);

			// One branch casts both, so neither appears without the other.
			var cast = CastsOver(harness, boss, player, 30);
			Assert.Contains(FatalDisease, cast);
			Assert.Contains(BoostDeadlyVirulency, cast);
		}
	}

	[Fact]
	public void ZubalaUsesItsSecondTraitBelowSeventyButHakaraHasNone()
	{
		var (harness, boss, player) = Engaged(235773);
		using (harness)
		{
			BossAiHarness.SetHpPercent(boss, 60);
			BossAiHarness.DrainQueuedSkills(boss);
			Assert.Contains(20575, CastsOver(harness, boss, player, 20));
		}

		var (h2, hakara, p2) = Engaged(235772);
		using (h2)
		{
			BossAiHarness.SetHpPercent(hakara, 60);
			BossAiHarness.DrainQueuedSkills(hakara);

			// His trait-2 branch casts nothing: the skill is missing from our data and from Java's, and
			// substituting a guess would be worse than a branch that does nothing. Pinned so it stays
			// a known gap rather than being quietly filled in.
			var cast = CastsOver(h2, hakara, p2, 20);
			Assert.DoesNotContain(17900, cast);
			Assert.Contains(FatalDisease, cast);
		}
	}

	private const int Zubala = 235773;
	private const int Mazikin = 235756;
	private const int Aethercaster = 235769;
	private const int SupportCombatant = 231185;

	/// <summary>Everything Ophidan Bridge's web needs registered, at one post.</summary>
	private static BossAiHarness Post(out Npc boss, out Player player)
	{
		BossAiHarness harness = BossAiHarness.For(OphidanBridge).WithWorldSize(2048)
			.WithAi(typeof(MiddleBossFireAI), typeof(OphidanBridgeCallAI), typeof(OphidanBridgeSweeperAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		boss = harness.Spawn(Zubala, 300f, 300f, 200f);
		player = harness.SpawnPlayer(302f, 302f, 200f);
		harness.Engage(boss, player);
		return harness;
	}

	private static int Standing(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>Killing a middle boss makes the fugitives around the post run.</b> Retail throws them clear
	/// with a <c>teleport_target</c> first, which we have no vocabulary for; the vanishing is the half
	/// we can say.
	/// </summary>
	[Fact]
	public void KillingAMiddleBossMakesTheFugitivesRun()
	{
		using BossAiHarness harness = Post(out Npc boss, out Player player);

		Npc fugitive = harness.Spawn(Mazikin, 310f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, fugitive);

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(0, Standing(harness, Mazikin));
	}

	/// <summary><b>A velkur does not run from it</b> — retail hangs the branch on the fugitives only.</summary>
	[Fact]
	public void AVelkurHoldsItsGround()
	{
		using BossAiHarness harness = Post(out Npc boss, out Player player);

		Npc velkur = harness.Spawn(Aethercaster, 310f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, velkur);

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(1, Standing(harness, Aethercaster));
	}

	/// <summary><b>And its support combatants are cleared with it.</b></summary>
	[Fact]
	public void KillingAMiddleBossClearsItsSupport()
	{
		using BossAiHarness harness = Post(out Npc boss, out Player player);

		harness.Spawn(SupportCombatant, 305f, 300f, 200f);
		harness.Spawn(SupportCombatant, 306f, 300f, 200f);
		Assert.Equal(2, Standing(harness, SupportCombatant));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(0, Standing(harness, SupportCombatant));
	}

	/// <summary>Walking away from the fight clears them too, without the signal going out.</summary>
	[Fact]
	public void LeavingTheFightClearsTheSupportButCallsNobody()
	{
		using BossAiHarness harness = Post(out Npc boss, out Player player);

		harness.Spawn(SupportCombatant, 305f, 300f, 200f);
		Npc fugitive = harness.Spawn(Mazikin, 310f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, fugitive);

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.BackHome);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(0, Standing(harness, SupportCombatant));
		Assert.Equal(1, Standing(harness, Mazikin));
	}

	/// <summary>
	/// <b>A middle boss answers the bridge's call.</b> It is in the same web as the velkurs and the
	/// fugitives, and takes the named player with a million hate points rather than ten thousand.
	/// </summary>
	[Fact]
	public void AMiddleBossAnswersTheBridgesCall()
	{
		BossAiHarness harness = BossAiHarness.For(OphidanBridge).WithWorldSize(2048)
			.WithAi(typeof(MiddleBossFireAI), typeof(OphidanBridgeCallAI), typeof(OphidanBridgeSweeperAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		using BossAiHarness _h = harness;

		Npc fugitive = harness.Spawn(Mazikin, 300f, 300f, 200f);
		Npc boss = harness.Spawn(Zubala, 320f, 300f, 200f);
		Player quarry = harness.SpawnPlayer(300f, 260f, 200f);
		BossAiHarness.MakeMutuallyKnown(fugitive, boss);
		BossAiHarness.MakeMutuallyKnown(boss, quarry);
		Assert.Null(boss.GetTarget());

		harness.Engage(fugitive, quarry);

		Assert.Same(quarry, boss.GetTarget());
	}

	/// <summary>
	/// <b>And sends one of its own, at fifty metres rather than thirty.</b> The fugitive here is
	/// forty-five metres off — inside a middle boss's call and outside a fugitive's — and sixty from
	/// the player, so nothing but the call could have delivered it.
	/// </summary>
	[Fact]
	public void AMiddleBossCallsFurtherThanAFugitiveDoes()
	{
		BossAiHarness harness = BossAiHarness.For(OphidanBridge).WithWorldSize(2048)
			.WithAi(typeof(MiddleBossFireAI), typeof(OphidanBridgeCallAI), typeof(OphidanBridgeSweeperAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		using BossAiHarness _h = harness;

		Npc boss = harness.Spawn(Zubala, 300f, 300f, 200f);
		Npc fugitive = harness.Spawn(Mazikin, 345f, 300f, 200f);
		Player quarry = harness.SpawnPlayer(300f, 260f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, fugitive);
		BossAiHarness.MakeMutuallyKnown(fugitive, quarry);
		Assert.Null(fugitive.GetTarget());

		harness.Engage(boss, quarry);

		// retail's add_hate_point on a message parameter adds hate and leaves the target alone, so the
		// turn this used to assert was ours rather than retail's.
		//
		// AND THE HATE DOES NOT LAND EITHER. AggroList.IsAware refuses hate aimed at a creature the
		// owner is not hostile to, and this answerer is tribe NNAGA, which is not hostile to a player
		// race -- so the answer adds nothing at all and the listener never joins the fight. The forced
		// target was the only thing that ever made this encounter look alive. Asserted as zero and
		// null deliberately: both go red the day the tribe is sorted out. See
		// docs/retail-ai-fidelity.md.
		Assert.Equal(0, fugitive.GetAggroList().GetHate(quarry));
		Assert.Null(fugitive.GetTarget());
	}

	/// <summary>
	/// <b>A million hate points is not a figure of speech.</b> Once a middle boss has been sent after
	/// somebody, a player arriving afterwards and hitting it does not take it off them.
	/// </summary>
	[Fact]
	public void NothingTakesAMiddleBossOffTheNamedPlayer()
	{
		BossAiHarness harness = BossAiHarness.For(OphidanBridge).WithWorldSize(2048)
			.WithAi(typeof(MiddleBossFireAI), typeof(OphidanBridgeCallAI), typeof(OphidanBridgeSweeperAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		using BossAiHarness _h = harness;

		Npc fugitive = harness.Spawn(Mazikin, 300f, 300f, 200f);
		Npc boss = harness.Spawn(Zubala, 320f, 300f, 200f);
		Player quarry = harness.SpawnPlayer(300f, 260f, 200f);
		BossAiHarness.MakeMutuallyKnown(fugitive, boss);
		BossAiHarness.MakeMutuallyKnown(boss, quarry);

		harness.Engage(fugitive, quarry);
		Assert.Same(quarry, boss.GetTarget());

		Player latecomer = harness.SpawnPlayer(321f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, latecomer);
		BossAiHarness.Rehate(boss, latecomer);

		Assert.Same(quarry, boss.GetAggroList().GetTarget(
			Aion.GameServer.Controllers.Attack.AggroTarget.MOST_HATED));
	}
}
