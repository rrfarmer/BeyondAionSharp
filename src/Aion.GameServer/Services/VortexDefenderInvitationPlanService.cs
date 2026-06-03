using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class VortexDefenderInvitationPlanService
{
	public const int DefenderQuestionId = SmQuestionWindow.VortexDefenderInvitation;

	public VortexDefenderInvitationPlan CreatePlan(
		VortexZonePlayerSnapshot defender,
		IReadOnlySet<int>? existingDefenderObjectIds = null,
		VortexDefenderAllianceSnapshot? alliance = null,
		bool requestSlotAvailable = true)
	{
		ArgumentNullException.ThrowIfNull(defender);

		var existingDefenders = existingDefenderObjectIds ?? new HashSet<int>();
		if (existingDefenders.Contains(defender.PlayerObjectId))
		{
			return CreateSkippedPlan(
				VortexDefenderInvitationPlanStatus.AlreadyDefender,
				defender,
				alliance,
				existingDefenders);
		}

		if (alliance?.IsFull == true)
		{
			return CreateSkippedPlan(
				VortexDefenderInvitationPlanStatus.AllianceFull,
				defender,
				alliance,
				existingDefenders);
		}

		var status = requestSlotAvailable
			? VortexDefenderInvitationPlanStatus.InvitationPlanned
			: VortexDefenderInvitationPlanStatus.RequestNotStored;

		return new VortexDefenderInvitationPlan(
			status,
			defender,
			existingDefenders.ToArray(),
			alliance ?? VortexDefenderAllianceSnapshot.Missing,
			DefenderQuestionId,
			QuestionWindowArg1: 0,
			QuestionWindowArg2: 0,
			RequestSlotAvailable: requestSlotAvailable,
			JavaSource: "services/vortex/Invasion.updateDefenders");
	}

	private static VortexDefenderInvitationPlan CreateSkippedPlan(
		VortexDefenderInvitationPlanStatus status,
		VortexZonePlayerSnapshot defender,
		VortexDefenderAllianceSnapshot? alliance,
		IReadOnlySet<int> existingDefenderObjectIds)
	{
		return new VortexDefenderInvitationPlan(
			status,
			defender,
			existingDefenderObjectIds.ToArray(),
			alliance ?? VortexDefenderAllianceSnapshot.Missing,
			RequestId: null,
			QuestionWindowArg1: null,
			QuestionWindowArg2: null,
			RequestSlotAvailable: false,
			JavaSource: "services/vortex/Invasion.updateDefenders");
	}
}

public enum VortexDefenderInvitationPlanStatus
{
	AlreadyDefender,
	AllianceFull,
	RequestNotStored,
	InvitationPlanned,
}

public sealed record VortexDefenderInvitationPlan(
	VortexDefenderInvitationPlanStatus Status,
	VortexZonePlayerSnapshot Defender,
	IReadOnlyList<int> ExistingDefenderObjectIds,
	VortexDefenderAllianceSnapshot Alliance,
	int? RequestId,
	int? QuestionWindowArg1,
	int? QuestionWindowArg2,
	bool RequestSlotAvailable,
	string JavaSource)
{
	public bool WouldInstallRequest => Status is VortexDefenderInvitationPlanStatus.RequestNotStored
		or VortexDefenderInvitationPlanStatus.InvitationPlanned;
	public bool HasQuestionWindowIntent => Status == VortexDefenderInvitationPlanStatus.InvitationPlanned;
	public bool ShouldMutateLiveRequest => false;
	public bool ShouldSendLivePacket => false;
}

public sealed record VortexDefenderAllianceSnapshot(
	bool Exists,
	bool IsFull,
	bool IsDisbanded = false)
{
	public static VortexDefenderAllianceSnapshot Missing { get; } = new(Exists: false, IsFull: false);
	public static VortexDefenderAllianceSnapshot Open { get; } = new(Exists: true, IsFull: false);
	public static VortexDefenderAllianceSnapshot Full { get; } = new(Exists: true, IsFull: true);
	public static VortexDefenderAllianceSnapshot Disbanded { get; } = new(Exists: true, IsFull: false, IsDisbanded: true);
}

public sealed class VortexDefenderInvitationRequestSlotSnapshotService
{
	public VortexDefenderInvitationRequestSlotSnapshot CreateSnapshot(Player defender)
	{
		ArgumentNullException.ThrowIfNull(defender);

		// Java parity: services/vortex/Invasion.updateDefenders uses
		// Player.getResponseRequester().putRequest(904306, responseHandler).
		return new VortexDefenderInvitationRequestSlotSnapshot(
			defender.ObjectId,
			VortexDefenderInvitationPlanService.DefenderQuestionId,
			defender.ResponseRequester.IsRequestSlotAvailable(VortexDefenderInvitationPlanService.DefenderQuestionId),
			defender.ResponseRequester.Count,
			JavaSource: "model/gameobjects/player/ResponseRequester.putRequest");
	}

	public IReadOnlyDictionary<int, bool> CollectRequestSlotsByPlayerObjectId(IEnumerable<Player>? defenders)
	{
		return (defenders ?? [])
			.Select(CreateSnapshot)
			.ToDictionary(snapshot => snapshot.PlayerObjectId, snapshot => snapshot.RequestSlotAvailable);
	}
}

public sealed record VortexDefenderInvitationRequestSlotSnapshot(
	int PlayerObjectId,
	int QuestionId,
	bool RequestSlotAvailable,
	int ActiveRequestCount,
	string JavaSource);

public sealed class VortexDefenderInvitationRequestPayloadPlanService
{
	public VortexDefenderInvitationRequestPayloadPlan CreatePlan(VortexDefenderInvitationPlan invitationPlan, int locationId = 0)
	{
		ArgumentNullException.ThrowIfNull(invitationPlan);

		if (!invitationPlan.WouldInstallRequest || invitationPlan.RequestId is not { } requestId)
		{
			return new VortexDefenderInvitationRequestPayloadPlan(
				VortexDefenderInvitationRequestPayloadPlanStatus.NotCreated,
				invitationPlan.Defender.PlayerObjectId,
				RequesterObjectId: 0,
				QuestionId: invitationPlan.RequestId ?? VortexDefenderInvitationPlanService.DefenderQuestionId,
				Request: null,
				InvitationPlan: invitationPlan,
				JavaSource: "services/vortex/Invasion.updateDefenders");
		}

		var payload = new PendingVortexDefenderInvitationRequest(
			invitationPlan.Defender.PlayerObjectId,
			requestId,
			invitationPlan.Alliance,
			invitationPlan.ExistingDefenderObjectIds,
			locationId);
		var request = new QuestionResponseRequest(
			invitationPlan.Defender.PlayerObjectId,
			QuestionResponseRequestKind.VortexDefenderInvitation,
			payload);

		return new VortexDefenderInvitationRequestPayloadPlan(
			VortexDefenderInvitationRequestPayloadPlanStatus.Created,
			invitationPlan.Defender.PlayerObjectId,
			invitationPlan.Defender.PlayerObjectId,
			requestId,
			request,
			invitationPlan,
			JavaSource: "services/vortex/Invasion.updateDefenders -> model/gameobjects/player/RequestResponseHandler");
	}
}

public enum VortexDefenderInvitationRequestPayloadPlanStatus
{
	NotCreated,
	Created,
}

public sealed record PendingVortexDefenderInvitationRequest(
	int RequesterObjectId,
	int QuestionId,
	VortexDefenderAllianceSnapshot DefenderAlliance,
	IReadOnlyList<int> ExistingDefenderObjectIds,
	int LocationId = 0);

public sealed record VortexDefenderInvitationRequestPayloadPlan(
	VortexDefenderInvitationRequestPayloadPlanStatus Status,
	int DefenderObjectId,
	int RequesterObjectId,
	int QuestionId,
	QuestionResponseRequest? Request,
	VortexDefenderInvitationPlan InvitationPlan,
	string JavaSource)
{
	public bool WouldCreateRequest => Status == VortexDefenderInvitationRequestPayloadPlanStatus.Created;
	public bool ShouldRegisterLiveRequest => false;
	public bool ShouldSendLivePacket => false;
}

public sealed class VortexDefenderInvitationRegistrationReportService
{
	private readonly VortexDefenderInvitationRequestPayloadPlanService _payloadPlanner = new();

	public VortexDefenderInvitationRegistrationReport CreateReport(VortexDefenderInvitationPlan invitationPlan, int locationId = 0)
	{
		ArgumentNullException.ThrowIfNull(invitationPlan);

		var payloadPlan = _payloadPlanner.CreatePlan(invitationPlan, locationId);
		var attempted = invitationPlan.WouldInstallRequest;
		var registered = attempted && invitationPlan.RequestSlotAvailable;
		var status = registered
			? VortexDefenderInvitationRegistrationReportStatus.Registered
			: attempted
				? VortexDefenderInvitationRegistrationReportStatus.RequestRejected
				: VortexDefenderInvitationRegistrationReportStatus.Skipped;
		var questionId = invitationPlan.RequestId ?? VortexDefenderInvitationPlanService.DefenderQuestionId;
		var wouldSendQuestionWindow = registered && invitationPlan.HasQuestionWindowIntent;

		return new VortexDefenderInvitationRegistrationReport(
			status,
			invitationPlan.Defender.PlayerObjectId,
			questionId,
			invitationPlan.RequestSlotAvailable,
			attempted,
			registered,
			wouldSendQuestionWindow,
			QuestionWindowSenderId: invitationPlan.QuestionWindowArg1,
			QuestionWindowRangeOrCooldownSeconds: invitationPlan.QuestionWindowArg2,
			payloadPlan,
			invitationPlan,
			JavaSource: "services/vortex/Invasion.updateDefenders -> model/gameobjects/player/ResponseRequester.putRequest");
	}
}

public enum VortexDefenderInvitationRegistrationReportStatus
{
	Skipped,
	RequestRejected,
	Registered,
}

public sealed record VortexDefenderInvitationRegistrationReport(
	VortexDefenderInvitationRegistrationReportStatus Status,
	int DefenderObjectId,
	int QuestionId,
	bool RequestSlotAvailable,
	bool WouldAttemptRequestRegistration,
	bool SimulatedPutRequestResult,
	bool WouldSendQuestionWindow,
	int? QuestionWindowSenderId,
	int? QuestionWindowRangeOrCooldownSeconds,
	VortexDefenderInvitationRequestPayloadPlan PayloadPlan,
	VortexDefenderInvitationPlan InvitationPlan,
	string JavaSource)
{
	public bool Registered => Status == VortexDefenderInvitationRegistrationReportStatus.Registered;
	public bool Rejected => Status == VortexDefenderInvitationRegistrationReportStatus.RequestRejected;
	public bool Skipped => Status == VortexDefenderInvitationRegistrationReportStatus.Skipped;
	public bool HasPayload => PayloadPlan.WouldCreateRequest;
	public bool ShouldRegisterLiveRequest => false;
	public bool ShouldSendLivePacket => false;
}

public sealed class VortexDefenderInvitationRegistrationRuntimeAdapterService
{
	private readonly VortexDefenderInvitationRequestPayloadPlanService _payloadPlanner = new();

	public VortexDefenderInvitationRegistrationRuntimeReport Register(
		Player defender,
		VortexDefenderInvitationPlan invitationPlan,
		int locationId = 0)
	{
		ArgumentNullException.ThrowIfNull(defender);
		ArgumentNullException.ThrowIfNull(invitationPlan);

		var questionId = invitationPlan.RequestId ?? VortexDefenderInvitationPlanService.DefenderQuestionId;
		var requestSlotAvailableBeforeRegistration = defender.ResponseRequester.IsRequestSlotAvailable(questionId);
		var payloadPlan = _payloadPlanner.CreatePlan(invitationPlan, locationId);
		var attempted = invitationPlan.WouldInstallRequest && payloadPlan.Request is not null;
		var registered = attempted && defender.ResponseRequester.PutRequest(questionId, payloadPlan.Request);
		var status = registered
			? VortexDefenderInvitationRegistrationReportStatus.Registered
			: invitationPlan.WouldInstallRequest
				? VortexDefenderInvitationRegistrationReportStatus.RequestRejected
				: VortexDefenderInvitationRegistrationReportStatus.Skipped;
		var wouldSendQuestionWindow = registered && invitationPlan.HasQuestionWindowIntent;

		return new VortexDefenderInvitationRegistrationRuntimeReport(
			status,
			defender.ObjectId,
			questionId,
			requestSlotAvailableBeforeRegistration,
			attempted,
			registered,
			wouldSendQuestionWindow,
			QuestionWindowSenderId: invitationPlan.QuestionWindowArg1,
			QuestionWindowRangeOrCooldownSeconds: invitationPlan.QuestionWindowArg2,
			payloadPlan,
			invitationPlan,
			JavaSource: "services/vortex/Invasion.updateDefenders -> model/gameobjects/player/ResponseRequester.putRequest");
	}
}

public sealed record VortexDefenderInvitationRegistrationRuntimeReport(
	VortexDefenderInvitationRegistrationReportStatus Status,
	int DefenderObjectId,
	int QuestionId,
	bool RequestSlotAvailableBeforeRegistration,
	bool AttemptedRequestRegistration,
	bool ActualPutRequestResult,
	bool WouldSendQuestionWindow,
	int? QuestionWindowSenderId,
	int? QuestionWindowRangeOrCooldownSeconds,
	VortexDefenderInvitationRequestPayloadPlan PayloadPlan,
	VortexDefenderInvitationPlan InvitationPlan,
	string JavaSource)
{
	public bool Registered => Status == VortexDefenderInvitationRegistrationReportStatus.Registered;
	public bool Rejected => Status == VortexDefenderInvitationRegistrationReportStatus.RequestRejected;
	public bool Skipped => Status == VortexDefenderInvitationRegistrationReportStatus.Skipped;
	public bool HasPayload => PayloadPlan.WouldCreateRequest;
	public bool RequestStoredByRegistry => ActualPutRequestResult;
	public bool ShouldSendLivePacket => false;
	public bool ShouldExecuteLiveCallback => false;
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveDefenders => false;
}
