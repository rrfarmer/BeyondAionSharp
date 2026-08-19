using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Brass-Eye Grogget's patrol, which the class had a "need snif" comment standing in for.
/// </summary>
/// <remarks>
/// Retail walks him a twelve-point route and hangs two ladders off it. Waypoint 4 drops one stigma stone
/// per lap at four absolute coordinates, guarded by <c>unset_flag_var</c> on <c>ZETA_4</c> down to
/// <c>ZETA_1</c>; waypoint 10 brings one wave per lap at his own point, guarded by <c>set_flag_var</c> on
/// <c>DELTA_1</c> to <c>DELTA_3</c> with an unguarded fourth rung.
/// <para>
/// No sniffing was needed: he already walks retail's route. Spawn <c>walker_id</c>
/// <c>055B73AA897B0E07D287848D3AD6EBCABB7DD93D</c> is twelve steps and is the client's
/// <c>IDShip_mobpath_ShulackCaptainNmd_46_Ah</c>, first point exactly on his spawn.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class BrassEyeGroggetAiTests
{
	private const int SteelRake = 300100000;

	private const int Grogget = 215081;
	private const string CaptainRoute = "055B73AA897B0E07D287848D3AD6EBCABB7DD93D";

	/// <summary>Retail's ids and indices, written out rather than read from the class under test.</summary>
	private static readonly int[] Stones = { 281191, 281192, 281193, 281194 };
	private static readonly int[] Waves = { 281198, 281199, 281200, 281201 };
	private const int StonePoint = 4;
	private const int WavePoint = 10;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(SteelRake).WithWorldSize(2048).WithWalkerRoutes()
			.WithAi(typeof(BrassEyeGroggetAI), typeof(SummonerAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI), typeof(NoActionAI))
			.Build();

	private static Npc Walking(BossAiHarness harness)
	{
		Npc grogget = harness.Spawn(Grogget, 427.45f, 509.88f, 1075.3801f);
		grogget.GetSpawn().SetWalkerId(CaptainRoute);
		Aion.GameServer.Ai.Manager.WalkManager.StartWalking((Aion.GameServer.Ai.NpcAI)grogget.GetAi());
		return grogget;
	}

	private static void ArriveAt(Npc grogget, int stepIndex)
	{
		grogget.GetMoveController().SetRouteStep(
			grogget.GetMoveController().GetWalkerTemplate().GetRouteSteps()[stepIndex]);
		grogget.GetAi().OnGeneralEvent(AiEventType.MoveArrived);
	}

	/// <summary>
	/// What of <paramref name="ids"/> is alive, <b>sorted</b>. <c>LiveNpcs()</c> does not promise an order,
	/// and comparing it as a sequence made this file fail about one run in three -- an intermittent that
	/// cost two turns to attribute because it only ever showed up in whole-solution runs.
	/// </summary>
	private static List<int> Spawned(BossAiHarness harness, int[] ids) =>
		harness.LiveNpcs().Select(n => n.GetNpcId()).Where(ids.Contains).OrderBy(i => i).ToList();

	/// <summary>
	/// <b>Four laps bring the four stigma stones, one each and in order.</b> These are the "4 towers in the
	/// room center" the class had a todo for.
	/// </summary>
	[Fact]
	public void EachLapBringsTheNextStigmaStone()
	{
		using BossAiHarness harness = NewHarness();
		Npc grogget = Walking(harness);

		for (int lap = 1; lap <= 4; lap++)
		{
			ArriveAt(grogget, StonePoint);
			Assert.Equal(Stones.Take(lap).OrderBy(i => i), Spawned(harness, Stones));
		}
	}

	/// <summary>
	/// <b>And a fifth lap brings no fifth stone.</b> Retail's guard is test-and-unset, so each rung fires
	/// exactly once; a stone per lap forever would fill the room.
	/// </summary>
	[Fact]
	public void AFifthLapBringsNoFifthStone()
	{
		using BossAiHarness harness = NewHarness();
		Npc grogget = Walking(harness);

		for (int lap = 1; lap <= 6; lap++)
			ArriveAt(grogget, StonePoint);

		Assert.Equal(4, Spawned(harness, Stones).Count);
	}

	/// <summary>
	/// <b>The stones stand where retail puts them, not where he happens to be.</b> They are
	/// <c>SPAWN_LOCATION_ABSOLUTE</c>; spawning them at his feet would put all four on his route instead of
	/// around the room.
	/// </summary>
	[Fact]
	public void TheStonesStandAtTheirOwnCoordinates()
	{
		using BossAiHarness harness = NewHarness();
		Npc grogget = Walking(harness);

		ArriveAt(grogget, StonePoint);

		Npc stone = harness.LiveNpcs().Single(n => n.GetNpcId() == Stones[0]);
		Assert.Equal(397.43f, stone.GetX(), 2);
		Assert.Equal(504.22f, stone.GetY(), 2);
	}

	/// <summary>
	/// <b>The first three laps bring three different waves, and every lap after brings the fourth.</b>
	/// Retail guards the first three rungs and leaves the fourth unguarded, which is what makes the tail
	/// repeat rather than stop.
	/// </summary>
	[Fact]
	public void TheWavesRunOutAndThenRepeatTheLast()
	{
		using BossAiHarness harness = NewHarness();
		Npc grogget = Walking(harness);

		for (int lap = 1; lap <= 3; lap++)
		{
			ArriveAt(grogget, WavePoint);
			Assert.Equal(Waves.Take(lap).OrderBy(i => i), Spawned(harness, Waves));
		}

		ArriveAt(grogget, WavePoint);
		ArriveAt(grogget, WavePoint);

		// Five laps, five waves: three distinct ones and then the unguarded fourth rung twice.
		List<int> all = Spawned(harness, Waves);
		Assert.Equal(5, all.Count);
		Assert.Equal(2, all.Count(id => id == Waves[3]));
	}

	/// <summary>
	/// <b>Ordinary waypoints bring nothing.</b> He has twelve points and spawns at two of them.
	/// </summary>
	[Fact]
	public void OrdinaryWaypointsBringNothing()
	{
		using BossAiHarness harness = NewHarness();
		Npc grogget = Walking(harness);

		foreach (int point in new[] { 0, 1, 2, 3, 5, 6, 7, 8, 9, 11 })
			ArriveAt(grogget, point);

		Assert.Empty(Spawned(harness, Stones));
		Assert.Empty(Spawned(harness, Waves));
	}

	/// <summary>
	/// <b>It all goes when he is pulled.</b> Retail's <c>despawn_at_attack_state=TRUE</c>, on all ten of
	/// his spawn rungs. They carry <c>live_time=0</c>, so without this the room stays full for the life of
	/// the instance.
	/// </summary>
	[Fact]
	public void ThePatrolSpawnsGoWhenHeIsPulled()
	{
		using BossAiHarness harness = NewHarness();
		Npc grogget = Walking(harness);

		ArriveAt(grogget, StonePoint);
		ArriveAt(grogget, WavePoint);
		Assert.NotEmpty(Spawned(harness, Stones));
		Assert.NotEmpty(Spawned(harness, Waves));

		Player puller = harness.SpawnPlayer(431f, 509f, 1075f);
		harness.Engage(grogget, puller);

		Assert.Empty(Spawned(harness, Stones));
		Assert.Empty(Spawned(harness, Waves));
	}
}
