using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Chief Gunner Koakoa's walk along the gun deck, which this port did not have.
/// </summary>
/// <remarks>
/// Retail paces him seven points out along the deck and back, and at point 3 -- the far end -- fires a
/// six-rung cascade: 17% A, 33% B, 50% C, 67% D, 83% E, and an unguarded last rung that is E again. One
/// npc at his own feet with <c>live_time=6</c>.
/// <para>
/// <b>Unlike the three encounters before him, his route was not already in our data.</b> It came from the
/// client: <c>IDShip_Mobpath_ShulackRaAtilleryChKnmd_45_Ah</c>, whose name is his own devname and whose
/// first point is 0.35m from his spawn. It is now <c>route_id="3001000001"</c>, bound to his spot.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ChiefGunnerKoakoaAiTests
{
	private const int SteelRake = 300100000;

	private const int Koakoa = 215070;

	/// <summary>Retail's index and ids, written out rather than read from the class under test.</summary>
	private const int GunPoint = 3;
	private static readonly int[] Muzzles = { 281220, 281221, 281222, 281223, 281296 };
	private const string Route = "3001000001";

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(SteelRake).WithWorldSize(2048).WithWalkerRoutes()
			.WithAi(typeof(ChiefGunnerKoakoaAI), typeof(SummonerAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// The walker id is set here rather than coming from the spawn file. <b>The harness synthesises spawn
	/// templates</b>, so the <c>walker_id</c> added to <c>300100000_Steel Rake.xml</c> does not reach it,
	/// and the binding in that file is therefore <b>not</b> pinned by anything below -- only the route data
	/// and the behaviour hanging off it are. That gap is recorded rather than papered over.
	/// </summary>
	private static Npc Walking(BossAiHarness harness)
	{
		Npc gunner = harness.Spawn(Koakoa, 755.777f, 509.02f, 1012.3f);
		gunner.GetSpawn()!.SetWalkerId(Route);
		Aion.GameServer.Ai.Manager.WalkManager.StartWalking((Aion.GameServer.Ai.NpcAI)gunner.GetAi());
		return gunner;
	}

	private static void ArriveAt(Npc gunner, int stepIndex)
	{
		gunner.GetMoveController().SetRouteStep(
			gunner.GetMoveController().GetWalkerTemplate().GetRouteSteps()[stepIndex]);
		gunner.GetAi().OnGeneralEvent(AiEventType.MoveArrived);
	}

	private static int MuzzleCount(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => Muzzles.Contains(n.GetNpcId()));

	/// <summary>
	/// <b>The imported route is seven points long.</b> He had no route at all before this, so nothing
	/// downstream could have worked. The count is written out because a pin that derives it from the same
	/// walker file cannot see the file change.
	/// </summary>
	[Fact]
	public void HeWalksTheGunDeck()
	{
		using BossAiHarness harness = NewHarness();
		Npc gunner = Walking(harness);

		Assert.NotNull(gunner.GetMoveController().GetWalkerTemplate());
		Assert.Equal("3001000001", gunner.GetMoveController().GetWalkerTemplate().GetRouteId());
		Assert.Equal(7, gunner.GetMoveController().GetWalkerTemplate().GetRouteSteps().Count);
	}

	/// <summary>
	/// <b>Reaching the far end fires a gun.</b> Exactly one, whichever rung of the cascade wins.
	/// </summary>
	[Fact]
	public void TheFarEndOfTheDeckFiresAGun()
	{
		using BossAiHarness harness = NewHarness();
		Npc gunner = Walking(harness);

		ArriveAt(gunner, GunPoint);

		Assert.Equal(1, MuzzleCount(harness));
	}

	/// <summary>
	/// <b>The other six points fire nothing.</b> He walks out and back past all of them.
	/// </summary>
	[Fact]
	public void TheOtherPointsFireNothing()
	{
		using BossAiHarness harness = NewHarness();
		Npc gunner = Walking(harness);

		foreach (int point in new[] { 0, 1, 2, 4, 5, 6 })
			ArriveAt(gunner, point);

		Assert.Equal(0, MuzzleCount(harness));
	}

	/// <summary>
	/// <b>The cascade reaches all five guns.</b> A single-outcome roll would be invisible in a count, and
	/// the five ids are the whole point of the ladder.
	/// </summary>
	[Fact]
	public void TheCascadeReachesAllFiveGuns()
	{
		using BossAiHarness harness = NewHarness();
		Npc gunner = Walking(harness);
		HashSet<int> seen = new HashSet<int>();

		for (int i = 0; i < 200; i++)
		{
			ArriveAt(gunner, GunPoint);
			foreach (Npc n in harness.LiveNpcs())
			{
				if (Muzzles.Contains(n.GetNpcId()))
					seen.Add(n.GetNpcId());
			}
		}

		Assert.Equal(Muzzles.OrderBy(i => i), seen.OrderBy(i => i));
	}

	/// <summary>
	/// <b>The muzzle lasts six seconds.</b> Retail's <c>live_time</c>, and the reason five of these in a
	/// row do not fill the deck: they are the guns going off, not adds.
	/// </summary>
	[Fact]
	public void TheMuzzleLastsSixSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc gunner = Walking(harness);

		ArriveAt(gunner, GunPoint);
		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		Assert.Equal(1, MuzzleCount(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, MuzzleCount(harness));
	}
}
