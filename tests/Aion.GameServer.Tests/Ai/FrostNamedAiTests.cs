using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="FrostNamedAI"/>, translated from retail pattern
/// <c>DF5_ItemNamed_12_SSH</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Two world bosses on one pattern with identical skill lists, so the shared assertions run against
/// both. The substance is the summon chain below 40 — four waves of six — and the fact that it does
/// not start a moment earlier.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class FrostNamedAiTests
{
	private const int Cygnea = 210070000;
	private const int Tottal = 235971;
	private const int Aizenka = 219933;
	private const int FrostBomb = 855913;

	private const int EarthCleave = 21849;
	private const int TectonicShift = 21850;
	private const int GelidImpel = 21851;
	private const int ShiverWrath = 21852;

	public static TheoryData<int> Bosses => new() { Tottal, Aizenka };

	private static (BossAiHarness, Npc, Player) Engaged(int npcId, int hpPercent)
	{
		BossAiHarness harness = BossAiHarness.For(Cygnea).WithWorldSize(2048)
			.WithAi(typeof(FrostNamedAI), typeof(UseSkillAndDieAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		BossAiHarness.SetHpPercent(boss, hpPercent);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static List<BossAiHarness.QueuedCast> Over(BossAiHarness harness, Npc boss, Player player,
		int seconds)
	{
		var cast = new List<BossAiHarness.QueuedCast>();
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			cast.AddRange(BossAiHarness.DrainQueuedSkills(boss));
		}
		return cast;
	}

	private static int Bombs(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == FrostBomb);

	[Theory]
	[MemberData(nameof(Bosses))]
	public void TheHealthyLoopOpensWithASingleHitThreeSecondsIn(int npcId)
	{
		var (harness, boss, player) = Engaged(npcId, 90);
		using BossAiHarness _h = harness;
		BossAiHarness.DrainQueuedSkills(boss);

		Assert.Equal([EarthCleave], Over(harness, boss, player, 3).Select(c => c.SkillId));
	}

	[Theory]
	[MemberData(nameof(Bosses))]
	public void TheHealthyLoopRunsHitPairHitSwitchPair(int npcId)
	{
		var (harness, boss, player) = Engaged(npcId, 90);
		using BossAiHarness _h = harness;
		BossAiHarness.DrainQueuedSkills(boss);

		// T0 at 3s, T1 at 13, T2 at 27, T3 at 37.5, T4 at 47.5.
		Assert.Equal(
			[EarthCleave, TectonicShift, TectonicShift, EarthCleave, EarthCleave,
				TectonicShift, TectonicShift],
			Over(harness, boss, player, 49).Select(c => c.SkillId));
	}

	/// <summary>The only skill either boss aims at a random target, and the band that uses it.</summary>
	[Theory]
	[MemberData(nameof(Bosses))]
	public void TheMiddleBandOpensWithTheRandomTargetDonut(int npcId)
	{
		var (harness, boss, player) = Engaged(npcId, 60);
		using BossAiHarness _h = harness;
		BossAiHarness.DrainQueuedSkills(boss);

		Assert.Equal([GelidImpel], Over(harness, boss, player, 3).Select(c => c.SkillId));
	}

	/// <summary>
	/// Retail aims index 1 at itself in this band alone, where every other band aims it at the target.
	/// </summary>
	[Theory]
	[MemberData(nameof(Bosses))]
	public void TheMiddleBandsSingleHitIsSelfCast(int npcId)
	{
		var (harness, boss, player) = Engaged(npcId, 60);
		using BossAiHarness _h = harness;

		List<BossAiHarness.QueuedCast> cast = Over(harness, boss, player, 15);

		Assert.Equal(NpcSkillTargetAttribute.ME,
			cast.Single(c => c.SkillId == EarthCleave).Target);
	}

	[Theory]
	[MemberData(nameof(Bosses))]
	public void AboveFortyNoBombIsEverPlaced(int npcId)
	{
		var (harness, boss, player) = Engaged(npcId, 60);
		using BossAiHarness _h = harness;

		Over(harness, boss, player, 80);

		Assert.Equal(0, Bombs(harness));
	}

	[Theory]
	[MemberData(nameof(Bosses))]
	public void BelowFortyTheFirstWaveIsSixBombs(int npcId)
	{
		var (harness, boss, player) = Engaged(npcId, 35);
		using BossAiHarness _h = harness;

		List<BossAiHarness.QueuedCast> cast = Over(harness, boss, player, 3);

		Assert.Equal(6, Bombs(harness));
		Assert.Contains(cast, c => c.SkillId == ShiverWrath);
	}

	/// <summary>
	/// Four waves of six, eight seconds apart, and then the chain stops — retail's last wave arms a
	/// timer no branch answers. Twenty-four bombs over the chain, not an endless stream.
	/// </summary>
	/// <remarks>
	/// Counted by identity rather than by how many are standing: the bombs run <c>useSkillAndDie</c>
	/// and take themselves off the field, so a live count only ever shows the most recent wave.
	/// </remarks>
	[Theory]
	[MemberData(nameof(Bosses))]
	public void TheSummonChainIsFourWavesAndThenStops(int npcId)
	{
		var (harness, boss, player) = Engaged(npcId, 35);
		using BossAiHarness _h = harness;
		var seen = new HashSet<Npc>();

		int Placed(int seconds)
		{
			for (int i = 0; i < seconds; i++)
			{
				BossAiHarness.Rehate(boss, player);
				BossAiHarness.KeepAlive(player);
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
				foreach (Npc bomb in harness.LiveNpcs().Where(n => n.GetNpcId() == FrostBomb))
					seen.Add(bomb);
			}
			return seen.Count;
		}

		Assert.Equal(6, Placed(3));
		Assert.Equal(12, Placed(8));
		Assert.Equal(18, Placed(8));
		Assert.Equal(24, Placed(8));

		// A further wave interval with nothing new: the chain has run out.
		Assert.Equal(24, Placed(8));
	}

	/// <summary>
	/// The first wave lights timer 1 on a 36-second fuse, so the ordinary rotation runs alongside the
	/// waves rather than being replaced by them.
	/// </summary>
	[Fact]
	public void TheRotationRestartsThirtySixSecondsAfterTheFirstWave()
	{
		var (harness, boss, player) = Engaged(Tottal, 35);
		using BossAiHarness _h = harness;
		Over(harness, boss, player, 3);
		BossAiHarness.DrainQueuedSkills(boss);

		// Nothing from the rotation until the fuse burns down at 39s.
		Assert.DoesNotContain(EarthCleave, Over(harness, boss, player, 35).Select(c => c.SkillId));

		Assert.Contains(EarthCleave, Over(harness, boss, player, 2).Select(c => c.SkillId));
	}

	[Theory]
	[MemberData(nameof(Bosses))]
	public void DyingClearsTheBombs(int npcId)
	{
		var (harness, boss, player) = Engaged(npcId, 35);
		using BossAiHarness _h = harness;
		Over(harness, boss, player, 3);
		Assert.True(Bombs(harness) > 0);

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Bombs(harness));
	}
}
