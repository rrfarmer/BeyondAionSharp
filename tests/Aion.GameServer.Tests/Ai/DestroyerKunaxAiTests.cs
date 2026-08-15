using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="DestroyerKunaxAI"/>, translated from retail pattern
/// <c>IDLDF5_Fortress_Re_Vritra_01</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// His fight is one fixed chain rather than a probability table, so the order and the spacing are the
/// behaviour and both are asserted here. Ours ran the same eight skills off <c>prob="100"</c> entries
/// with cooldowns, which produced neither a fixed order nor a fixed cadence, and never spawned the NPC
/// the last step drops on the tank.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DestroyerKunaxAiTests
{
	private const int IdgelDome = 301310000;
	private const int DestroyerKunax = 287249;
	private const int KunaxsWrath = 855009;

	/// <summary>The chain in order: Ide Scale through Aether Prison.</summary>
	private static readonly int[] Chain =
		{ 21744, 21551, 21552, 21553, 21554, 21555, 21556, 21558 };

	private static readonly TimeSpan FirstStep = TimeSpan.FromSeconds(6);
	private static readonly TimeSpan Step = TimeSpan.FromSeconds(10);

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(IdgelDome)
			.WithAi(typeof(DestroyerKunaxAI), typeof(AggressiveNpcAI))
			.Build();
		Npc boss = harness.Spawn(DestroyerKunax);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);
		BossAiHarness.DrainQueuedSkills(boss);
		return (harness, boss, player);
	}

	[Fact]
	public void RunsItsEightSkillsInOrderTenSecondsApart()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			harness.Clock.Advance(FirstStep);

			var cast = new List<int>();
			cast.AddRange(BossAiHarness.DrainQueuedSkills(boss).Select(c => c.SkillId));
			for (int i = 1; i < Chain.Length; i++)
			{
				BossAiHarness.Rehate(boss, player);
				harness.Clock.Advance(Step);
				cast.AddRange(BossAiHarness.DrainQueuedSkills(boss).Select(c => c.SkillId));
			}

			Assert.Equal(Chain, cast);
		}
	}

	[Fact]
	public void HoldsEachStepForTheFullTenSeconds()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			harness.Clock.Advance(FirstStep);
			BossAiHarness.DrainQueuedSkills(boss);

			harness.Clock.Advance(TimeSpan.FromSeconds(9));
			Assert.Empty(BossAiHarness.DrainQueuedSkills(boss));

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			Assert.Equal(Chain[1], Assert.Single(BossAiHarness.DrainQueuedSkills(boss)).SkillId);
		}
	}

	[Fact]
	public void CastsItsTwoSweepsAtItselfAndTheRestAtItsTarget()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			harness.Clock.Advance(FirstStep);

			var targets = new List<NpcSkillTargetAttribute>();
			targets.AddRange(BossAiHarness.DrainQueuedSkills(boss).Select(c => c.Target));
			for (int i = 1; i < Chain.Length; i++)
			{
				BossAiHarness.Rehate(boss, player);
				harness.Clock.Advance(Step);
				targets.AddRange(BossAiHarness.DrainQueuedSkills(boss).Select(c => c.Target));
			}

			// Steps 3 and 4 are the only two the pattern casts at OBJI_SELF, and that is a load-bearing
			// part of the index mapping rather than an incidental detail.
			Assert.Equal(
			[
				NpcSkillTargetAttribute.MOST_HATED, NpcSkillTargetAttribute.MOST_HATED,
				NpcSkillTargetAttribute.MOST_HATED, NpcSkillTargetAttribute.ME,
				NpcSkillTargetAttribute.ME, NpcSkillTargetAttribute.MOST_HATED,
				NpcSkillTargetAttribute.MOST_HATED, NpcSkillTargetAttribute.MOST_HATED,
			], targets);
		}
	}

	[Fact]
	public void DropsKunaxsWrathOnTheTankWithItsLastStepAndThenStartsOver()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			harness.Clock.Advance(FirstStep);
			for (int i = 1; i < Chain.Length; i++)
			{
				Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == KunaxsWrath));
				BossAiHarness.Rehate(boss, player);
				harness.Clock.Advance(Step);
			}

			Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == KunaxsWrath));

			// The eighth step arms the first again, so the chain is a loop and not a one-off opener.
			BossAiHarness.DrainQueuedSkills(boss);
			BossAiHarness.Rehate(boss, player);
			harness.Clock.Advance(Step);
			Assert.Equal(Chain[0], Assert.Single(BossAiHarness.DrainQueuedSkills(boss)).SkillId);
		}
	}
}
