using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class VortexDefenderUpdateDefendersPlanService
{
	private readonly VortexDefenderInvitationPlanService _invitationPlanner = new();
	private readonly VortexDefenderInvitationAcceptancePlanService _acceptancePlanner = new();
	private readonly VortexDefenderAddPlayerTransitionPlanService _addPlayerPlanner = new();

	public VortexDefenderUpdateDefendersPlan CreateInvitationPlan(
		VortexZonePlayerSnapshot defender,
		IReadOnlyList<VortexDefenderAddPlayerSnapshot>? existingDefenders = null,
		VortexDefenderAllianceSnapshot? defenderAlliance = null,
		bool requestSlotAvailable = true)
	{
		ArgumentNullException.ThrowIfNull(defender);

		var defenders = existingDefenders ?? [];
		var existingDefenderIds = defenders.Select(existingDefender => existingDefender.PlayerObjectId).ToHashSet();
		var invitationPlan = _invitationPlanner.CreatePlan(
			defender,
			existingDefenderIds,
			defenderAlliance,
			requestSlotAvailable);
		var status = invitationPlan.Status switch
		{
			VortexDefenderInvitationPlanStatus.AlreadyDefender => VortexDefenderUpdateDefendersPlanStatus.InvitationAlreadyDefender,
			VortexDefenderInvitationPlanStatus.AllianceFull => VortexDefenderUpdateDefendersPlanStatus.InvitationAllianceFull,
			VortexDefenderInvitationPlanStatus.RequestNotStored => VortexDefenderUpdateDefendersPlanStatus.InvitationRequestNotStored,
			_ => VortexDefenderUpdateDefendersPlanStatus.InvitationPlanned,
		};

		return new VortexDefenderUpdateDefendersPlan(
			status,
			VortexDefenderUpdateDefendersPlanStage.Invitation,
			defender.PlayerObjectId,
			defenders,
			defenderAlliance ?? VortexDefenderAllianceSnapshot.Missing,
			invitationPlan,
			AcceptancePlan: null,
			AddPlayerPlan: null,
			JavaSource: "services/vortex/Invasion.updateDefenders");
	}

	public VortexDefenderUpdateDefendersPlan CreateAcceptancePlan(
		VortexDefenderInvitationResponderSnapshot responder,
		IReadOnlyList<VortexDefenderAddPlayerSnapshot>? existingDefenders = null,
		VortexDefenderAllianceSnapshot? defenderAlliance = null)
	{
		ArgumentNullException.ThrowIfNull(responder);

		var defenders = existingDefenders ?? [];
		var alliance = defenderAlliance ?? VortexDefenderAllianceSnapshot.Missing;
		var acceptancePlan = _acceptancePlanner.CreatePlan(responder, alliance);
		var addPlayerPlan = acceptancePlan.WouldAddDefender
			? _addPlayerPlanner.CreatePlan(
				new VortexDefenderAddPlayerSnapshot(
					responder.PlayerObjectId,
					responder.IsInGroup,
					responder.IsInAlliance),
				defenders,
				alliance)
			: null;
		var status = acceptancePlan.Status switch
		{
			VortexDefenderInvitationAcceptancePlanStatus.DefenderAllianceFull => VortexDefenderUpdateDefendersPlanStatus.AcceptanceAllianceFull,
			_ => VortexDefenderUpdateDefendersPlanStatus.AcceptancePlanned,
		};

		return new VortexDefenderUpdateDefendersPlan(
			status,
			VortexDefenderUpdateDefendersPlanStage.Acceptance,
			responder.PlayerObjectId,
			defenders,
			alliance,
			InvitationPlan: null,
			acceptancePlan,
			addPlayerPlan,
			JavaSource: "services/vortex/Invasion.updateDefenders.RequestResponseHandler.acceptRequest -> services/vortex/Invasion.addPlayer(player, false)");
	}
}

public enum VortexDefenderUpdateDefendersPlanStage
{
	Invitation,
	Acceptance,
}

public enum VortexDefenderUpdateDefendersPlanStatus
{
	InvitationAlreadyDefender,
	InvitationAllianceFull,
	InvitationRequestNotStored,
	InvitationPlanned,
	AcceptanceAllianceFull,
	AcceptancePlanned,
}

public sealed record VortexDefenderUpdateDefendersPlan(
	VortexDefenderUpdateDefendersPlanStatus Status,
	VortexDefenderUpdateDefendersPlanStage Stage,
	int PlayerObjectId,
	IReadOnlyList<VortexDefenderAddPlayerSnapshot> ExistingDefenders,
	VortexDefenderAllianceSnapshot DefenderAlliance,
	VortexDefenderInvitationPlan? InvitationPlan,
	VortexDefenderInvitationAcceptancePlan? AcceptancePlan,
	VortexDefenderAddPlayerTransitionPlan? AddPlayerPlan,
	string JavaSource)
{
	public IReadOnlyList<int> ExistingDefenderObjectIds => ExistingDefenders.Select(defender => defender.PlayerObjectId).ToArray();
	public bool HasInvitationPlan => InvitationPlan is not null;
	public bool HasAcceptancePlan => AcceptancePlan is not null;
	public bool HasAddPlayerPlan => AddPlayerPlan is not null;
	public bool WouldInstallRequest => InvitationPlan?.WouldInstallRequest == true;
	public bool HasQuestionWindowIntent => InvitationPlan?.HasQuestionWindowIntent == true;
	public bool WouldRemoveGroup => AcceptancePlan?.WouldRemoveGroup == true;
	public bool WouldRemoveAlliance => AcceptancePlan?.WouldRemoveAlliance == true;
	public bool WouldCallAddPlayer => AcceptancePlan?.WouldAddDefender == true;
	public bool WouldPutParticipant => AddPlayerPlan?.WouldPutParticipant == true;
	public bool WouldWarn => AddPlayerPlan?.WouldWarn == true;
	public bool ShouldMutateLiveRequest => false;
	public bool ShouldSendLivePacket => false;
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveDefenders => false;
}

public sealed class VortexDefenderUpdateDefendersRegistrationRuntimeAdapterService
{
	private readonly VortexDefenderUpdateDefendersPlanService _updateDefendersPlanner = new();
	private readonly VortexDefenderInvitationRequestSlotSnapshotService _requestSlotSnapshotter = new();
	private readonly VortexDefenderInvitationRegistrationRuntimeAdapterService _registrationAdapter = new();

	public VortexDefenderUpdateDefendersRegistrationRuntimeReport RegisterInvitation(
		Player defender,
		IReadOnlyList<VortexDefenderAddPlayerSnapshot>? existingDefenders = null,
		VortexDefenderAllianceSnapshot? defenderAlliance = null,
		int locationId = 0)
	{
		ArgumentNullException.ThrowIfNull(defender);

		var requestSlot = _requestSlotSnapshotter.CreateSnapshot(defender);
		var updatePlan = _updateDefendersPlanner.CreateInvitationPlan(
			VortexZonePlayerSnapshot.FromPlayer(defender),
			existingDefenders,
			defenderAlliance,
			requestSlot.RequestSlotAvailable);
		var registrationReport = _registrationAdapter.Register(
			defender,
			updatePlan.InvitationPlan ?? throw new InvalidOperationException("Update-defenders invitation plan was not created."),
			locationId);

		return new VortexDefenderUpdateDefendersRegistrationRuntimeReport(
			updatePlan.Status,
			updatePlan.Stage,
			defender.ObjectId,
			requestSlot,
			updatePlan,
			registrationReport,
			JavaSource: "services/vortex/Invasion.updateDefenders");
	}
}

public sealed record VortexDefenderUpdateDefendersRegistrationRuntimeReport(
	VortexDefenderUpdateDefendersPlanStatus Status,
	VortexDefenderUpdateDefendersPlanStage Stage,
	int DefenderObjectId,
	VortexDefenderInvitationRequestSlotSnapshot RequestSlot,
	VortexDefenderUpdateDefendersPlan UpdateDefendersPlan,
	VortexDefenderInvitationRegistrationRuntimeReport RegistrationReport,
	string JavaSource)
{
	public bool RequestSlotAvailableBeforeRegistration => RequestSlot.RequestSlotAvailable;
	public bool Registered => RegistrationReport.Registered;
	public bool Rejected => RegistrationReport.Rejected;
	public bool Skipped => RegistrationReport.Skipped;
	public bool AttemptedRequestRegistration => RegistrationReport.AttemptedRequestRegistration;
	public bool RequestStoredByRegistry => RegistrationReport.RequestStoredByRegistry;
	public bool WouldSendQuestionWindow => RegistrationReport.WouldSendQuestionWindow;
	public bool HasPayload => RegistrationReport.HasPayload;
	public bool ShouldSendLivePacket => false;
	public bool ShouldExecuteLiveCallback => false;
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveDefenders => false;
}

public sealed class VortexDefenderQuestionWindowIntentAdapterService
{
	public VortexDefenderQuestionWindowIntent CreateIntent(VortexDefenderUpdateDefendersRegistrationRuntimeReport registrationReport)
	{
		ArgumentNullException.ThrowIfNull(registrationReport);

		var report = registrationReport.RegistrationReport;
		var questionId = report.QuestionId;
		var senderId = report.QuestionWindowSenderId ?? 0;
		var rangeOrCooldownSeconds = report.QuestionWindowRangeOrCooldownSeconds ?? 0;
		if (!registrationReport.WouldSendQuestionWindow)
		{
			return new VortexDefenderQuestionWindowIntent(
				VortexDefenderQuestionWindowIntentStatus.NotCreated,
				registrationReport.DefenderObjectId,
				questionId,
				senderId,
				rangeOrCooldownSeconds,
				QuestionWindow: null,
				registrationReport,
				JavaSource: "services/vortex/Invasion.updateDefenders -> network/aion/serverpackets/SM_QUESTION_WINDOW");
		}

		return new VortexDefenderQuestionWindowIntent(
			VortexDefenderQuestionWindowIntentStatus.Created,
			registrationReport.DefenderObjectId,
			questionId,
			senderId,
			rangeOrCooldownSeconds,
			new SmQuestionWindow(questionId, senderId, rangeOrCooldownSeconds),
			registrationReport,
			JavaSource: "services/vortex/Invasion.updateDefenders -> network/aion/serverpackets/SM_QUESTION_WINDOW");
	}
}

public enum VortexDefenderQuestionWindowIntentStatus
{
	NotCreated,
	Created,
}

public sealed record VortexDefenderQuestionWindowIntent(
	VortexDefenderQuestionWindowIntentStatus Status,
	int RecipientObjectId,
	int QuestionId,
	int SenderObjectId,
	int RangeOrCooldownSeconds,
	SmQuestionWindow? QuestionWindow,
	VortexDefenderUpdateDefendersRegistrationRuntimeReport RegistrationReport,
	string JavaSource)
{
	public bool Created => Status == VortexDefenderQuestionWindowIntentStatus.Created;
	public bool ShouldSendLivePacket => false;
}
