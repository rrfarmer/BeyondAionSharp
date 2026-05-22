using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcWalkerRouteStepServiceTests
{
	[Fact]
	public void FindClosestRouteStep_UsesJavaWalkManagerDistance()
	{
		var service = new WorldNpcWalkerRouteStepService();
		var plan = CreatePlan(
			routeSteps:
			[
				new WalkerRouteStepSummary(0, 0, 0, 0, 0, false),
				new WalkerRouteStepSummary(10, 0, 0, 0, 1, false),
				new WalkerRouteStepSummary(4, 0, 0, 0, 2, true),
			]);

		var closest = service.FindClosestRouteStep(new WorldPosition(210010000, 3.5f, 0, 0, 0), plan);

		Assert.NotNull(closest);
		Assert.Equal(2, closest.StepIndex);
		Assert.Equal(plan.RouteSteps[2], closest.Step);
	}

	[Fact]
	public void GetNextStepIndex_WrapsLastStepToRouteStart()
	{
		var service = new WorldNpcWalkerRouteStepService();
		var plan = CreatePlan();

		Assert.Equal(1, service.GetNextStepIndex(plan, currentStepIndex: 0));
		Assert.Equal(0, service.GetNextStepIndex(plan, currentStepIndex: 1));
	}

	[Fact]
	public void CreateWalkerTarget_UsesStepCoordinatesAndStopsOnLoopNoneLastStep()
	{
		var service = new WorldNpcWalkerRouteStepService();
		var walker = new WorldNpcWalkerSpawnCandidate(1, 203000, "route-a", "", 0, 0, 0, 0);
		var plan = CreatePlan(loopType: "NONE");

		var target = service.CreateWalkerTarget(walker, plan, stepIndex: 1);

		Assert.Equal(1, target.ObjectId);
		Assert.Equal(1, target.StepIndex);
		Assert.Equal(10, target.X);
		Assert.Equal(0, target.Y);
		Assert.Equal(5, target.Z);
		Assert.Equal(200, target.RestTime);
		Assert.True(target.IsLastStep);
		Assert.True(target.ShouldStop);
	}

	[Fact]
	public void CreateFormationTargets_ProjectsMemberShiftsFromCurrentToNextStep()
	{
		var service = new WorldNpcWalkerRouteStepService();
		var plan = CreatePlan(
			routeSteps:
			[
				new WalkerRouteStepSummary(0, 0, 7, 100, 0, false),
				new WalkerRouteStepSummary(10, 0, 99, 200, 1, true),
			]);
		var formation = new WorldNpcWalkerFormationResult(
			WorldNpcWalkerFormationStatus.Ready,
			"route-a",
			string.Empty,
			[
				new WorldNpcWalkerFormationMember(1, 203000, 1, 0, 0, -1, 0),
				new WorldNpcWalkerFormationMember(2, 203000, 2, 0, 0, 1, 0),
			]);

		var targets = service.CreateFormationTargets(formation, plan, currentStepIndex: 0, nextStepIndex: 1);

		Assert.Equal([1, 2], targets.Select(target => target.ObjectId).ToArray());
		AssertRouteStepTarget(targets[0], x: 0, y: 1, z: 7, shouldStop: false);
		AssertRouteStepTarget(targets[1], x: 0, y: -1, z: 7, shouldStop: false);
		Assert.All(targets, target =>
		{
			Assert.Equal(1, target.StepIndex);
			Assert.Equal(200, target.RestTime);
			Assert.True(target.IsLastStep);
		});
	}

	private static WorldNpcWalkerRoutePlan CreatePlan(
		string loopType = "NORMAL",
		IReadOnlyList<WalkerRouteStepSummary>? routeSteps = null)
	{
		return WorldNpcWalkerRoutePlan.Ready(
			"route-a",
			string.Empty,
			1,
			"POINT",
			loopType,
			[],
			routeSteps ??
			[
				new WalkerRouteStepSummary(0, 0, 0, 100, 0, false),
				new WalkerRouteStepSummary(10, 0, 5, 200, 1, true),
			]);
	}

	private static void AssertRouteStepTarget(
		WorldNpcWalkerRouteStepTarget target,
		float x,
		float y,
		float z,
		bool shouldStop)
	{
		Assert.Equal(x, target.X, precision: 4);
		Assert.Equal(y, target.Y, precision: 4);
		Assert.Equal(z, target.Z, precision: 4);
		Assert.Equal(shouldStop, target.ShouldStop);
	}
}
