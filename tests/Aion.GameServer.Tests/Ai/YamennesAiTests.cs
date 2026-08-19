using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Yamennes' summoning portals, which used to arrive once and then never again.
/// </summary>
/// <remarks>
/// Retail <c>IDAbRe_Core_NamedD_Hard</c> gives them <c>live_time</c> 70 on a timer re-armed at 70
/// seconds, so one set expires exactly as the next arrives and the branch spawns unconditionally. This
/// class gave them no lifetime and spawned <b>only when none of the three were still standing</b> — so a
/// group that ignored the portals rather than killing them saw the first wave and never another.
/// <para>
/// The unstable variant had already been corrected for exactly this, in an earlier pass, and this class
/// kept the old shape. <b>The pin is written on identity rather than on a count</b>, because a second
/// wave of three looks identical to a first wave of three that never left.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class YamennesAiTests
{
	private const int AbyssalSplinter = 300220000;
	private const int Yamennes = 216960;

	/// <summary>Every gate either floor can open. See <see cref="YamennesAI.UpperGateA"/>.</summary>
	private static readonly int[] Portals =
	[
		YamennesAI.UpperGateA, YamennesAI.UpperGateB, YamennesAI.UpperGateC, YamennesAI.LowerGate,
	];

	private const int Golem = 282107;

	/// <summary>The normal-mode Yamennes, IDAbRe_Core_NamedD.</summary>
	private const int NormalYamennes = 216952;

	private static (BossAiHarness, Npc, Player) Engaged() => EngagedAs(Yamennes);

	private static (BossAiHarness, Npc, Player) EngagedAs(int npcId)
	{
		BossAiHarness harness = BossAiHarness.For(AbyssalSplinter).WithWorldSize(2048)
			.WithAi(typeof(YamennesAI), typeof(YamennesSpawnGateAI), typeof(GatesSummonedAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc boss = harness.Spawn(npcId, 330f, 730f, 216f);
		Player player = harness.SpawnPlayer(332f, 732f, 216f);
		harness.Engage(boss, player);

		// His portal clock starts on the first blow landed on him, not on entering combat.
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
		return (harness, boss, player);
	}

	private static List<Npc> Standing(BossAiHarness harness) =>
		harness.LiveNpcs().Where(n => Portals.Contains(n.GetNpcId())).ToList();

	/// <summary>Three portals a minute into the fight.</summary>
	[Fact]
	public void ThreePortalsArriveAfterAMinute()
	{
		var (harness, _, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(60));

		Assert.Equal(3, Standing(harness).Count);
	}

	/// <summary>
	/// <b>And a fresh set follows, whether or not the first was killed.</b> Nothing here touches the
	/// first three, and before the fix that alone stopped every later wave.
	/// </summary>
	/// <remarks>
	/// <b>This pin measures the lifetime, not the removal of the guard.</b> Putting the old
	/// "only if none are standing" test back leaves it green, because with the portals expiring at
	/// seventy seconds the guard finds an empty room every time it looks and never blocks anything.
	/// <para>
	/// So the guard removal is <b>not independently observable</b> in the fixed configuration — the same
	/// conclusion Pazuzu reached. It is kept because retail spawns unconditionally and because the guard
	/// is what turned a missing lifetime into a dead mechanic, but <b>no pin here proves it</b>, and
	/// claiming otherwise would be the sort of pin that passes for the wrong reason.
	/// </para>
	/// </remarks>
	[Fact]
	public void AFreshSetArrivesEvenIfTheFirstIsIgnored()
	{
		var (harness, _, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(60));
		var first = Standing(harness).ToHashSet();
		Assert.Equal(3, first.Count);

		// The set spawned at sixty expires at a hundred and thirty, and the next is due at the same
		// moment; a second past that is the first tick where only the new set is standing.
		harness.Clock.Advance(TimeSpan.FromSeconds(71));

		var later = Standing(harness);
		Assert.NotEmpty(later);
		Assert.DoesNotContain(later, n => first.Contains(n));
	}

	/// <summary>
	/// <b>They expire on their own.</b> Stated separately so the pin above cannot pass merely because the
	/// portals are replaced — the old ones have to actually leave.
	/// </summary>
	[Fact]
	public void ThePortalsExpireOnRetailsSeventySeconds()
	{
		var (harness, _, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(60));
		var first = Standing(harness).ToHashSet();

		// Both assertions below range over `first`, so an empty snapshot satisfies the whole pin --
		// it would report that portals expire correctly in a fight that never placed any.
		Assert.Equal(3, first.Count);

		harness.Clock.Advance(TimeSpan.FromSeconds(69));
		Assert.All(first, portal => Assert.Contains(portal, Standing(harness)));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.DoesNotContain(Standing(harness), n => first.Contains(n));
	}
	/// <summary>
	/// <b>The golems stand on their own three marks, on their own three-minute clock.</b>
	/// </summary>
	/// <remarks>
	/// They used to ride the healing-debuff chain and be placed ten metres diagonally off Yamennes, so
	/// they followed him around the room and arrived on the debuff's cadence rather than their own. Retail
	/// gives them absolute marks and a timer of their own.
	/// </remarks>
	[Fact]
	public void ThreeGolemsStandOnRetailsMarks()
	{
		var (harness, _, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(181));

		var golems = harness.LiveNpcs().Where(n => n.GetNpcId() == Golem).ToList();
		Assert.Equal(3, golems.Count);

		// Retail's marks, not the boss's position: he stands at 330,730 and none of these is near him.
		Assert.Contains(golems, g => Math.Abs(g.GetX() - 361.53f) < 1f);
		Assert.Contains(golems, g => Math.Abs(g.GetX() - 302.85f) < 1f);
		Assert.Contains(golems, g => Math.Abs(g.GetX() - 334.30f) < 1f);
	}

	/// <summary>
	/// <b>And they expire on their own three minutes rather than being cleared by the next debuff.</b>
	/// </summary>
	[Fact]
	public void TheGolemsExpireOnTheirOwnClock()
	{
		var (harness, _, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(181));
		var first = harness.LiveNpcs().Where(n => n.GetNpcId() == Golem).ToHashSet();
		Assert.Equal(3, first.Count);

		harness.Clock.Advance(TimeSpan.FromSeconds(181));

		Assert.DoesNotContain(harness.LiveNpcs(), n => first.Contains(n));
	}
	/// <summary>
	/// <b>The normal Yamennes gets the portals and not the golems.</b>
	/// </summary>
	/// <remarks>
	/// 216952 ran <c>aggressive</c> -- a scripted fight served as a plain melee npc -- and shares almost
	/// all of 216960''s pattern. <b>Almost.</b> <c>IDAbRe_Core_NamedD</c> has the same three portals and no
	/// ametgolems at all; the golems are hard mode only.
	/// <para>
	/// Binding the two npcs to one class is right, and doing it without gating the golems would have
	/// handed the normal fight a hard-mode mechanic. <b>The first version of that gate returned early and
	/// skipped the portal clock as well</b>, which would have left the normal fight with neither -- caught
	/// by reading the method rather than by a pin, because no pin covered 216952 at the time.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheNormalYamennesHasPortalsButNoGolems()
	{
		using BossAiHarness harness = BossAiHarness.For(AbyssalSplinter).WithWorldSize(2048)
			.WithAi(typeof(YamennesAI), typeof(YamennesSpawnGateAI), typeof(GatesSummonedAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc normal = harness.Spawn(NormalYamennes, 330f, 730f, 216f);
		Player player = harness.SpawnPlayer(332f, 732f, 216f);
		harness.Engage(normal, player);
		normal.GetAi().OnCreatureEvent(AiEventType.Attack, player);

		harness.Clock.Advance(TimeSpan.FromSeconds(181));

		Assert.Equal(3, harness.LiveNpcs().Count(n => Portals.Contains(n.GetNpcId())));
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == Golem));
	}

	/// <summary>Retail's <c>IDCatacombs_Hard_Buff</c> and <c>..._Sum_NamedD_onDie</c>.</summary>
	private const int ProtectorsFury = 281819;
	private const int YamennesSliver = 282065;

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>The fury wave, which neither Yamennes had</b> — and the two modes are paced differently.
	/// </summary>
	/// <remarks>
	/// Both patterns carry it and this class ported neither, so the fight's only continuous add stream
	/// was missing entirely. The hard mode is much the harsher of the two: <b>three every eight seconds
	/// from fifty-four</b>, against two every twenty from sixty. Asserting the first wave's timing and
	/// size together is what separates the modes; a count alone would pass on either cadence.
	/// </remarks>
	[Theory]
	[InlineData(Yamennes, 54, 3)]
	[InlineData(NormalYamennes, 60, 2)]
	public void EachModeSendsItsOwnFuryWave(int npcId, int firstAt, int perWave)
	{
		var (harness, boss, _) = EngagedAs(npcId);
		using BossAiHarness _h = harness;

		// Five on the hate list, comfortably more than either cap. One player would give one fury
		// whatever the cap said, so a raid is the only way this pin can tell the two modes apart.
		for (int i = 0; i < 4; i++)
			BossAiHarness.Rehate(boss, harness.SpawnPlayer(334f + i, 732f, 216f));

		harness.Clock.Advance(TimeSpan.FromSeconds(firstAt - 2));
		Assert.Equal(0, Count(harness, ProtectorsFury));

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.Equal(perWave, Count(harness, ProtectorsFury));
	}

	/// <summary><b>And each fury leaves at ten seconds</b>, which is retail's <c>live_time</c>.</summary>
	[Fact]
	public void TheFuriesLeaveAtTenSeconds()
	{
		var (harness, _, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(55));
		var first = harness.LiveNpcs().Where(n => n.GetNpcId() == ProtectorsFury).ToHashSet();
		Assert.NotEmpty(first);

		harness.Clock.Advance(TimeSpan.FromSeconds(10));
		Assert.DoesNotContain(harness.LiveNpcs(), n => first.Contains(n));
	}

	/// <summary>
	/// <b>One sliver, where he falls.</b> Retail's <c>target_obj=OBJI_SELF</c>, and one and not two
	/// because the hard pattern's duplicated death branch shares a test-and-set flag var.
	/// </summary>
	[Theory]
	[InlineData(Yamennes)]
	[InlineData(NormalYamennes)]
	public void HeLeavesOneSliverWhereHeFalls(int npcId)
	{
		var (harness, boss, _) = EngagedAs(npcId);
		using BossAiHarness _h = harness;
		Assert.Equal(0, Count(harness, YamennesSliver));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Npc sliver = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == YamennesSliver);
		Assert.Equal(boss.GetX(), sliver.GetX(), 1);
		Assert.Equal(boss.GetY(), sliver.GetY(), 1);
	}

	/// <summary>
	/// <b>The upper floor opens three different gates.</b>
	/// </summary>
	/// <remarks>
	/// Retail's set branch names <c>IDAbRe_Core_Sum_Teleport2</c>, <c>_03</c> and <c>_06</c>. This class
	/// used <c>_03</c>, <c>_06</c> and <c>_Low</c>, so <b>281906 never appeared in the encounter at
	/// all</b> and the lower floor's gate was doing duty upstairs.
	/// </remarks>
	[Fact]
	public void TheUpperFloorOpensThreeDifferentGates()
	{
		var (harness, boss, player) = EngagedAs(Yamennes);
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(61));

		List<int> opened = harness.LiveNpcs().Where(n => Portals.Contains(n.GetNpcId()))
			.Select(n => n.GetNpcId()).OrderBy(i => i).ToList();
		Assert.Equal(
			new[] { YamennesAI.UpperGateA, YamennesAI.UpperGateB, YamennesAI.UpperGateC }
				.OrderBy(i => i).ToList(),
			opened);
	}

	/// <summary>
	/// <b>The lower floor opens the same gate three times.</b>
	/// </summary>
	/// <remarks>
	/// Retail's test-and-unset branch names <c>_Low</c> three times over — not three different gates.
	/// The two floors are not mirror images, and translating them as if they were is what put the wrong
	/// npcs on both.
	/// </remarks>
	[Fact]
	public void TheLowerFloorOpensTheSameGateThreeTimes()
	{
		var (harness, boss, player) = EngagedAs(Yamennes);
		using BossAiHarness _h = harness;

		// Past the first cycle and into the second, which is the lower floor.
		harness.Clock.Advance(TimeSpan.FromSeconds(61));
		harness.Clock.Advance(TimeSpan.FromSeconds(71));

		List<int> opened = harness.LiveNpcs().Where(n => Portals.Contains(n.GetNpcId()))
			.Select(n => n.GetNpcId()).ToList();
		Assert.Equal(3, opened.Count);
		Assert.All(opened, id => Assert.Equal(YamennesAI.LowerGate, id));
	}
}
