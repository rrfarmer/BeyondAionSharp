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
/// summoned four at a time on a three-percent roll per blow and never removed them, so a long fight
/// accumulated them without bound.
/// <para>
/// <b>The roll cannot be forced.</b> <c>BossAiHarness</c>'s roll helpers reach <c>PatternAi.RollPercent</c>
/// and this is a Java-parity class calling <c>Rnd.Chance()</c> directly, so the pin lands the blow until a
/// wave appears. At three percent a blow, five hundred blows miss with probability around three in ten
/// million — <b>stated rather than hidden</b>, because a bounded retry loop is a real if small source of
/// flake and the last four flaky pins in this log were all pins that looked deterministic.
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

	/// <summary>Lands blows until a wave appears, or gives up after five hundred.</summary>
	private static (BossAiHarness, Npc) WithAWave()
	{
		BossAiHarness harness = BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(BrigadeGeneralLaksyakaAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc boss = harness.Spawn(Laksyaka, 644f, 1319f, 488f);
		Player player = harness.SpawnPlayer(646f, 1321f, 488f);
		harness.Engage(boss, player);

		for (int i = 0; i < 500 && Skeletons(harness) == 0; i++)
			boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);

		Assert.True(Skeletons(harness) > 0, "no wave in five hundred blows");
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
