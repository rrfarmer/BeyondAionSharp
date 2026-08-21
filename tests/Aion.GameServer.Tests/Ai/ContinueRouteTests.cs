using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Xunit;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// <b>Retail's <c>goto_next_waypoint</c> on an npc that is standing still.</b>
/// </summary>
/// <remarks>
/// The action was a deliberate no-op, and the reasoning for that was right about the case it
/// considered: an npc already walking advances its own route on arrival, so a rung that advanced it
/// again would make the patrol visit every other point. The <c>WALKING</c> guard keeps that true.
/// <para>
/// What it missed is the npc that is <i>not</i> walking. <c>BIDF5_R2_Runner</c> is the clearest
/// example — empty <c>on_wake_up</c>, a sighting rung that shows <c>STR_MSG_IDF5_R2_RUNNER_START</c>
/// and calls <c>goto_next_waypoint</c>, and an <c>on_arrived_at_waypoint</c> that despawns it at the
/// last point. The whole race is that one element, and against a no-op the runner never left the line.
/// </para>
/// <para>
/// <b>Those nine runners cannot be pinned here: this port has no spawns and no routes for them</b> —
/// they exist in <c>npc_templates.xml</c> and nowhere else. So the mechanism is pinned on an npc this
/// server does place with a route, and the encounter is recorded as unverifiable rather than claimed.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public class ContinueRouteTests
{
	/// <summary>Gelkmaros, where the routed npc below is spawned.</summary>
	private const int Gelkmaros = 220070000;

	/// <summary>
	/// A path walker this port spawns with a route, and one of the 158 npcs carrying a
	/// <c>goto_next_waypoint</c> rung.
	/// </summary>
	private const int RoutedNpc = 216433;

	/// <summary>The route <c>220070000_Gelkmaros.xml</c> spawns <see cref="RoutedNpc"/> on.</summary>
	private const string GelkmarosRoute = "6E7066D53123B8865CFD1033AEF626EA27E7EB62";

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Gelkmaros).WithWorldSize(4096).WithWalkerRoutes()
			.WithAi(typeof(BattleCycleAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>A stopped path walker is sent back down its route.</b> This is the case the no-op silently
	/// dropped: retail says "go to your next waypoint" and the npc was standing still.
	/// </summary>
	[Fact]
	public void AStoppedWalkerIsSentBackDownItsRoute()
	{
		using BossAiHarness harness = NewHarness();
		Npc walker = harness.Spawn(RoutedNpc, 300f, 300f, 200f);
		// The harness spawns without a walker id; production reads it from the spawn row. This is the
		// route 220070000_Gelkmaros.xml really gives this npc.
		walker.GetSpawn().SetWalkerId(GelkmarosRoute);
		PatternAi ai = Assert.IsAssignableFrom<PatternAi>(walker.GetAi());
		ai.SetStateIfNot(AIState.IDLE);
		Assert.False(ai.IsInState(AIState.WALKING));

		ai.ContinueRoute();

		Assert.True(ai.IsInState(AIState.WALKING));
	}

	/// <summary>
	/// <b>An npc with no route of its own is left where it is.</b> The element is an instruction about
	/// a route; without one there is nothing to instruct, and starting a wander here would invent
	/// movement retail never asked for.
	/// </summary>
	[Fact]
	public void AnNpcWithNoRouteIsLeftAlone()
	{
		using BossAiHarness harness = NewHarness();
		Npc rooted = harness.Spawn(216435, 300f, 300f, 200f);
		PatternAi ai = Assert.IsAssignableFrom<PatternAi>(rooted.GetAi());
		ai.SetStateIfNot(AIState.IDLE);
		Assert.Null(rooted.GetSpawn().GetWalkerId());

		ai.ContinueRoute();

		Assert.False(ai.IsInState(AIState.WALKING));
	}
}
