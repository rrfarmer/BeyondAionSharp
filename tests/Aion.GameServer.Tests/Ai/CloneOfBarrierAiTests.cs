using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="CloneOfBarrierAI"/>, translated from retail pattern
/// <c>LF4_FieldRaid_SumD</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The detonation at 10% did not happen at all before this, and neither of the two NPCs it leaves
/// behind was spawned by anything in the server. The clone is spawned directly here rather than through
/// Omega, so these assertions are about the clone's own pattern and not about his summon waves.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class CloneOfBarrierAiTests
{
	private const int Inggison = 210050000;
	private const int CloneOfPhysicalBarrier = 281948;

	private const int SelfDestruct = 19196;
	private const int SoulEssence = 281764;
	private const int SelfDestructEffect = 281952;

	private static readonly TimeSpan Tick = TimeSpan.FromSeconds(5);

	private static BossAiHarness NewHarness() => BossAiHarness.For(Inggison)
		.WithWorldSize(4096)
		.WithAi(typeof(CloneOfBarrierAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
		.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc clone = harness.Spawn(CloneOfPhysicalBarrier, 1780f, 2260f, 300f);
		Player player = harness.SpawnPlayer(1783f, 2262f, 300f);
		harness.Engage(clone, player);
		return (harness, clone, player);
	}

	[Fact]
	public void DetonatesAtTenPercentLeavingBothOfItsEffectsBehind()
	{
		var (harness, clone, player) = Engaged();
		using (harness)
		{
			BossAiHarness.DrainQueuedSkills(clone);

			// 20 is below the two thresholds its other branches use (70 and 35) and above this one, so
			// this distinguishes 10 specifically rather than showing only that it is hurt.
			BossAiHarness.SetHpPercent(clone, 20);
			BossAiHarness.Rehate(clone, player);
			harness.Clock.Advance(Tick);
			Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(clone), c => c.SkillId == SelfDestruct);

			BossAiHarness.SetHpPercent(clone, 8);
			BossAiHarness.Rehate(clone, player);
			harness.Clock.Advance(Tick);

			Assert.Contains(BossAiHarness.DrainQueuedSkills(clone), c => c.SkillId == SelfDestruct);
			Assert.Equal(1, Count(harness, SoulEssence));
			Assert.Equal(1, Count(harness, SelfDestructEffect));
		}
	}

	[Fact]
	public void RemovesItselfWhenItDetonates()
	{
		var (harness, clone, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(clone, 8);
			BossAiHarness.Rehate(clone, player);
			harness.Clock.Advance(Tick);

			// It blows itself up rather than lingering at 10% health for the raid to finish off.
			Assert.Equal(0, Count(harness, CloneOfPhysicalBarrier));
		}
	}

	[Fact]
	public void LetsBothLeavingsExpireAfterTenSeconds()
	{
		var (harness, clone, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(clone, 8);
			BossAiHarness.Rehate(clone, player);
			harness.Clock.Advance(Tick);
			Assert.Equal(1, Count(harness, SelfDestructEffect));

			harness.Clock.Advance(TimeSpan.FromSeconds(9));
			Assert.Equal(1, Count(harness, SelfDestructEffect));

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			Assert.Equal(0, Count(harness, SelfDestructEffect));
			Assert.Equal(0, Count(harness, SoulEssence));
		}
	}

	[Fact]
	public void LeavesASoulEssenceWhenKilledOutrightInstead()
	{
		var (harness, clone, player) = Engaged();
		using (harness)
		{
			clone.GetAi().OnGeneralEvent(AiEventType.Died);

			// Killed rather than detonated, so only the soul essence -- no self-destruct effect.
			Assert.Equal(1, Count(harness, SoulEssence));
			Assert.Equal(0, Count(harness, SelfDestructEffect));
		}
	}

	[Fact]
	public void DetonatesOnlyOnce()
	{
		var (harness, clone, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(clone, 8);
			BossAiHarness.Rehate(clone, player);
			harness.Clock.Advance(Tick);
			Assert.Equal(1, Count(harness, SelfDestructEffect));

			// A minute later the first effect has long expired and no new one has appeared. A repeating
			// detonation would be spawning a fresh one every five seconds, so a live count of zero here
			// is the assertion. Two things prevent the repeat -- the pattern's test-and-set flag and the
			// despawn that follows it -- so this pins the outcome rather than either mechanism.
			harness.Clock.Advance(TimeSpan.FromMinutes(1));
			Assert.Equal(0, Count(harness, SelfDestructEffect));
		}
	}
}
