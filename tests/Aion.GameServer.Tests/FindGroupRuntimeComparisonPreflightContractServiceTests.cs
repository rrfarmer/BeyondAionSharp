using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupRuntimeComparisonPreflightContractServiceTests
{
	[Fact]
	public void Create_KeepsRuntimeComparisonBlockedAndNonLive()
	{
		var contract = FindGroupRuntimeComparisonPreflightContractService.Create();

		Assert.Equal(FindGroupRuntimeComparisonPreflightStatus.BlockedPendingLiveDispatchAndTraceHarness, contract.Status);
		Assert.False(contract.IsReadyForRuntimeComparison);
		Assert.False(contract.ShouldInvokeLiveSideEffects);
		Assert.False(contract.IsCmFindGroupBoundaryWired);
		Assert.True(contract.RequiresJavaRuntimeTrace);
		Assert.True(contract.RequiresCSharpRuntimeTrace);
		Assert.True(contract.RequiresEncryptedSocketCapture);
		Assert.Contains("Preflight only", contract.BoundaryNote, StringComparison.Ordinal);
		Assert.Contains("CM_FIND_GROUP.readImpl/runImpl", contract.JavaSource, StringComparison.Ordinal);
		Assert.Contains("GameServerConnection.ProcessPacketAsync", contract.CSharpSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_RequiresTraceFieldsForPayloadStatePacketsFanoutAndSocketFrames()
	{
		var contract = FindGroupRuntimeComparisonPreflightContractService.Create();

		Assert.Equal(
			[
				FindGroupRuntimeComparisonTraceFieldKind.ClientAction,
				FindGroupRuntimeComparisonTraceFieldKind.ActivePlayer,
				FindGroupRuntimeComparisonTraceFieldKind.ParsedPayload,
				FindGroupRuntimeComparisonTraceFieldKind.SingletonStateBeforeAfter,
				FindGroupRuntimeComparisonTraceFieldKind.DirectPackets,
				FindGroupRuntimeComparisonTraceFieldKind.WorldBroadcasts,
				FindGroupRuntimeComparisonTraceFieldKind.InviteRequests,
				FindGroupRuntimeComparisonTraceFieldKind.NoSideEffectBranches,
				FindGroupRuntimeComparisonTraceFieldKind.EncryptedSocketFrames,
			],
			contract.RequiredTraceFields.Select(field => field.Kind));
		Assert.Contains(
			contract.RequiredTraceFields,
			field => field.Kind == FindGroupRuntimeComparisonTraceFieldKind.ParsedPayload
				&& field.Requirement.Contains("Java readImpl and runImpl", StringComparison.Ordinal));
		Assert.Contains(
			contract.RequiredTraceFields,
			field => field.Kind == FindGroupRuntimeComparisonTraceFieldKind.WorldBroadcasts
				&& field.Requirement.Contains("excluded opposite-race players", StringComparison.Ordinal));
		Assert.Contains(
			contract.RequiredTraceFields,
			field => field.Kind == FindGroupRuntimeComparisonTraceFieldKind.EncryptedSocketFrames
				&& field.Requirement.Contains("after live dispatch exists", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CoversFindGroupScenarioMatrixConservatively()
	{
		var contract = FindGroupRuntimeComparisonPreflightContractService.Create();

		AssertScenario(contract, "show-list-direct", [0, 4, 10, 13, 15], "optional action 26 ordering");
		AssertScenario(contract, "mutation-direct", [2, 6, 8, 9, 17], "posted-message-before-refresh");
		AssertScenario(contract, "world-broadcast", [1, 5], "missing-branch no-send outcomes");
		AssertScenario(contract, "instance-application", [11], "missing-recipient no-send outcome");
		AssertScenario(contract, "action-12-invite", [12], "declined whisper");
		AssertScenario(contract, "parsed-only-no-run", [20, 25], "no runImpl branch");
		AssertScenario(contract, "shared-singleton-lifecycle", [], "logout cleanup");
	}

	[Fact]
	public void Create_AddsMutationPostFixtureRowWithoutMarkingRuntimeComparisonReady()
	{
		var contract = FindGroupRuntimeComparisonPreflightContractService.Create();

		var row = Assert.Single(contract.RequiredFixtureRows);
		Assert.Equal("mutation-post-actions-2-6", row.Name);
		Assert.Equal([2, 6], row.Actions);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", row.TraceName);
		Assert.Contains("FindGroupService.addRecruitment/addApplication", row.JavaSource, StringComparison.Ordinal);
		Assert.Contains("CreateExportFromDisabledPlan", row.CSharpProjectionSource, StringComparison.Ordinal);
		Assert.Contains("posted system message id", row.Requirement, StringComparison.Ordinal);
		Assert.Contains("refreshed show-list action", row.Requirement, StringComparison.Ordinal);
		Assert.Equal(
			FindGroupRuntimeComparisonFixtureContractStatus.BlockedPendingJavaAndLiveCSharpTrace,
			row.Status);
		Assert.False(contract.IsReadyForRuntimeComparison);
	}

	private static void AssertScenario(
		FindGroupRuntimeComparisonPreflightContract contract,
		string name,
		IReadOnlyList<int> actions,
		string expectedRequirement)
	{
		var scenario = Assert.Single(contract.RequiredScenarios, item => item.Name == name);
		Assert.Equal(actions, scenario.Actions);
		Assert.Contains(expectedRequirement, scenario.Requirement, StringComparison.Ordinal);
	}
}
