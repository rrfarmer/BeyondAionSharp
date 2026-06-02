using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupLiveDispatchDryRunPlanServiceTests
{
	[Fact]
	public void CreatePlan_EnumeratesEveryRequiredGateWithoutLiveSideEffects()
	{
		var plan = FindGroupLiveDispatchDryRunPlanService.CreatePlan();

		Assert.Equal(FindGroupLiveDispatchDryRunStatus.BlockedNonLivePlan, plan.Status);
		Assert.False(plan.IsReadyForLiveDispatch);
		Assert.False(plan.ShouldInvokeLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.True(plan.HasEveryRequiredGatePlan);
		Assert.Empty(plan.MissingRequiredGatePlans);
		Assert.Contains("Dry run only", plan.BoundaryNote, StringComparison.Ordinal);
		Assert.Contains("CM_FIND_GROUP.runImpl", plan.JavaSource, StringComparison.Ordinal);
		Assert.Equal(plan.Checklist.RequiredLiveDispatchGateKinds, plan.GatePlans.Select(gate => gate.GateKind));
		Assert.All(plan.GatePlans, gate => Assert.True(gate.BlocksLiveWiring));
	}

	[Fact]
	public void CreatePlan_MapsRequiredGatesToExecutorsAndResultSurfaces()
	{
		var plan = FindGroupLiveDispatchDryRunPlanService.CreatePlan();

		AssertGate(
			plan,
			FindGroupLiveDispatchGoNoGoChecklistItemKind.ConnectionBoundaryWiring,
			"ProcessPacketAsync",
			"FindGroupConnectionBoundaryDispatchAdapterPlan",
			"CreateDisabledFindGroupBoundaryPlan");
		AssertGate(
			plan,
			FindGroupLiveDispatchGoNoGoChecklistItemKind.SharedSingletonLifecycle,
			"FindGroupRecruitmentPlanService",
			"FindGroupLifecycleSingletonWiringReadinessReport",
			"live CM_FIND_GROUP does not execute the shared singleton");
		AssertGate(
			plan,
			FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch,
			"FindGroupSideEffectDispatchExecutorService.ExecuteAsync direct packet phase",
			"DirectPackets",
			"order relative to the triggering CM_FIND_GROUP packet");
		AssertGate(
			plan,
			FindGroupLiveDispatchGoNoGoChecklistItemKind.WorldBroadcastDispatch,
			"FindGroupSideEffectDispatchExecutorService.ExecuteAsync world-broadcast phase",
			"WorldBroadcasts",
			"same-race fanout and opposite-race exclusion");
		AssertGate(
			plan,
			FindGroupLiveDispatchGoNoGoChecklistItemKind.ActionTwelveInviteDispatch,
			"FindGroupInstanceApplicationInviteDispatchPlanService.CreateDisabledPlan",
			"FindGroupInstanceApplicationInviteDispatchPlan",
			"live invite request mutation");
		AssertGate(
			plan,
			FindGroupLiveDispatchGoNoGoChecklistItemKind.RuntimeComparison,
			"runtime comparison harness",
			"runtime trace or socket comparison artifact",
			"No encrypted socket");
	}

	[Fact]
	public void CreatePlan_PreservesParsedOnlyActionsAsChecklistNoOpsOutsideLiveSideEffectGates()
	{
		var plan = FindGroupLiveDispatchDryRunPlanService.CreatePlan();

		Assert.DoesNotContain(
			plan.GatePlans,
			gate => gate.GateKind == FindGroupLiveDispatchGoNoGoChecklistItemKind.ParsedOnlyNoRunActions);
		var parsedOnly = Assert.Single(
			plan.Checklist.Items,
			item => item.Kind == FindGroupLiveDispatchGoNoGoChecklistItemKind.ParsedOnlyNoRunActions);
		Assert.Equal(FindGroupLiveDispatchGoNoGoChecklistItemStatus.Ready, parsedOnly.Status);
		Assert.Contains("20 and 25", parsedOnly.Gate, StringComparison.Ordinal);
	}

	private static void AssertGate(
		FindGroupLiveDispatchDryRunPlan plan,
		FindGroupLiveDispatchGoNoGoChecklistItemKind gateKind,
		string expectedExecutor,
		string expectedResultSurface,
		string expectedEvidence)
	{
		var gate = Assert.Single(plan.GatePlans, item => item.GateKind == gateKind);
		Assert.Contains(expectedExecutor, gate.RequiredExecutor, StringComparison.Ordinal);
		Assert.Contains(expectedResultSurface, gate.RequiredResultSurface, StringComparison.Ordinal);
		Assert.Contains(expectedEvidence, gate.ExistingNonLiveEvidence, StringComparison.Ordinal);
		Assert.True(gate.BlocksLiveWiring);
	}
}
