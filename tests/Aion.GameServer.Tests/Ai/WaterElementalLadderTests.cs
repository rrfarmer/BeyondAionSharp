using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The water elemental ladder (213738), whose middle step was flat.
/// </summary>
/// <remarks>
/// Retail's <c>ND2_WhA</c> escalates: two of <c>Su1</c> in the top band, <b>three</b> of <c>Su2</c> in
/// the middle, four of <c>Su3</c> at the bottom, all ten metres out. Ours placed 2 / <b>2</b> / 4, so
/// the fight got no harder between the first band and the third — the step that exists to raise the
/// pressure did nothing.
/// <para>
/// A count comparison found this. It is the plainest kind of defect this work turns up and the easiest
/// to overlook by eye, because two is a perfectly plausible number for a wave.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class WaterElementalLadderTests
{
	private const int Beluslan = 220040000;

	private const int Boss = 213738;
	private const int Top = 280705;
	private const int Middle = 280706;
	private const int Bottom = 280707;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Beluslan).WithWorldSize(2048)
			.WithAi(typeof(SummonerAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>Drops him to a band and reports what each tier added across the threshold.</summary>
	private static (int Top, int Middle, int Bottom) AtPercent(BossAiHarness harness, int percent)
	{
		Npc boss = harness.Spawn(Boss, 500f, 500f, 200f);
		Player player = harness.SpawnPlayer(504f, 500f, 200f);
		harness.Engage(boss, player);
		BossAiHarness.SetExactPercent(boss, percent);
		boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, boss);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		return (harness.LiveNpcs().Count(n => n.GetNpcId() == Top),
			harness.LiveNpcs().Count(n => n.GetNpcId() == Middle),
			harness.LiveNpcs().Count(n => n.GetNpcId() == Bottom));
	}

	/// <summary>
	/// <b>The ladder climbs two, three, four.</b>
	/// </summary>
	/// <remarks>
	/// Asserted together rather than one band per pin, because the defect was not any single number —
	/// it was that the sequence stopped climbing. Reading them side by side is what makes 2/2/4 look
	/// wrong; each on its own looks fine.
	/// </remarks>
	[Fact]
	public void TheLadderClimbsTwoThreeFour()
	{
		using BossAiHarness harness = NewHarness();
		(int top, int middle, int bottom) = AtPercent(harness, 30);

		Assert.Equal(2, top);
		Assert.Equal(3, middle);
		Assert.Equal(4, bottom);
	}
}
