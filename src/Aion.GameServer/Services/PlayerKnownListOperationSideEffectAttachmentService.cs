using Aion.GameServer.Model;

namespace Aion.GameServer.Services;

public enum PlayerKnownListOperationSideEffectAttachmentStatus
{
	Attached,
	SkippedRejectedPlan,
	NoSideEffectSteps,
}

public sealed record PlayerKnownListOperationSideEffectDirectionFacts(
	bool ViewerAggroIconToSubject = false,
	bool SubjectIsInRideMode = false,
	int? SubjectRideNpcId = null,
	bool SubjectIsUnderStance = false,
	bool SubjectHasAbnormalEffects = false,
	bool ViewerIsSpawned = true,
	ObjectDeleteAnimation NotSeeAnimation = ObjectDeleteAnimation.None);

public sealed record PlayerKnownListOperationSideEffectAttachmentRequest(
	PlayerKnownListTwoWayOperationPlan OperationPlan,
	PlayerKnownListOperationSideEffectDirectionFacts OwnerViewingCandidate,
	PlayerKnownListOperationSideEffectDirectionFacts CandidateViewingOwner);

public sealed record PlayerKnownListOperationAttachedSideEffect(
	PlayerKnownListTwoWayOperationStep OperationStep,
	PlayerKnownListPlayerSideEffectPlan SideEffectPlan);

public sealed record PlayerKnownListOperationSideEffectAttachmentPlan(
	PlayerKnownListTwoWayOperationPlan OperationPlan,
	PlayerKnownListOperationSideEffectAttachmentStatus Status,
	IReadOnlyList<PlayerKnownListOperationAttachedSideEffect> AttachedSideEffects,
	bool ExecutesLivePackets,
	bool IsJavaControllerParity,
	bool IsLive,
	string JavaSource);

public sealed class PlayerKnownListOperationSideEffectAttachmentService
{
	private readonly PlayerKnownListPlayerSideEffectPlanService _playerSideEffects;

	public PlayerKnownListOperationSideEffectAttachmentService(
		PlayerKnownListPlayerSideEffectPlanService? playerSideEffects = null)
	{
		_playerSideEffects = playerSideEffects ?? new PlayerKnownListPlayerSideEffectPlanService();
	}

	public PlayerKnownListOperationSideEffectAttachmentPlan Attach(
		PlayerKnownListOperationSideEffectAttachmentRequest request)
	{
		// Java parity breadcrumb: KnownList.updateVisibility and KnownList.del
		// produce see/notSee controller callbacks after membership operations.
		if (request.OperationPlan.Status != PlayerKnownListTwoWayOperationStatus.Planned)
		{
			return CreatePlan(
				request.OperationPlan,
				PlayerKnownListOperationSideEffectAttachmentStatus.SkippedRejectedPlan,
				[]);
		}

		var attachments = new List<PlayerKnownListOperationAttachedSideEffect>();
		foreach (var step in request.OperationPlan.Steps)
		{
			var sideEffect = CreateSideEffectForStep(request, step);
			if (sideEffect is not null)
				attachments.Add(new PlayerKnownListOperationAttachedSideEffect(step, sideEffect));
		}

		return CreatePlan(
			request.OperationPlan,
			attachments.Count == 0
				? PlayerKnownListOperationSideEffectAttachmentStatus.NoSideEffectSteps
				: PlayerKnownListOperationSideEffectAttachmentStatus.Attached,
			attachments);
	}

	private PlayerKnownListPlayerSideEffectPlan? CreateSideEffectForStep(
		PlayerKnownListOperationSideEffectAttachmentRequest request,
		PlayerKnownListTwoWayOperationStep step) =>
		step.Kind switch
		{
			PlayerKnownListTwoWayOperationStepKind.OwnerSeesCandidate =>
				PlanSee(
					request.OperationPlan.OwnerPlayerObjectId,
					request.OperationPlan.CandidatePlayerObjectId,
					request.OwnerViewingCandidate),
			PlayerKnownListTwoWayOperationStepKind.CandidateSeesOwner =>
				PlanSee(
					request.OperationPlan.CandidatePlayerObjectId,
					request.OperationPlan.OwnerPlayerObjectId,
					request.CandidateViewingOwner),
			PlayerKnownListTwoWayOperationStepKind.OwnerNotSeesCandidate =>
				PlanNotSee(
					request.OperationPlan.OwnerPlayerObjectId,
					request.OperationPlan.CandidatePlayerObjectId,
					request.OwnerViewingCandidate),
			PlayerKnownListTwoWayOperationStepKind.CandidateNotSeesOwner =>
				PlanNotSee(
					request.OperationPlan.CandidatePlayerObjectId,
					request.OperationPlan.OwnerPlayerObjectId,
					request.CandidateViewingOwner),
			_ => null,
		};

	private PlayerKnownListPlayerSideEffectPlan PlanSee(
		int viewerPlayerObjectId,
		int subjectPlayerObjectId,
		PlayerKnownListOperationSideEffectDirectionFacts facts) =>
		_playerSideEffects.PlanSee(new PlayerKnownListPlayerSeeSideEffectContext(
			viewerPlayerObjectId,
			subjectPlayerObjectId,
			facts.ViewerAggroIconToSubject,
			facts.SubjectIsInRideMode,
			facts.SubjectRideNpcId,
			facts.SubjectIsUnderStance,
			facts.SubjectHasAbnormalEffects));

	private PlayerKnownListPlayerSideEffectPlan PlanNotSee(
		int viewerPlayerObjectId,
		int subjectPlayerObjectId,
		PlayerKnownListOperationSideEffectDirectionFacts facts) =>
		_playerSideEffects.PlanNotSee(new PlayerKnownListPlayerNotSeeSideEffectContext(
			viewerPlayerObjectId,
			subjectPlayerObjectId,
			facts.NotSeeAnimation,
			facts.ViewerIsSpawned));

	private static PlayerKnownListOperationSideEffectAttachmentPlan CreatePlan(
		PlayerKnownListTwoWayOperationPlan operationPlan,
		PlayerKnownListOperationSideEffectAttachmentStatus status,
		IReadOnlyList<PlayerKnownListOperationAttachedSideEffect> attachedSideEffects) =>
		new(
			operationPlan,
			status,
			attachedSideEffects,
			ExecutesLivePackets: false,
			IsJavaControllerParity: false,
			IsLive: false,
			"Descriptor attachment for com.aionemu.gameserver.world.knownlist.KnownList see/notSee controller callbacks; does not execute live packets.");
}
