using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class VortexDefenderInvitationAcceptancePlanService
{
	public VortexDefenderInvitationAcceptancePlan CreatePlan(
		VortexDefenderInvitationResponderSnapshot responder,
		VortexDefenderAllianceSnapshot? defenderAlliance = null)
	{
		ArgumentNullException.ThrowIfNull(responder);

		var alliance = defenderAlliance ?? VortexDefenderAllianceSnapshot.Missing;
		var wouldRemoveGroup = responder.IsInGroup;
		var wouldRemoveAlliance = !wouldRemoveGroup && responder.IsInAlliance;
		var wouldAddDefender = !alliance.IsFull;
		var status = wouldAddDefender
			? VortexDefenderInvitationAcceptancePlanStatus.AcceptancePlanned
			: VortexDefenderInvitationAcceptancePlanStatus.DefenderAllianceFull;

		return new VortexDefenderInvitationAcceptancePlan(
			status,
			responder,
			alliance,
			wouldRemoveGroup,
			wouldRemoveAlliance,
			wouldAddDefender,
			JavaSource: "services/vortex/Invasion.updateDefenders.RequestResponseHandler.acceptRequest");
	}
}

public sealed class VortexDefenderInvitationResponseDispatchPlanService
{
	private readonly VortexDefenderInvitationAcceptancePlanService _acceptancePlanner = new();

	public VortexDefenderInvitationResponseDispatchPlan CreatePlan(
		PendingVortexDefenderInvitationRequest request,
		VortexDefenderInvitationResponderSnapshot responder,
		int responseCode,
		VortexDefenderAllianceSnapshot? defenderAlliance = null)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(responder);

		var accepted = responseCode != 0;
		var acceptancePlan = accepted
			? _acceptancePlanner.CreatePlan(responder, defenderAlliance ?? request.DefenderAlliance)
			: null;

		return new VortexDefenderInvitationResponseDispatchPlan(
			accepted
				? VortexDefenderInvitationResponseDispatchPlanStatus.Accepted
				: VortexDefenderInvitationResponseDispatchPlanStatus.Denied,
			request.QuestionId,
			responseCode,
			request.RequesterObjectId,
			responder.PlayerObjectId,
			request,
			acceptancePlan,
			JavaSource: accepted
				? "model/gameobjects/player/RequestResponseHandler.handle -> services/vortex/Invasion.updateDefenders.acceptRequest"
				: "model/gameobjects/player/RequestResponseHandler.handle -> model/gameobjects/player/RequestResponseHandler.denyRequest");
	}
}

public sealed class VortexDefenderInvitationResponseConsumptionReportService
{
	private readonly VortexDefenderInvitationResponseDispatchPlanService _dispatchPlanner = new();

	public VortexDefenderInvitationResponseConsumptionReport CreateReport(
		int questionId,
		int responseCode,
		QuestionResponseDispatch? dispatch,
		VortexDefenderInvitationResponderSnapshot responder,
		VortexDefenderAllianceSnapshot? defenderAlliance = null)
	{
		ArgumentNullException.ThrowIfNull(responder);

		if (dispatch is null)
		{
			return CreateReport(
				VortexDefenderInvitationResponseConsumptionReportStatus.RequestMissing,
				questionId,
				responseCode,
				requestRemovedByRegistry: false,
				wouldInvokeHandler: false,
				hasVortexPayload: false,
				request: null,
				dispatchPlan: null,
				JavaSource: "model/gameobjects/player/ResponseRequester.respond");
		}

		if (dispatch.Request.Kind != QuestionResponseRequestKind.VortexDefenderInvitation)
		{
			return CreateReport(
				VortexDefenderInvitationResponseConsumptionReportStatus.NonVortexRequest,
				dispatch.QuestionId,
				dispatch.ResponseCode,
				requestRemovedByRegistry: true,
				wouldInvokeHandler: false,
				hasVortexPayload: false,
				dispatch.Request,
				dispatchPlan: null,
				JavaSource: "model/gameobjects/player/ResponseRequester.respond");
		}

		if (dispatch.Request.Payload is not PendingVortexDefenderInvitationRequest request)
		{
			return CreateReport(
				VortexDefenderInvitationResponseConsumptionReportStatus.PayloadMissing,
				dispatch.QuestionId,
				dispatch.ResponseCode,
				requestRemovedByRegistry: true,
				wouldInvokeHandler: false,
				hasVortexPayload: false,
				dispatch.Request,
				dispatchPlan: null,
				JavaSource: "model/gameobjects/player/ResponseRequester.respond");
		}

		var dispatchPlan = _dispatchPlanner.CreatePlan(request, responder, dispatch.ResponseCode, defenderAlliance);
		return CreateReport(
			dispatchPlan.Accepted
				? VortexDefenderInvitationResponseConsumptionReportStatus.Accepted
				: VortexDefenderInvitationResponseConsumptionReportStatus.Denied,
			dispatch.QuestionId,
			dispatch.ResponseCode,
			requestRemovedByRegistry: true,
			wouldInvokeHandler: true,
			hasVortexPayload: true,
			dispatch.Request,
			dispatchPlan,
			JavaSource: "model/gameobjects/player/ResponseRequester.respond -> model/gameobjects/player/RequestResponseHandler.handle");
	}

	private static VortexDefenderInvitationResponseConsumptionReport CreateReport(
		VortexDefenderInvitationResponseConsumptionReportStatus status,
		int questionId,
		int responseCode,
		bool requestRemovedByRegistry,
		bool wouldInvokeHandler,
		bool hasVortexPayload,
		QuestionResponseRequest? request,
		VortexDefenderInvitationResponseDispatchPlan? dispatchPlan,
		string JavaSource)
	{
		return new VortexDefenderInvitationResponseConsumptionReport(
			status,
			questionId,
			responseCode,
			requestRemovedByRegistry,
			wouldInvokeHandler,
			hasVortexPayload,
			request,
			dispatchPlan,
			JavaSource);
	}
}

public sealed class VortexDefenderInvitationResponseRuntimeAdapterService
{
	private readonly VortexDefenderInvitationResponseConsumptionReportService _consumptionReporter = new();
	private readonly VortexDefenderUpdateDefendersPlanService _updateDefendersPlanner = new();

	public VortexDefenderInvitationResponseConsumptionReport HandleResponse(
		Player responder,
		int questionId,
		int responseCode,
		VortexDefenderAllianceSnapshot? defenderAlliance = null)
	{
		ArgumentNullException.ThrowIfNull(responder);

		var dispatch = responder.ResponseRequester.Respond(questionId, responseCode);
		var responderSnapshot = VortexDefenderInvitationResponderSnapshot.FromPlayer(responder);
		return _consumptionReporter.CreateReport(
			questionId,
			responseCode,
			dispatch,
			responderSnapshot,
			defenderAlliance);
	}

	public VortexDefenderInvitationAcceptanceTransitionRuntimeReport HandleResponseWithAcceptanceTransition(
		Player responder,
		int questionId,
		int responseCode,
		IReadOnlyList<VortexDefenderAddPlayerSnapshot>? existingDefenders = null,
		VortexDefenderAllianceSnapshot? defenderAlliance = null)
	{
		var consumption = HandleResponse(responder, questionId, responseCode, defenderAlliance);
		VortexDefenderUpdateDefendersPlan? updatePlan = null;
		if (consumption.Accepted && consumption.DispatchPlan?.Request is { } request)
		{
			var defenders = existingDefenders
				?? request.ExistingDefenderObjectIds
					.Select(objectId => new VortexDefenderAddPlayerSnapshot(objectId, IsInGroup: false, IsInAlliance: false))
					.ToArray();
			updatePlan = _updateDefendersPlanner.CreateAcceptancePlan(
				VortexDefenderInvitationResponderSnapshot.FromPlayer(responder),
				defenders,
				defenderAlliance ?? request.DefenderAlliance);
		}

		return new VortexDefenderInvitationAcceptanceTransitionRuntimeReport(
			consumption.Status,
			responder.ObjectId,
			consumption,
			updatePlan,
			JavaSource: "model/gameobjects/player/ResponseRequester.respond -> services/vortex/Invasion.updateDefenders.RequestResponseHandler.acceptRequest");
	}
}

public sealed class VortexDefenderAcceptanceParticipantRuntimeReportService
{
	public VortexDefenderAcceptanceParticipantRuntimeReport CreateReport(
		int locationId,
		VortexDefenderInvitationAcceptanceTransitionRuntimeReport transitionReport,
		IReadOnlyList<int>? currentDefenderObjectIds = null)
	{
		ArgumentNullException.ThrowIfNull(transitionReport);

		var addPlayerPlan = transitionReport.UpdateDefendersPlan?.AddPlayerPlan;
		var before = (currentDefenderObjectIds ?? transitionReport.UpdateDefendersPlan?.ExistingDefenderObjectIds ?? []).ToArray();
		if (addPlayerPlan?.WouldWarn == true)
		{
			return CreateReport(
				VortexDefenderAcceptanceParticipantRuntimeReportStatus.WarningNoParticipantPut,
				locationId,
				transitionReport,
				addPlayerPlan,
				before,
				before,
				wouldRecordParticipant: false);
		}

		if (addPlayerPlan is null || !transitionReport.WouldPutParticipant)
		{
			return CreateReport(
				VortexDefenderAcceptanceParticipantRuntimeReportStatus.NoParticipantMutation,
				locationId,
				transitionReport,
				addPlayerPlan,
				before,
				before,
				wouldRecordParticipant: false);
		}

		if (before.Contains(transitionReport.ResponderObjectId))
		{
			return CreateReport(
				VortexDefenderAcceptanceParticipantRuntimeReportStatus.AlreadyParticipant,
				locationId,
				transitionReport,
				addPlayerPlan,
				before,
				before,
				wouldRecordParticipant: false);
		}

		return CreateReport(
			VortexDefenderAcceptanceParticipantRuntimeReportStatus.ParticipantWouldBeRecorded,
			locationId,
			transitionReport,
			addPlayerPlan,
			before,
			[.. before, transitionReport.ResponderObjectId],
			wouldRecordParticipant: true);
	}

	private static VortexDefenderAcceptanceParticipantRuntimeReport CreateReport(
		VortexDefenderAcceptanceParticipantRuntimeReportStatus status,
		int locationId,
		VortexDefenderInvitationAcceptanceTransitionRuntimeReport transitionReport,
		VortexDefenderAddPlayerTransitionPlan? addPlayerPlan,
		IReadOnlyList<int> defenderObjectIdsBefore,
		IReadOnlyList<int> defenderObjectIdsAfter,
		bool wouldRecordParticipant)
	{
		return new VortexDefenderAcceptanceParticipantRuntimeReport(
			status,
			locationId,
			transitionReport.ResponderObjectId,
			defenderObjectIdsBefore,
			defenderObjectIdsAfter,
			transitionReport,
			addPlayerPlan,
			wouldRecordParticipant,
			JavaSource: "services/vortex/Invasion.addPlayer(player, false) -> defenders.put(player.getObjectId(), player)");
	}
}

public sealed class VortexDefenderAcceptanceRuntimeObserverService
{
	private readonly VortexDefenderInvitationResponseRuntimeAdapterService _adapter = new();
	private readonly VortexDefenderAcceptanceParticipantRuntimeReportService _participantReporter = new();

	public VortexDefenderAcceptanceRuntimeObserverReport Observe(
		int locationId,
		Player responder,
		int questionId,
		int responseCode,
		IReadOnlyList<VortexDefenderAddPlayerSnapshot>? existingDefenders = null,
		VortexDefenderAllianceSnapshot? defenderAlliance = null,
		IReadOnlyList<int>? currentDefenderObjectIds = null)
	{
		ArgumentNullException.ThrowIfNull(responder);

		var transition = _adapter.HandleResponseWithAcceptanceTransition(
			responder, questionId, responseCode, existingDefenders, defenderAlliance);
		var participantReport = _participantReporter.CreateReport(
			locationId, transition, currentDefenderObjectIds);

		return new VortexDefenderAcceptanceRuntimeObserverReport(
			locationId,
			responder.ObjectId,
			transition,
			participantReport,
			JavaSource: "model/gameobjects/player/ResponseRequester.respond -> services/vortex/Invasion.updateDefenders.RequestResponseHandler.acceptRequest -> services/vortex/Invasion.addPlayer(player, false) -> defenders.put(player.getObjectId(), player)");
	}
}

public sealed record VortexDefenderAcceptanceRuntimeObserverReport(
	int LocationId,
	int ResponderObjectId,
	VortexDefenderInvitationAcceptanceTransitionRuntimeReport TransitionReport,
	VortexDefenderAcceptanceParticipantRuntimeReport ParticipantReport,
	string JavaSource)
{
	public bool Accepted => TransitionReport.Accepted;
	public bool Denied => TransitionReport.Denied;
	public bool WouldRecordParticipant => ParticipantReport.WouldRecordParticipant;
	public bool WouldPutParticipant => ParticipantReport.WouldPutParticipant;
	public bool WouldWarn => ParticipantReport.WouldWarn;
	public IReadOnlyList<int> DefenderObjectIdsBefore => ParticipantReport.DefenderObjectIdsBefore;
	public IReadOnlyList<int> DefenderObjectIdsAfter => ParticipantReport.DefenderObjectIdsAfter;
	public VortexDefenderAcceptanceParticipantRuntimeReportStatus ParticipantStatus => ParticipantReport.Status;
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveDefenders => false;
	public bool ShouldSendLivePacket => false;
}

public enum VortexDefenderInvitationAcceptancePlanStatus
{
	AcceptancePlanned,
	DefenderAllianceFull,
}

public enum VortexDefenderInvitationResponseDispatchPlanStatus
{
	Denied,
	Accepted,
}

public enum VortexDefenderInvitationResponseConsumptionReportStatus
{
	RequestMissing,
	NonVortexRequest,
	PayloadMissing,
	Denied,
	Accepted,
}

public enum VortexDefenderAcceptanceParticipantRuntimeReportStatus
{
	NoParticipantMutation,
	ParticipantWouldBeRecorded,
	AlreadyParticipant,
	WarningNoParticipantPut,
}

public sealed record VortexDefenderInvitationAcceptancePlan(
	VortexDefenderInvitationAcceptancePlanStatus Status,
	VortexDefenderInvitationResponderSnapshot Responder,
	VortexDefenderAllianceSnapshot DefenderAlliance,
	bool WouldRemoveGroup,
	bool WouldRemoveAlliance,
	bool WouldAddDefender,
	string JavaSource)
{
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveDefenders => false;
}

public sealed record VortexDefenderInvitationResponseDispatchPlan(
	VortexDefenderInvitationResponseDispatchPlanStatus Status,
	int QuestionId,
	int ResponseCode,
	int RequesterObjectId,
	int ResponderObjectId,
	PendingVortexDefenderInvitationRequest Request,
	VortexDefenderInvitationAcceptancePlan? AcceptancePlan,
	string JavaSource)
{
	public bool Accepted => Status == VortexDefenderInvitationResponseDispatchPlanStatus.Accepted;
	public bool Denied => Status == VortexDefenderInvitationResponseDispatchPlanStatus.Denied;
	public bool HasAcceptancePlan => AcceptancePlan is not null;
	public bool ShouldRemoveLiveRequest => false;
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveDefenders => false;
}

public sealed record VortexDefenderInvitationResponseConsumptionReport(
	VortexDefenderInvitationResponseConsumptionReportStatus Status,
	int QuestionId,
	int ResponseCode,
	bool RequestRemovedByRegistry,
	bool WouldInvokeHandler,
	bool HasVortexPayload,
	QuestionResponseRequest? Request,
	VortexDefenderInvitationResponseDispatchPlan? DispatchPlan,
	string JavaSource)
{
	public bool Accepted => Status == VortexDefenderInvitationResponseConsumptionReportStatus.Accepted;
	public bool Denied => Status == VortexDefenderInvitationResponseConsumptionReportStatus.Denied;
	public bool HasDispatchPlan => DispatchPlan is not null;
	public bool ShouldRemoveLiveRequest => false;
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveDefenders => false;
}

public sealed record VortexDefenderInvitationAcceptanceTransitionRuntimeReport(
	VortexDefenderInvitationResponseConsumptionReportStatus Status,
	int ResponderObjectId,
	VortexDefenderInvitationResponseConsumptionReport ConsumptionReport,
	VortexDefenderUpdateDefendersPlan? UpdateDefendersPlan,
	string JavaSource)
{
	public bool Accepted => ConsumptionReport.Accepted;
	public bool Denied => ConsumptionReport.Denied;
	public bool RequestRemovedByRegistry => ConsumptionReport.RequestRemovedByRegistry;
	public bool HasVortexPayload => ConsumptionReport.HasVortexPayload;
	public bool HasDispatchPlan => ConsumptionReport.HasDispatchPlan;
	public bool HasAcceptanceTransitionPlan => UpdateDefendersPlan is not null;
	public bool HasAddPlayerPlan => UpdateDefendersPlan?.HasAddPlayerPlan == true;
	public bool WouldRemoveGroup => UpdateDefendersPlan?.WouldRemoveGroup == true;
	public bool WouldRemoveAlliance => UpdateDefendersPlan?.WouldRemoveAlliance == true;
	public bool WouldCallAddPlayer => UpdateDefendersPlan?.WouldCallAddPlayer == true;
	public bool WouldPutParticipant => UpdateDefendersPlan?.WouldPutParticipant == true;
	public bool WouldWarn => UpdateDefendersPlan?.WouldWarn == true;
	public bool ShouldRemoveLiveRequest => false;
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveDefenders => false;
}

public sealed record VortexDefenderAcceptanceParticipantRuntimeReport(
	VortexDefenderAcceptanceParticipantRuntimeReportStatus Status,
	int LocationId,
	int ResponderObjectId,
	IReadOnlyList<int> DefenderObjectIdsBefore,
	IReadOnlyList<int> DefenderObjectIdsAfter,
	VortexDefenderInvitationAcceptanceTransitionRuntimeReport TransitionReport,
	VortexDefenderAddPlayerTransitionPlan? AddPlayerPlan,
	bool WouldRecordParticipant,
	string JavaSource)
{
	public bool HasAddPlayerPlan => AddPlayerPlan is not null;
	public bool WouldPutParticipant => AddPlayerPlan?.WouldPutParticipant == true;
	public bool WouldWarn => AddPlayerPlan?.WouldWarn == true;
	public bool AlreadyParticipant => Status == VortexDefenderAcceptanceParticipantRuntimeReportStatus.AlreadyParticipant;
	public bool WouldAddToExistingAlliance => AddPlayerPlan?.WouldAddToExistingAlliance == true;
	public bool WouldCreateDefenderAlliance => AddPlayerPlan?.WouldCreateDefenderAlliance == true;
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveDefenders => false;
	public bool ShouldSendLivePacket => false;
}

public sealed record VortexDefenderInvitationResponderSnapshot(
	int PlayerObjectId,
	bool IsInGroup,
	bool IsInAlliance)
{
	public static VortexDefenderInvitationResponderSnapshot FromPlayer(Player player)
	{
		ArgumentNullException.ThrowIfNull(player);
		return new VortexDefenderInvitationResponderSnapshot(
			player.ObjectId,
			player.TeamMembership == PlayerTeamMembership.Group,
			player.TeamMembership == PlayerTeamMembership.Alliance);
	}
}
