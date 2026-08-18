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

	private static readonly int[] Portals = [282014, 282015, 282131];

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(AbyssalSplinter).WithWorldSize(2048)
			.WithAi(typeof(YamennesAI), typeof(YamennesSpawnGateAI), typeof(GatesSummonedAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc boss = harness.Spawn(Yamennes, 330f, 730f, 216f);
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

		harness.Clock.Advance(TimeSpan.FromSeconds(69));
		Assert.All(first, portal => Assert.Contains(portal, Standing(harness)));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.DoesNotContain(Standing(harness), n => first.Contains(n));
	}
}
