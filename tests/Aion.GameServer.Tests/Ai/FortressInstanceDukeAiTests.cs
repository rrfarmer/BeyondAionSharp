using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The three barracks dukes get the same parting shot the three chamber lords already had.
/// </summary>
/// <remarks>
/// Retail runs one pattern for both — <c>BGuard_ChiefD</c> for the lords and <c>BGuard_ChiefD_Tune405</c>
/// for the dukes, with an identical <c>on_die</c>. <b>Only the lords had it here</b>, because they are a
/// pattern class and the dukes are a Java-parity one, and the Java side has no death wave at all.
/// <para>
/// The barracks share the chambers' layout, which is why the pattern's absolute coordinates work for
/// both. <b>That was checked rather than assumed</b>: each duke stands inside the box the four spawn
/// points describe.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class FortressInstanceDukeAiTests
{
	private const int DrakanByTeleporter = 296339;
	private const int DrakanByBarrier = 296338;

	public static TheoryData<int, int, float, float, float> Dukes => new()
	{
		{ 301260000, 233633, 526.401f, 845.38f, 199.395f },   // Crotan, Legion's Krotan Barracks
		{ 301240000, 233676, 526.401f, 845.38f, 199.395f },   // Dkisas, Legion's Kysis Barracks
		{ 301250000, 233719, 526.401f, 845.38f, 199.395f },   // Lamiren, Legion's Miren Barracks
	};

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary><b>Six leave by teleporter and three through the barrier</b>, as for the lords.</summary>
	[Theory]
	[MemberData(nameof(Dukes))]
	public void DyingBringsSixByTeleporterAndThreeThroughTheBarrier(
		int mapId, int npcId, float x, float y, float z)
	{
		using BossAiHarness harness = BossAiHarness.For(mapId).WithWorldSize(2048)
			.WithAi(typeof(FortressInstanceDukeAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc duke = harness.Spawn(npcId, x, y, z);
		Assert.Equal(0, Count(harness, DrakanByTeleporter));

		duke.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(6, Count(harness, DrakanByTeleporter));
		Assert.Equal(3, Count(harness, DrakanByBarrier));
	}

	/// <summary>
	/// <b>Two at each of the three points, not six in one place.</b> The pin that separates "the wave
	/// happened" from "the wave happened where retail puts it" — a single loop bug would pass the count.
	/// </summary>
	[Fact]
	public void TheyArriveAtThreeSeparatePoints()
	{
		using BossAiHarness harness = BossAiHarness.For(301260000).WithWorldSize(2048)
			.WithAi(typeof(FortressInstanceDukeAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc duke = harness.Spawn(233633, 526.401f, 845.38f, 199.395f);

		duke.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(3, harness.LiveNpcs()
			.Where(n => n.GetNpcId() == DrakanByTeleporter)
			.Select(n => (n.GetX(), n.GetY()))
			.Distinct()
			.Count());
	}
	/// <summary>
	/// <b>A parting shot, not permanent scenery.</b> The barrier group goes at twelve seconds and the
	/// teleported at eighteen.
	/// </summary>
	/// <remarks>
	/// This is the pin the whole change waited on. The Java-parity path had no timed despawn, so these
	/// seven would have stood in the barracks forever — <b>the wave without its lifetime is worse than no
	/// wave</b>, and a count-only pin would have called that a success.
	/// </remarks>
	[Fact]
	public void TheDeathWaveTimesOut()
	{
		using BossAiHarness harness = BossAiHarness.For(301260000).WithWorldSize(2048)
			.WithAi(typeof(FortressInstanceDukeAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc duke = harness.Spawn(233633, 526.401f, 845.38f, 199.395f);

		duke.GetAi().OnGeneralEvent(AiEventType.Died);
		Assert.Equal(6, Count(harness, DrakanByTeleporter));
		Assert.Equal(3, Count(harness, DrakanByBarrier));

		harness.Clock.Advance(TimeSpan.FromSeconds(14));
		Assert.Equal(6, Count(harness, DrakanByTeleporter));
		Assert.Equal(0, Count(harness, DrakanByBarrier));

		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		Assert.Equal(0, Count(harness, DrakanByTeleporter));
	}
}
