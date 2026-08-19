using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The Muragan escort in Tiamat Stronghold: one who vanished mid-corridor, one who vanished for no
/// reason at all.
/// </summary>
/// <remarks>
/// Each of the three npcs has its own retail pattern. <c>IDTiamat_Murugan1</c> chains six waypoints and
/// despawns at the last; <c>IDTiamat_Murugan2</c> is a single flag-guarded rung that opens a door and
/// nothing else.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class MuraganEscortTests
{
	private const int TiamatStronghold = 300510000;

	/// <summary>Muragan the Loyal, who walks; and the door-opener, who does not.</summary>
	private const int TheLoyal = 800435;
	private const int TheDoorOpener = 800436;

	/// <summary>Where each one stands in the instance's own spawn table.</summary>
	private const float LoyalX = 930.90997f;
	private const float LoyalY = 1316.27f;
	private const float LoyalZ = 401f;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithWalkerRoutes()
			.WithAi(typeof(MuraganAI), typeof(GeneralNpcAI), typeof(AggressiveNpcAI)).Build();

	private static int Alive(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>Muragan the Loyal is still walking at ten seconds.</b>
	/// </summary>
	/// <remarks>
	/// He has ninety-three units to cover, which no npc walk speed does in ten — and ten seconds is
	/// exactly when this class used to delete him, so the escort disappeared part-way down the corridor
	/// every run.
	/// </remarks>
	[Fact]
	public void TheLoyalIsStillWalkingAtTenSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc muragan = harness.Spawn(TheLoyal, LoyalX, LoyalY, LoyalZ);
		Player player = harness.SpawnPlayer(LoyalX + 3f, LoyalY, LoyalZ);
		muragan.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CREATURE_SEE, player);

		harness.Clock.Advance(TimeSpan.FromSeconds(11));

		Assert.Equal(1, Alive(harness, TheLoyal));
	}

	/// <summary>
	/// <b>And the walk is long enough that ten seconds could never have covered it.</b>
	/// </summary>
	/// <remarks>
	/// Stated as a distance rather than a duration because the duration depends on a speed the harness
	/// does not model. Ninety-three units at any npc walk speed is tens of seconds.
	/// </remarks>
	[Fact]
	public void AndTheWalkIsNinetyThreeUnitsLong()
	{
		double dx = MuraganAI.DoorX - LoyalX;
		double dy = MuraganAI.DoorY - LoyalY;
		double dz = MuraganAI.DoorZ - LoyalZ;

		Assert.InRange(Math.Sqrt(dx * dx + dy * dy + dz * dz), 90d, 96d);
	}

	/// <summary>
	/// <b>The door-opener stays where he is.</b>
	/// </summary>
	/// <remarks>
	/// Retail's whole pattern for him is one rung: open the door. This class deleted him in the same
	/// breath, so the npc a group walks past was gone by the time they reached the doorway.
	/// </remarks>
	[Fact]
	public void TheDoorOpenerStaysWhereHeIs()
	{
		using BossAiHarness harness = NewHarness();
		Npc opener = harness.Spawn(TheDoorOpener, 717.45721f, 1314.4929f, 490.3996f);
		Player player = harness.SpawnPlayer(719f, 1314.5f, 490.4f);

		opener.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CREATURE_SEE, player);
		harness.Clock.Advance(TimeSpan.FromMinutes(1));

		Assert.Equal(1, Alive(harness, TheDoorOpener));
	}

	/// <summary>
	/// <b>And a player who never comes near does not open the door.</b>
	/// </summary>
	/// <remarks>
	/// Retail's guard is fifteen metres. Without this the pin above passes for a class that does nothing
	/// at all, which is a different way of being wrong.
	/// </remarks>
	[Fact]
	public void AndAPlayerWhoNeverComesNearDoesNotTripHim()
	{
		using BossAiHarness harness = NewHarness();
		Npc muragan = harness.Spawn(TheLoyal, LoyalX, LoyalY, LoyalZ);
		Player distant = harness.SpawnPlayer(LoyalX + 60f, LoyalY, LoyalZ);

		muragan.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CREATURE_SEE, distant);
		harness.Clock.Advance(TimeSpan.FromMinutes(5));

		// Untripped, so the backstop never armed and he is still standing at his spawn point.
		Assert.Equal(1, Alive(harness, TheLoyal));
		Assert.Equal(LoyalX, muragan.GetX(), 1);
	}

	/// <summary>
	/// <b>Without the route he still goes when the single move ends.</b>
	/// </summary>
	/// <remarks>
	/// This was "arrival despawns him", which held only while the whole walk was one straight move to the
	/// door. He now walks retail's six points, so arriving is something that happens five times before he
	/// should go anywhere, and that assertion moved to <see cref="HeGoesAtTheSixthWaypoint"/>.
	/// <para>
	/// What is left here is the fallback, which is worth its own pin: on a build whose walker data does not
	/// carry the route, <c>StartRoute</c> declines and he is sent at the door in a line as before. The
	/// harness reproduces that exactly by not loading walker routes, and without this the fallback path is
	/// unpinned and only the two-minute backstop -- ours, not retail's -- would be holding him.
	/// </para>
	/// </remarks>
	[Fact]
	public void WithoutTheRouteHeStillGoesWhenTheMoveEnds()
	{
		using BossAiHarness harness = BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(MuraganAI), typeof(GeneralNpcAI), typeof(AggressiveNpcAI)).Build();
		Npc muragan = harness.Spawn(TheLoyal, LoyalX, LoyalY, LoyalZ);
		Player player = harness.SpawnPlayer(LoyalX + 3f, LoyalY, LoyalZ);
		muragan.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CREATURE_SEE, player);
		Assert.Null(muragan.GetMoveController().GetWalkerTemplate());
		Assert.Equal(1, Alive(harness, TheLoyal));

		muragan.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.MOVE_ARRIVED);

		Assert.Equal(0, Alive(harness, TheLoyal));
	}

	/// <summary>
	/// <b>But the door-opener does not, even though he shares the class.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>despawn_self</c> is on Muragan the Loyal's route alone. Without this the despawn
	/// could be keyed on nothing at all and still satisfy the pin above.
	/// </remarks>
	[Fact]
	public void ButTheDoorOpenerDoesNot()
	{
		using BossAiHarness harness = NewHarness();
		Npc opener = harness.Spawn(TheDoorOpener, 717.45721f, 1314.4929f, 490.3996f);

		opener.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.MOVE_ARRIVED);

		Assert.Equal(1, Alive(harness, TheDoorOpener));
	}

	/// <summary>
	/// <b>And the door he walks to is retail's own waypoint.</b>
	/// </summary>
	/// <remarks>
	/// The sixth point of <c>Path_IDTiamat_Murugan_1</c>, from the client's
	/// <c>Map/Worlds/idtiamat_1/world_N_WayPoint_1.xml</c> and now in
	/// <c>npc_walker/300510000_Tiamat_Stronghold.xml</c>. It used to be (838, 1317, 396) — close enough
	/// to look right, and nearly two metres out in both y and z.
	/// <para>
	/// Written out rather than compared against the constants, because a pin that reads the thing it is
	/// pinning tests nothing; that mistake has been made twice in this suite already.
	/// </para>
	/// </remarks>
	[Fact]
	public void AndTheDoorHeWalksToIsRetailsOwnWaypoint()
	{
		Assert.Equal(838.003113f, MuraganAI.DoorX, 4);
		Assert.Equal(1319.114136f, MuraganAI.DoorY, 4);
		Assert.Equal(397.737579f, MuraganAI.DoorZ, 4);
	}

	/// <summary>Triggers the escort by putting a player inside his fifteen-metre radius.</summary>
	private static Npc Triggered(BossAiHarness harness)
	{
		Npc muragan = harness.Spawn(TheLoyal, LoyalX, LoyalY, LoyalZ);
		Player player = harness.SpawnPlayer(LoyalX + 3f, LoyalY, LoyalZ);
		muragan.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CREATURE_SEE, player);
		return muragan;
	}

	/// <summary>Puts him at one point of the route, as arriving there would.</summary>
	private static void ArriveAt(Npc muragan, int stepIndex)
	{
		muragan.GetMoveController().SetRouteStep(
			muragan.GetMoveController().GetWalkerTemplate().GetRouteSteps()[stepIndex]);
		muragan.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.MoveArrived);
	}

	/// <summary>
	/// <b>He is put on retail's own route, not sent at the door in a straight line.</b>
	/// </summary>
	/// <remarks>
	/// The route id is written out rather than read from <c>MuraganAI.RouteId</c>: a pin that takes its
	/// expectation from the constant it is pinning passes whatever that constant becomes, and that mistake
	/// has been made three times in this suite.
	/// <para>
	/// He cannot get the route from a <c>walker_id</c> on his spawn -- that would have him patrolling from
	/// the moment the instance opens, where retail has him stand still until somebody comes near -- so the
	/// route is attached at the trigger instead.
	/// </para>
	/// </remarks>
	[Fact]
	public void HeWalksRetailsRouteRatherThanAStraightLine()
	{
		using BossAiHarness harness = NewHarness();
		Npc muragan = Triggered(harness);

		Assert.NotNull(muragan.GetMoveController().GetWalkerTemplate());
		Assert.Equal("3005100001", muragan.GetMoveController().GetWalkerTemplate().GetRouteId());
	}

	/// <summary>
	/// <b>He does not vanish at the waypoints along the way.</b> Retail despawns him at the sixth point and
	/// nowhere earlier, and an escort that disappears at the second corner is the bug this route was
	/// imported to fix, in a new form.
	/// </summary>
	[Fact]
	public void HeSurvivesTheWaypointsBeforeTheSixth()
	{
		using BossAiHarness harness = NewHarness();
		Npc muragan = Triggered(harness);

		for (int step = 1; step <= 4; step++)
		{
			ArriveAt(muragan, step);
			Assert.Equal(1, Alive(harness, TheLoyal));
		}
	}

	/// <summary>
	/// <b>And he goes at the sixth.</b> Retail's <c>despawn_self</c>, on the last point he actually walks;
	/// the route carries eleven and he never sees points seven to eleven.
	/// </summary>
	[Fact]
	public void HeGoesAtTheSixthWaypoint()
	{
		using BossAiHarness harness = NewHarness();
		Npc muragan = Triggered(harness);

		ArriveAt(muragan, 5);

		Assert.Equal(0, Alive(harness, TheLoyal));
	}
}
