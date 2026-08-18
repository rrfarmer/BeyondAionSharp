using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Adds that remove themselves, pinned on the add rather than on whoever summons it.
/// </summary>
/// <remarks>
/// <b>This is the shape those pins should always have taken.</b> Several fixes in this log went unpinned
/// on the grounds that the summoner had no spawn entry naming a map — but when the clock lives in the
/// add's own class, the summoner is not involved at all. <b>An add can be dropped into any map and asked
/// when it leaves</b>, which is the whole of the question.
/// <para>
/// It also pins the half that kept going wrong. Four summoner-side lifetimes in this log were dead code
/// because the add already had a clock, and two more took effect only by being the smaller of two
/// numbers. <b>Pinning the add is what makes that visible</b>: if the number moves back, this file goes
/// red no matter which class the duplicate lived in.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SelfBoundingAddAiTests
{
	/// <summary>Any map with room; none of these adds cares where it stands.</summary>
	private const int SomeMap = 300520000;

	private const int SparkOfDarkness = 282373;
	private const int ThickDust = 283134;

	private static (BossAiHarness, Npc) Placed(int npcId)
	{
		BossAiHarness harness = BossAiHarness.For(SomeMap).WithWorldSize(2048)
			.WithAi(typeof(SparkOfDarknessAI), typeof(ThickDustAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();
		return (harness, harness.Spawn(npcId, 504f, 514f, 417.5f));
	}

	private static bool Standing(BossAiHarness harness, Npc add) =>
		harness.LiveNpcs().Contains(add);

	/// <summary>
	/// <b>The arena spark burns out at five seconds.</b> Retail's number; Java used six and a half, and
	/// the encounter that summons it has no spawn entry in our data, so this is the only way to ask.
	/// </summary>
	[Fact]
	public void TheSparkBurnsOutAtFiveSeconds()
	{
		var (harness, spark) = Placed(SparkOfDarkness);
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(4));
		Assert.True(Standing(harness, spark), "the spark left before its five seconds");

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.False(Standing(harness, spark), "the spark outlasted its five seconds");
	}

	/// <summary>
	/// <b>And Tiamat's dust clears at six.</b> Pinned here as well as through the encounter, because the
	/// encounter pin only sees it while the dragon is there to leave it.
	/// </summary>
	[Fact]
	public void TheDustClearsAtSixSeconds()
	{
		var (harness, dust) = Placed(ThickDust);
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		Assert.True(Standing(harness, dust), "the dust left before its six seconds");

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.False(Standing(harness, dust), "the dust outlasted its six seconds");
	}
}
