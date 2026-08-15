using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="OmegaAI"/>, translated from retail pattern <c>LF4_FieldRaid</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// His waves used to come from <c>ai/spawn_helpers.xml</c> at 80/60/40/20, accumulating rather than
/// rotating, and the clone of magical barrier was spawned by nothing at all. Every assertion here is
/// about a difference from that: the thresholds, the clearing of the previous wave, and the pair that
/// closes the fight.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class OmegaAiTests
{
	private const int Inggison = 210050000;
	private const int Omega = 216516;

	private const int CloneOfPower = 281945;
	private const int CloneOfExplosion = 281946;
	private const int CloneOfHealing = 281947;
	private const int CloneOfPhysicalBarrier = 281948;
	private const int CloneOfMagicalBarrier = 281949;

	private static readonly TimeSpan PhaseTick = TimeSpan.FromSeconds(5);

	private static BossAiHarness NewHarness() => BossAiHarness.For(Inggison)
		.WithWorldSize(4096)
		.WithAi(typeof(OmegaAI), typeof(AggressiveNpcAI), typeof(CloneOfBarrierAI))
		.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Drops him to <paramref name="percent"/> and lets one phase tick elapse.</summary>
	private static void PhaseAt(BossAiHarness harness, Npc boss, Player player, int percent)
	{
		BossAiHarness.SetHpPercent(boss, percent);
		BossAiHarness.Rehate(boss, player);
		harness.Clock.Advance(PhaseTick);
	}

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Omega, 1780f, 2260f, 300f);
		Player player = harness.SpawnPlayer(1783f, 2262f, 300f);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	[Fact]
	public void RotatesItsWavesRatherThanAccumulatingThem()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			PhaseAt(harness, boss, player, 84);
			Assert.Equal(3, Count(harness, CloneOfPower));

			// Each phase clears the one before it. The old summon table never cleared anything, so by
			// the last phase twelve clones were up at once.
			PhaseAt(harness, boss, player, 64);
			Assert.Equal(0, Count(harness, CloneOfPower));
			Assert.Equal(3, Count(harness, CloneOfExplosion));

			PhaseAt(harness, boss, player, 44);
			Assert.Equal(0, Count(harness, CloneOfExplosion));
			Assert.Equal(3, Count(harness, CloneOfHealing));

			PhaseAt(harness, boss, player, 24);
			Assert.Equal(0, Count(harness, CloneOfHealing));
		}
	}

	[Fact]
	public void ClosesWithOneBarrierCloneOfEachKind()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			foreach (int hp in new[] { 84, 64, 44, 24 })
				PhaseAt(harness, boss, player, hp);

			// The last wave is a pair, not a third trio. Ours sent three physical barriers and the
			// magical one existed only in npc_templates.
			Assert.Equal(1, Count(harness, CloneOfPhysicalBarrier));
			Assert.Equal(1, Count(harness, CloneOfMagicalBarrier));
		}
	}

	[Fact]
	public void HoldsEachWaveUntilItsOwnRetailThreshold()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			// 86 is inside the old 80 threshold's band but above retail's 85, so this distinguishes the
			// two numbers rather than merely showing that something eventually spawns.
			PhaseAt(harness, boss, player, 86);
			Assert.Equal(0, Count(harness, CloneOfPower));

			PhaseAt(harness, boss, player, 84);
			Assert.Equal(3, Count(harness, CloneOfPower));

			PhaseAt(harness, boss, player, 66);
			Assert.Equal(0, Count(harness, CloneOfExplosion));

			PhaseAt(harness, boss, player, 64);
			Assert.Equal(3, Count(harness, CloneOfExplosion));
		}
	}

	[Fact]
	public void SummonsEachWaveExactlyOnce()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			PhaseAt(harness, boss, player, 84);
			Assert.Equal(3, Count(harness, CloneOfPower));

			// Sitting in the same band for another minute must not keep summoning: the phase branches
			// are one-shot steps, not a regime that fires on every tick.
			harness.Clock.Advance(TimeSpan.FromMinutes(1));
			Assert.Equal(3, Count(harness, CloneOfPower));
		}
	}

	[Fact]
	public void PointsHisBarrierCloneAtWhoeverHeIsFighting()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			// A second player the clone has no reason to prefer, so hate on the tank has to come from
			// Omega's rally call rather than from being the only creature in the room.
			Player bystander = harness.SpawnPlayer(1786f, 2264f, 300f);
			BossAiHarness.MakeMutuallyKnown(boss, bystander);

			foreach (int hp in new[] { 84, 64, 44, 24 })
				PhaseAt(harness, boss, player, hp);

			// Only the physical barrier clone runs a pattern; the other three are plain aggressive NPCs
			// and hear nothing, which is why this is asserted on the last wave rather than the first.
			Npc clone = harness.LiveNpcs().Single(n => n.GetNpcId() == CloneOfPhysicalBarrier);
			Assert.True(clone.GetAggroList().GetHate(player) > 0,
				"the barrier clone should have arrived already hating Omega's target");
			Assert.Equal(0, clone.GetAggroList().GetHate(bystander));
		}
	}

	[Fact]
	public void ClearsEveryWaveWhenHeDies()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			PhaseAt(harness, boss, player, 84);
			PhaseAt(harness, boss, player, 24);
			Assert.True(Count(harness, CloneOfMagicalBarrier) > 0);

			boss.GetAi().OnGeneralEvent(AiEventType.Died);

			foreach (int clone in new[] { CloneOfPower, CloneOfExplosion, CloneOfHealing,
				CloneOfPhysicalBarrier, CloneOfMagicalBarrier })
			{
				Assert.Equal(0, Count(harness, clone));
			}
		}
	}
}
