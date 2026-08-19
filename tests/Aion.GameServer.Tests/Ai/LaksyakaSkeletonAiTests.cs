using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Laksyaka's skeletons, which used to stand for the whole fight.
/// </summary>
/// <remarks>
/// Retail <c>IDTiamat_Rakshaka</c> gives <c>IDTiamat_Rakshaka_Skeleton</c> twenty seconds. This class
/// summoned four at a time and never removed them, so a long fight accumulated them without bound.
/// <para>
/// <b>These pins used to land up to five hundred blows to force a three-percent roll</b>, because that
/// roll was how the wave arrived. It is not any more: retail arms the wave on a battle timer at fifteen
/// seconds and re-arms it at twenty, and the roll is gone. The setup advances the clock instead, and the
/// retry loop — honestly documented at the time as a small source of flake — goes with it.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class LaksyakaSkeletonAiTests
{
	private const int TiamatStronghold = 300510000;
	private const int Laksyaka = 219356;
	private const int Skeleton = 283115;

	private static int Skeletons(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == Skeleton);

	/// <summary>Advances to retail's first wave, fifteen seconds into the fight.</summary>
	private static (BossAiHarness, Npc) WithAWave()
	{
		BossAiHarness harness = BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(BrigadeGeneralLaksyakaAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc boss = harness.Spawn(Laksyaka, 644f, 1319f, 488f);
		Player player = harness.SpawnPlayer(646f, 1321f, 488f);
		harness.Engage(boss, player);

		harness.Clock.Advance(TimeSpan.FromSeconds(16));

		Assert.True(Skeletons(harness) > 0, "no wave sixteen seconds into the fight");
		return (harness, boss);
	}

	/// <summary><b>He summons a wave of skeletons.</b></summary>
	[Fact]
	public void HeSummonsSkeletons()
	{
		var (harness, _) = WithAWave();
		using BossAiHarness _h = harness;

		Assert.True(Skeletons(harness) > 0);
	}

	/// <summary>
	/// <b>And they leave at twenty seconds.</b> The pin the change is about — before it, every wave he
	/// ever summoned was still standing when he died.
	/// </summary>
	[Fact]
	public void TheSkeletonsLeaveAtTwentySeconds()
	{
		var (harness, _) = WithAWave();
		using BossAiHarness _h = harness;

		var first = harness.LiveNpcs().Where(n => n.GetNpcId() == Skeleton).ToHashSet();

		harness.Clock.Advance(TimeSpan.FromSeconds(21));

		Assert.DoesNotContain(harness.LiveNpcs(), n => first.Contains(n));
	}
}
