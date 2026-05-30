namespace Aion.GameServer.Services;

public enum PlayerKnownListTwoWayMembershipAdapterStatus
{
	Disabled,
	SkippedRejectedPlan,
	NoMembershipSteps,
	Applied,
}

public sealed record PlayerKnownListTwoWayMembershipAdapterRequest(PlayerKnownListTwoWayOperationPlan Plan, bool ExecuteMembershipMutation = false);

public sealed record PlayerKnownListTwoWayMembershipAdapterResult(
	PlayerKnownListTwoWayMembershipAdapterStatus Status,
	PlayerKnownListTwoWayOperationPlan Plan,
	IReadOnlyList<PlayerKnownListMembershipSnapshot> MembershipSnapshots,
	int AppliedMembershipStepCount,
	IReadOnlyList<PlayerKnownListTwoWayOperationStep> PreservedSideEffectSteps,
	bool MutatedMembership,
	bool ExecutedControllerSideEffects,
	bool IsJavaRegionKnownListParity,
	string JavaSource,
	bool IsLive
);

public sealed class PlayerKnownListTwoWayMembershipAdapterService
{
	private readonly PlayerKnownListMembershipService _membershipService;

	public PlayerKnownListTwoWayMembershipAdapterService(PlayerKnownListMembershipService membershipService)
	{
		_membershipService = membershipService;
	}

	public PlayerKnownListTwoWayMembershipAdapterResult Apply(PlayerKnownListTwoWayMembershipAdapterRequest request)
	{
		// Java parity: KnownList.add/remove drives two-way player membership plus controller side effects.
		// This adapter applies only the membership mutations from the staged two-way plan and preserves the
		// remaining Java side effects as descriptors.
		if (!request.ExecuteMembershipMutation)
		{
			return CreateResult(PlayerKnownListTwoWayMembershipAdapterStatus.Disabled, request.Plan, [], appliedMembershipStepCount: 0);
		}

		if (request.Plan.Status != PlayerKnownListTwoWayOperationStatus.Planned)
		{
			return CreateResult(PlayerKnownListTwoWayMembershipAdapterStatus.SkippedRejectedPlan, request.Plan, [], appliedMembershipStepCount: 0);
		}

		var membershipSteps = request
			.Plan.Steps.Where(step =>
				step.Kind
					is PlayerKnownListTwoWayOperationStepKind.CandidateAddsOwner
						or PlayerKnownListTwoWayOperationStepKind.OwnerAddsCandidate
						or PlayerKnownListTwoWayOperationStepKind.OwnerRemovesCandidate
						or PlayerKnownListTwoWayOperationStepKind.CandidateRemovesOwner
			)
			.ToArray();
		if (membershipSteps.Length == 0)
		{
			return CreateResult(PlayerKnownListTwoWayMembershipAdapterStatus.NoMembershipSteps, request.Plan, [], appliedMembershipStepCount: 0);
		}

		var snapshots = new List<PlayerKnownListMembershipSnapshot>();
		foreach (var step in membershipSteps)
		{
			switch (step.Kind)
			{
				case PlayerKnownListTwoWayOperationStepKind.CandidateAddsOwner:
					snapshots.Add(
						_membershipService.UpsertKnownPlayers(
							step.CandidatePlayerObjectId,
							[
								new PlayerKnownListMembershipCandidate(
									step.OwnerPlayerObjectId,
									request.Plan.Steps.Any(s => s.Kind == PlayerKnownListTwoWayOperationStepKind.CandidateSeesOwner),
									step.JavaSource
								),
							],
							PlayerKnownListMembershipUpdateReason.TwoWayOperationPlan
						)
					);
					break;
				case PlayerKnownListTwoWayOperationStepKind.OwnerAddsCandidate:
					snapshots.Add(
						_membershipService.UpsertKnownPlayers(
							step.OwnerPlayerObjectId,
							[
								new PlayerKnownListMembershipCandidate(
									step.CandidatePlayerObjectId,
									request.Plan.Steps.Any(s => s.Kind == PlayerKnownListTwoWayOperationStepKind.OwnerSeesCandidate),
									step.JavaSource
								),
							],
							PlayerKnownListMembershipUpdateReason.TwoWayOperationPlan
						)
					);
					break;
				case PlayerKnownListTwoWayOperationStepKind.OwnerRemovesCandidate:
					_membershipService.RemoveKnownPlayer(step.OwnerPlayerObjectId, step.CandidatePlayerObjectId, out var ownerSnapshot);
					snapshots.Add(ownerSnapshot);
					break;
				case PlayerKnownListTwoWayOperationStepKind.CandidateRemovesOwner:
					_membershipService.RemoveKnownPlayer(step.CandidatePlayerObjectId, step.OwnerPlayerObjectId, out var candidateSnapshot);
					snapshots.Add(candidateSnapshot);
					break;
			}
		}

		return CreateResult(PlayerKnownListTwoWayMembershipAdapterStatus.Applied, request.Plan, snapshots, membershipSteps.Length);
	}

	private static PlayerKnownListTwoWayMembershipAdapterResult CreateResult(
		PlayerKnownListTwoWayMembershipAdapterStatus status,
		PlayerKnownListTwoWayOperationPlan plan,
		IReadOnlyList<PlayerKnownListMembershipSnapshot> snapshots,
		int appliedMembershipStepCount
	) =>
		new(
			status,
			plan,
			snapshots,
			appliedMembershipStepCount,
			plan.Steps.Where(step =>
					step.Kind
						is PlayerKnownListTwoWayOperationStepKind.CandidateSeesOwner
							or PlayerKnownListTwoWayOperationStepKind.OwnerSeesCandidate
							or PlayerKnownListTwoWayOperationStepKind.OwnerNotSeesCandidate
							or PlayerKnownListTwoWayOperationStepKind.OwnerNotKnowsCandidate
							or PlayerKnownListTwoWayOperationStepKind.CandidateNotSeesOwner
							or PlayerKnownListTwoWayOperationStepKind.CandidateNotKnowsOwner
				)
				.ToArray(),
			MutatedMembership: status == PlayerKnownListTwoWayMembershipAdapterStatus.Applied,
			ExecutedControllerSideEffects: false,
			IsJavaRegionKnownListParity: false,
			"Applies PlayerKnownListTwoWayOperationPlan membership steps only; controller side effects remain descriptors and live Java KnownList locking is not implemented",
			IsLive: false
		);
}
