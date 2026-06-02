using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupConcurrentMutationOrderingReadinessServiceTests
{
	[Fact]
	public void CreateReport_KeepsLiveSingletonConcurrencyBlocked()
	{
		var report = FindGroupConcurrentMutationOrderingReadinessService.CreateReport();

		Assert.Equal(
			FindGroupConcurrentMutationOrderingReadinessStatus.BlockedPendingLiveSingletonConcurrencyEvidence,
			report.Status);
		Assert.False(report.IsReadyForLiveSingletonConcurrency);
		Assert.Contains("FindGroupService.java", report.JavaFindGroupSource, StringComparison.Ordinal);
		Assert.Contains("FindGroupRecruitmentPlanService.cs", report.CSharpFindGroupSource, StringComparison.Ordinal);
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupConcurrentMutationOrderingEvidenceKind.LiveSingletonCallerInterleaving
				&& evidence.Status == FindGroupConcurrentMutationOrderingEvidenceStatus.Blocked
				&& evidence.Detail.Contains("CM_FIND_GROUP", StringComparison.Ordinal)
				&& evidence.Detail.Contains("same singleton state", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateReport_SeparatesConcurrentMapShapeFromMultiStepLiveEvidence()
	{
		var report = FindGroupConcurrentMutationOrderingReadinessService.CreateReport();

		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupConcurrentMutationOrderingEvidenceKind.JavaConcurrentMapShape
				&& evidence.Status == FindGroupConcurrentMutationOrderingEvidenceStatus.Reviewed
				&& evidence.Detail.Contains("ConcurrentHashMap", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupConcurrentMutationOrderingEvidenceKind.CSharpConcurrentDictionaryShape
				&& evidence.Status == FindGroupConcurrentMutationOrderingEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("ConcurrentDictionary", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupConcurrentMutationOrderingEvidenceKind.CSharpBasicConcurrentStoreTests
				&& evidence.Status == FindGroupConcurrentMutationOrderingEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("concurrent add/logout operations", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupConcurrentMutationOrderingEvidenceKind.CSharpDeterministicSharedSingletonInterleavingTests
				&& evidence.Status == FindGroupConcurrentMutationOrderingEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("CM_FIND_GROUP-created state", StringComparison.Ordinal)
				&& evidence.Detail.Contains("group disband cleanup", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupConcurrentMutationOrderingEvidenceKind.CSharpDeterministicSharedSingletonTraceProjection
				&& evidence.Status == FindGroupConcurrentMutationOrderingEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("FindGroupSharedSingletonInterleavingTraceService", StringComparison.Ordinal)
				&& evidence.Detail.Contains("logout-before-joined-team", StringComparison.Ordinal)
				&& evidence.Detail.Contains("joined-team-before-logout-before-disband", StringComparison.Ordinal));
		Assert.Contains(
			report.NextRequiredEvidence,
			item => item.Contains("ConcurrentDictionary storage shape alone", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateReport_RecordsJavaOnJoinedTeamOrderingAndRemainingInterleavingWork()
	{
		var report = FindGroupConcurrentMutationOrderingReadinessService.CreateReport();

		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupConcurrentMutationOrderingEvidenceKind.JavaOnJoinedTeamMethodOrder
				&& evidence.Status == FindGroupConcurrentMutationOrderingEvidenceStatus.Reviewed
				&& evidence.Detail.Contains("unknown3=16", StringComparison.Ordinal)
				&& evidence.Detail.Contains("re-adds leader recruitment", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupConcurrentMutationOrderingEvidenceKind.CSharpSequentialOnJoinedTeamTests
				&& evidence.Status == FindGroupConcurrentMutationOrderingEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("leader re-add priority", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupConcurrentMutationOrderingEvidenceKind.RuntimeComparison
				&& evidence.Status == FindGroupConcurrentMutationOrderingEvidenceStatus.Blocked
				&& evidence.Detail.Contains("concurrent player actions", StringComparison.Ordinal));
		Assert.Contains(
			report.NextRequiredEvidence,
			item => item.Contains("live boundary tests or runtime traces", StringComparison.Ordinal));
	}
}
