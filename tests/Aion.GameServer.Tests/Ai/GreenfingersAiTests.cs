using Aion.GameServer.Ai.Event;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Walker;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Dalia's three helpers, who left one waypoint before the end of their route.
/// </summary>
/// <remarks>
/// Each helper walks a route and, at the end of it, buffs the boss and despawns. The end is identified by
/// a <c>walkPosition</c> -- 24, 26 and 40, against routes of 25, 27 and 41 steps, so each is the final
/// index and "the end of the route" is unambiguously the intent.
/// <para>
/// The index was read <b>after</b> <c>base.HandleMoveArrived()</c>, which is what runs
/// <c>WalkManager.ChooseNextRouteStep</c> and advances the controller. None of the three routes carries a
/// <c>loop_type</c>, so all default to <c>NORMAL</c> and wrap at the end: arriving at the genuine last
/// step read back as index <b>zero</b> and never matched, and arriving at the second-to-last read back as
/// the last and did. So the helper always fired one waypoint early, and never at the point it names.
/// </para>
/// <para>
/// Found while checking whether <c>GreenfingersAI</c> and <c>ReianBomberAI</c> shared the ordering trap
/// that <c>MuraganAI</c> had just been written around. The bomber reads it in the right order; this did not.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GreenfingersAiTests
{
	private const int DaliaMap = 300250000;

	/// <summary>The first helper, on route 3002500002 -- twenty-five steps, so the last index is 24.</summary>
	private const int Helper = 282176;
	private const string HelperRoute = "3002500002";
	private const int LastStepIndex = 24;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DaliaMap).WithWorldSize(2048).WithWalkerRoutes()
			.WithAi(typeof(GreenfingersAI), typeof(GeneralNpcAI), typeof(AggressiveNpcAI))
			.Build();

	/// <summary>
	/// Starts him walking the way <c>DaliaCharlandsAI</c> does -- walker id on the spawn, then
	/// <c>WalkManager.StartWalking</c>. Setting the template directly is not enough: without the WALKING
	/// state the arrival never reaches <c>ChooseNextRouteStep</c>, and a pin written against that would
	/// find him idle for the wrong reason and pass either way.
	/// </summary>
	private static Npc Walking(BossAiHarness harness)
	{
		Npc helper = harness.Spawn(Helper, 1174.44f, 669.64f, 297.5f);
		helper.GetSpawn()!.SetWalkerId(HelperRoute);
		Aion.GameServer.Ai.Manager.WalkManager.StartWalking((Aion.GameServer.Ai.NpcAI)helper.GetAi());
		return helper;
	}

	private static void ArriveAt(Npc helper, int stepIndex)
	{
		helper.GetMoveController().SetRouteStep(
			helper.GetMoveController().GetWalkerTemplate().GetRouteSteps()[stepIndex]);
		helper.GetAi().OnGeneralEvent(AiEventType.MoveArrived);
	}

	private static int Alive(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == Helper);

	/// <summary>
	/// <b>The route really does have twenty-five steps.</b>
	/// </summary>
	/// <remarks>
	/// Written out rather than read from the walker file, because the whole defect is a disagreement about
	/// which index is the end: a pin that derives the end from the same data cannot see it move.
	/// </remarks>
	[Fact]
	public void TheRouteEndsAtIndexTwentyFour()
	{
		using BossAiHarness harness = NewHarness();
		Npc helper = Walking(harness);

		Assert.Equal(LastStepIndex + 1,
			helper.GetMoveController().GetWalkerTemplate().GetRouteSteps().Count);
	}

	/// <summary>
	/// <b>He walks the whole route.</b> Arriving one short of the end is not the end, and this is the
	/// assertion the old ordering failed: it fired here, a waypoint early, every run.
	/// </summary>
	[Fact]
	public void HeDoesNotLeaveOneWaypointEarly()
	{
		using BossAiHarness harness = NewHarness();
		Npc helper = Walking(harness);

		ArriveAt(helper, LastStepIndex - 1);

		Assert.Equal(1, Alive(harness));
		Assert.False(helper.GetAi().IsInState(Aion.GameServer.Ai.AIState.IDLE),
			"the helper stopped walking a waypoint before the end of his route");
	}

	/// <summary>
	/// <b>And he stops when he actually gets there.</b> With the index read after the base handler this
	/// could never fire at all: the route loops, so the last step advanced to index zero.
	/// </summary>
	[Fact]
	public void HeStopsAtTheEndOfTheRoute()
	{
		using BossAiHarness harness = NewHarness();
		Npc helper = Walking(harness);

		ArriveAt(helper, LastStepIndex);

		Assert.True(helper.GetAi().IsInState(Aion.GameServer.Ai.AIState.IDLE),
			"the helper walked past the end of his route without stopping");
	}
}
