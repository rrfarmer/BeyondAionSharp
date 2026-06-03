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
