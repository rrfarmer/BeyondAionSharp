using Aion.GameServer.Ai.Event;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Walker;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The three captured Drakan scientists, who all ran to the same door and were deleted before reaching it.
/// </summary>
/// <remarks>
/// Retail gives each an eleven-point path and one rung: <c>is_waypoint_index 5, despawn_self</c>. This
/// class sent all three to <c>(838, 1317, 396)</c> — which is index 5 of
/// <c>Path_IDTiamat_Drakan_Surama_1_1</c>, the route belonging to 800425 alone — and deleted every one of
/// them on a nine-second clock whether it had arrived or not. It is the same defect <c>MuraganAI</c>
/// carried, in the same instance, with the same hardcoded coordinate copied across.
/// <para>
/// <b>The release is covered now.</b> It took <c>BossAiHarness.Kill</c> to run a real controller death, and
/// then one more thing: the eyes have to be spawned <i>before</i> the player. See <see cref="Released"/>.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class CapturedDrakanScientistAiTests
{
	private const int TiamatStronghold = 300510000;

	/// <summary>Each scientist, its spawn, and the route it should walk. Written out, not derived.</summary>
	public static TheoryData<int, float, float, string> Scientists() => new TheoryData<int, float, float, string>
	{
		{ 800425, 882.38f, 1262.39f, "3005100002" },
		{ 800426, 888.42f, 1390.02f, "3005100004" },
		{ 800427, 885.2f, 1248.2f, "3005100003" },
	};

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048).WithWalkerRoutes()
			.WithAi(typeof(CapturedDrakanScientistAI), typeof(GeneralNpcAI), typeof(AggressiveNpcAI))
			.Build();

	private static int Alive(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	private static Npc Walking(BossAiHarness harness, int npcId, float x, float y, string routeId)
	{
		Npc scientist = harness.Spawn(npcId, x, y, 397.42f);
		WalkerTemplate route = DataManager.WALKER_DATA.GetWalkerTemplate(routeId);
		scientist.GetMoveController().SetWalkerTemplate(route, 0);
		return scientist;
	}

	private static void ArriveAt(Npc npc, int stepIndex)
	{
		npc.GetMoveController().SetRouteStep(
			npc.GetMoveController().GetWalkerTemplate().GetRouteSteps()[stepIndex]);
		npc.GetAi().OnGeneralEvent(AiEventType.MoveArrived);
	}

	/// <summary>
	/// <b>Each scientist's route starts on its own spawn.</b> That is how the three were picked out of the
	/// ten retail defines: each matches one of them to 0.00m and the next nearest is seven metres off. A
	/// route on the wrong scientist puts it in the corridor at the wrong end.
	/// </summary>
	[Theory]
	[MemberData(nameof(Scientists))]
	public void EachRouteStartsOnItsOwnScientistsSpawn(int npcId, float x, float y, string routeId)
	{
		_ = npcId;
		// The harness is what loads the walker data; reading the holder without one finds it empty.
		using BossAiHarness harness = NewHarness();
		WalkerTemplate route = DataManager.WALKER_DATA.GetWalkerTemplate(routeId);

		Assert.NotNull(route);
		Assert.Equal(11, route.GetRouteSteps().Count);
		Assert.Equal(x, route.GetRouteSteps()[0].GetX(), 1);
		Assert.Equal(y, route.GetRouteSteps()[0].GetY(), 1);
	}

	/// <summary>
	/// <b>The three go to three different doors.</b> The whole defect was one destination standing in for
	/// three: retail's sixth points are metres apart, and a pin that never compares them cannot notice the
	/// shared coordinate coming back.
	/// </summary>
	[Fact]
	public void TheThreeScientistsHaveThreeDifferentDestinations()
	{
		using BossAiHarness harness = NewHarness();
		List<(float X, float Y)> sixth = new List<(float, float)>();
		foreach (object[] row in Scientists().Select(r => r.ToArray()))
		{
			RouteStep step = DataManager.WALKER_DATA
				.GetWalkerTemplate((string)row[3]).GetRouteSteps()[5];
			sixth.Add((step.GetX(), step.GetY()));
		}

		Assert.Equal(3, sixth.Distinct().Count());
	}

	/// <summary>
	/// <b>None of them vanishes on the way.</b> Nine seconds did not cover the corridor, so the escort
	/// disappeared part-way along it every run.
	/// </summary>
	[Theory]
	[MemberData(nameof(Scientists))]
	public void NoneOfThemVanishesOnTheWay(int npcId, float x, float y, string routeId)
	{
		using BossAiHarness harness = NewHarness();
		Npc scientist = Walking(harness, npcId, x, y, routeId);

		for (int step = 1; step <= 4; step++)
		{
			ArriveAt(scientist, step);
			Assert.Equal(1, Alive(harness, npcId));
		}
	}

	/// <summary>
	/// <b>And each goes at the sixth point.</b> Retail's <c>despawn_self</c> on <c>is_waypoint_index 5</c>.
	/// </summary>
	[Theory]
	[MemberData(nameof(Scientists))]
	public void EachGoesAtTheSixthPoint(int npcId, float x, float y, string routeId)
	{
		using BossAiHarness harness = NewHarness();
		Npc scientist = Walking(harness, npcId, x, y, routeId);

		ArriveAt(scientist, 5);

		Assert.Equal(0, Alive(harness, npcId));
	}

	/// <summary>
	/// <b>The class hands each scientist its own route.</b>
	/// </summary>
	/// <remarks>
	/// This pins the map as data, which is cheap and independent of the release chain. The behavioural
	/// half is <see cref="ReleasedEachScientistTakesItsOwnRoute"/>.
	/// </remarks>
	[Theory]
	[MemberData(nameof(Scientists))]
	public void TheClassHandsEachScientistItsOwnRoute(int npcId, float x, float y, string routeId)
	{
		_ = x;
		_ = y;

		Assert.Equal(routeId, CapturedDrakanScientistAI.Routes[npcId]);
	}

	/// <summary>
	/// <b>And no two of them get the same one.</b> The defect was one destination for three.
	/// </summary>
	[Fact]
	public void NoTwoScientistsShareARoute()
	{
		Assert.Equal(3, CapturedDrakanScientistAI.Routes.Values.Distinct().Count());
	}

	private const int GuardingEye = 219390;

	/// <summary>Releases a scientist the way the encounter does: two guarding eyes die.</summary>
	/// <remarks>
	/// <b>The eyes must exist before the player does.</b> The class attaches its death observers inside a
	/// <c>CompareAndSet</c>-guarded <c>HandleCreatureSee</c>, so it walks its known list exactly once —
	/// and the harness delivers <c>CreatureSee</c> as soon as a player is spawned nearby. A player
	/// created first therefore spends the scientist's single activation on an empty room, and every eye
	/// added afterwards goes unwatched. That cost several attempts, all of them spent at the other end of
	/// the chain: it looked like the deaths were not landing, and the deaths were fine.
	/// </remarks>
	private static Npc Released(BossAiHarness harness, int npcId, float x, float y)
	{
		Npc scientist = harness.Spawn(npcId, x, y, 397.42f);
		Npc[] eyes = { harness.Spawn(GuardingEye, x + 2f, y, 397.42f),
		               harness.Spawn(GuardingEye, x + 3f, y, 397.42f) };
		foreach (Npc eye in eyes)
			BossAiHarness.MakeMutuallyKnown(scientist, eye);

		Player player = harness.SpawnPlayer(x + 4f, y, 397.42f);
		scientist.GetAi().OnCreatureEvent(AiEventType.CREATURE_SEE, player);
		foreach (Npc eye in eyes)
			BossAiHarness.Kill(eye, player);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		return scientist;
	}

	/// <summary>
	/// <b>Released for real, each scientist takes its own route.</b>
	/// </summary>
	/// <remarks>
	/// This is the pin the others cannot be: they attach a route themselves to reach the arrival
	/// behaviour, so none of them can see the class hand out the wrong one. A mutation deleting the whole
	/// route lookup from <c>StartWalk</c> survived two commits until this existed.
	/// </remarks>
	[Theory]
	[MemberData(nameof(Scientists))]
	public void ReleasedEachScientistTakesItsOwnRoute(int npcId, float x, float y, string routeId)
	{
		using BossAiHarness harness = NewHarness();
		Npc scientist = Released(harness, npcId, x, y);

		Assert.Equal(routeId, scientist.GetMoveController().GetWalkerTemplate()?.GetRouteId());
	}
}
