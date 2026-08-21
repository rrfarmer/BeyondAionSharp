using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Engineer Lahulahu's nozzle wave, which this port did not have at all.
/// </summary>
/// <remarks>
/// Retail arms <c>BTIMERI_INDEX_9</c> at 3500ms from <c>on_arrived_at_waypoint</c> at indices 2, 6, 12
/// and 16 of his route. When it fires, a probability cascade picks one of nine
/// <c>BIDShulack_EngineerSum*</c> npcs by health band, and only each band's first rung re-arms, at 6500.
/// <para>
/// It had been recorded twice as blocked -- on waypoint arrival, and on route data. Neither was true.
/// <c>MoveArrived</c> fires at every route step, and his route was already in our own spawn table: walker
/// <c>02692E8AA2C2793A7801E13C574871619504EEF9</c>, twenty-one steps matching the client's
/// <c>IDShulackShip_1F_Engineer_MobPath</c>, first point five millimetres from his spawn.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class EngineerLahulahuAiTests
{
	private const int SteelRake = 300100000;

	private const int Lahulahu = 215080;
	private const string EngineerRoute = "02692E8AA2C2793A7801E13C574871619504EEF9";

	/// <summary>Retail's summoning waypoints, written out rather than read from the class under test.</summary>
	private static readonly int[] SummonPoints = { 2, 6, 12, 16 };

	/// <summary>All nine BIDShulack_EngineerSum* npcs, resolved from the pattern's devnames.</summary>
	private static readonly int[] Nozzles =
		{ 281103, 281104, 281105, 281106, 281107, 281293, 281294, 281295, 281351 };

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(SteelRake).WithWorldSize(2048).WithWalkerRoutes()
			.WithAi(typeof(EngineerLahulahuAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static Npc Walking(BossAiHarness harness)
	{
		Npc engineer = harness.Spawn(Lahulahu, 695.76f, 508.37f, 867.3649f);
		engineer.GetSpawn()!.SetWalkerId(EngineerRoute);
		Aion.GameServer.Ai.Manager.WalkManager.StartWalking((Aion.GameServer.Ai.NpcAI)engineer.GetAi());
		return engineer;
	}

	private static void ArriveAt(Npc engineer, int stepIndex)
	{
		engineer.GetMoveController().SetRouteStep(
			engineer.GetMoveController().GetWalkerTemplate().GetRouteSteps()[stepIndex]);
		engineer.GetAi().OnGeneralEvent(AiEventType.MoveArrived);
	}

	private static int NozzleCount(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => Nozzles.Contains(n.GetNpcId()));

	/// <summary>
	/// <b>Each of the four summoning waypoints brings a nozzle.</b>
	/// </summary>
	[Fact]
	public void EachSummoningWaypointBringsANozzle()
	{
		foreach (int point in SummonPoints)
		{
			using BossAiHarness harness = NewHarness();
			Npc engineer = Walking(harness);
			BossAiHarness.SetHpPercent(engineer, 90);

			ArriveAt(engineer, point);
			harness.Clock.Advance(TimeSpan.FromSeconds(4));

			Assert.True(NozzleCount(harness) >= 1,
				$"waypoint {point} summoned nothing; retail arms the wave at 2, 6, 12 and 16");
		}
	}

	/// <summary>
	/// <b>And the other waypoints bring nothing.</b> He has twenty-one points and summons at four of them;
	/// a wave at every step would be a different encounter.
	/// </summary>
	[Fact]
	public void OrdinaryWaypointsSummonNothing()
	{
		foreach (int point in new[] { 0, 3, 7, 13, 17, 20 })
		{
			using BossAiHarness harness = NewHarness();
			Npc engineer = Walking(harness);
			BossAiHarness.SetHpPercent(engineer, 90);

			ArriveAt(engineer, point);
			harness.Clock.Advance(TimeSpan.FromSeconds(4));

			Assert.Equal(0, NozzleCount(harness));
		}
	}

	/// <summary>
	/// <b>The nozzle takes three and a half seconds to arrive.</b> Retail's <c>add_battle_timer</c> delay,
	/// not an immediate spawn.
	/// </summary>
	[Fact]
	public void TheNozzleTakesThreeAndAHalfSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc engineer = Walking(harness);
		BossAiHarness.SetHpPercent(engineer, 90);

		ArriveAt(engineer, 2);
		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.Equal(0, NozzleCount(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.True(NozzleCount(harness) >= 1, "the nozzle never arrived");
	}

	/// <summary>
	/// <b>The health band decides which nozzle.</b> Above seventy-five only D, E and H can appear; below
	/// twenty-five only C, F, G, H and I can. Getting this wrong puts the wrong adds in the fight at the
	/// wrong time, which is invisible in a count.
	/// </summary>
	[Fact]
	public void TheHealthBandDecidesWhichNozzle()
	{
		int[] top = { 281106, 281107, 281295 };
		int[] bottom = { 281105, 281293, 281294, 281295, 281351 };

		using (BossAiHarness harness = NewHarness())
		{
			Npc engineer = Walking(harness);
			BossAiHarness.SetHpPercent(engineer, 90);
			for (int i = 0; i < 12; i++)
			{
				ArriveAt(engineer, 2);
				harness.Clock.Advance(TimeSpan.FromSeconds(4));
			}
			Assert.Contains(harness.LiveNpcs(), n => Nozzles.Contains(n.GetNpcId()));
			Assert.All(harness.LiveNpcs().Where(n => Nozzles.Contains(n.GetNpcId())),
				n => Assert.Contains(n.GetNpcId(), top));
		}

		using (BossAiHarness harness = NewHarness())
		{
			Npc engineer = Walking(harness);
			BossAiHarness.SetExactPercent(engineer, 20);
			for (int i = 0; i < 12; i++)
			{
				ArriveAt(engineer, 2);
				harness.Clock.Advance(TimeSpan.FromSeconds(4));
			}
			Assert.Contains(harness.LiveNpcs(), n => Nozzles.Contains(n.GetNpcId()));
			Assert.All(harness.LiveNpcs().Where(n => Nozzles.Contains(n.GetNpcId())),
				n => Assert.Contains(n.GetNpcId(), bottom));
		}
	}

	/// <summary>
	/// <b>Nothing is summoned on a band boundary.</b>
	/// </summary>
	/// <remarks>
	/// Retail's bands are <c>is_hp_lower_than 25</c> and <c>is_hp_in_boundary</c> 26-50, 51-75 and 75-100,
	/// exclusive at both ends, so exactly 25, 26, 50, 51 and 75 fall through every one of them and the
	/// timer fires against nothing. This pin exists because the hole looks like a bug to anyone reading
	/// the code, and it is not one -- it is what the pattern says, and a "tidied" set of bands would be a
	/// silent divergence.
	/// </remarks>
	[Fact]
	public void NothingIsSummonedOnABandBoundary()
	{
		foreach (int percent in new[] { 25, 26, 50, 51, 75 })
		{
			using BossAiHarness harness = NewHarness();
			Npc engineer = Walking(harness);
			BossAiHarness.SetExactPercent(engineer, percent);

			ArriveAt(engineer, 2);
			harness.Clock.Advance(TimeSpan.FromSeconds(4));

			Assert.Equal(0, NozzleCount(harness));
		}
	}

	/// <summary>
	/// <b>The nozzles go when he is pulled.</b> Retail's <c>despawn_at_attack_state=TRUE</c>, on every
	/// summon rung. Without it, <c>live_time=0</c> would leave a patrol's worth of adds standing for the
	/// life of the instance and waiting in the room for the group.
	/// </summary>
	[Fact]
	public void TheNozzlesGoWhenHeIsPulled()
	{
		using BossAiHarness harness = NewHarness();
		Npc engineer = Walking(harness);
		BossAiHarness.SetHpPercent(engineer, 90);

		ArriveAt(engineer, 2);
		harness.Clock.Advance(TimeSpan.FromSeconds(4));
		ArriveAt(engineer, 6);
		harness.Clock.Advance(TimeSpan.FromSeconds(4));
		Assert.True(NozzleCount(harness) >= 1, "no nozzles to clear, so this pin would prove nothing");

		Player puller = harness.SpawnPlayer(699f, 508f, 867f);
		harness.Engage(engineer, puller);

		Assert.Equal(0, NozzleCount(harness));
	}
}
