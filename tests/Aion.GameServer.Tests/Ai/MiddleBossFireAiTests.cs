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
}
