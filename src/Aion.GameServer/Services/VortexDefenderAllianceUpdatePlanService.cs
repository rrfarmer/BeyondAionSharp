using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class VortexDefenderAllianceUpdatePlanService
{
	public VortexDefenderAllianceUpdatePlan CreatePlan(
		VortexLocationSummary location,
		IReadOnlyList<VortexZonePlayerSnapshot>? zonePlayers)
	{
		ArgumentNullException.ThrowIfNull(location);

		var players = zonePlayers ?? [];
		var defenders = players
			.Where(player => string.Equals(player.Race, location.DefendersRace, StringComparison.Ordinal))
			.ToArray();
		var skipped = players
			.Where(player => !string.Equals(player.Race, location.DefendersRace, StringComparison.Ordinal))
			.ToArray();

		return new VortexDefenderAllianceUpdatePlan(
			VortexDefenderAllianceUpdatePlanStatus.Planned,
			location.Id,
			location.DefendersRace,
			defenders,
			skipped,
			JavaSource: "services/vortex/Invasion.updateAlliance -> services/vortex/Invasion.updateDefenders");
	}
}

public sealed class VortexDefenderInvitationBatchPlanService
{
	private readonly VortexDefenderUpdateDefendersPlanService _defenderUpdatePlanner = new();

	public VortexDefenderInvitationBatchPlan CreatePlan(
		VortexDefenderAllianceUpdatePlan updatePlan,
		IReadOnlyList<VortexDefenderAddPlayerSnapshot>? existingDefenders = null,
		VortexDefenderAllianceSnapshot? defenderAlliance = null,
		IReadOnlyDictionary<int, bool>? requestSlotsByPlayerObjectId = null,
		bool defaultRequestSlotAvailable = true)
	{
		ArgumentNullException.ThrowIfNull(updatePlan);

		var defenders = existingDefenders ?? [];
		var alliance = defenderAlliance ?? VortexDefenderAllianceSnapshot.Missing;
		var invitationPlans = updatePlan.DefenderUpdatePlayers
			.Select(defender =>
			{
				var requestSlotAvailable = requestSlotsByPlayerObjectId?.TryGetValue(defender.PlayerObjectId, out var available) == true
					? available
					: defaultRequestSlotAvailable;
				return _defenderUpdatePlanner.CreateInvitationPlan(
					defender,
					defenders,
					alliance,
					requestSlotAvailable);
			})
			.ToArray();

		return new VortexDefenderInvitationBatchPlan(
			VortexDefenderInvitationBatchPlanStatus.Planned,
			updatePlan.LocationId,
			updatePlan,
			defenders,
			alliance,
			invitationPlans,
			JavaSource: "services/vortex/Invasion.updateAlliance -> services/vortex/Invasion.updateDefenders");
	}
}

public enum VortexDefenderAllianceUpdatePlanStatus
{
	Planned,
}

public enum VortexDefenderInvitationBatchPlanStatus
{
	Planned,
}

public sealed record VortexDefenderAllianceUpdatePlan(
	VortexDefenderAllianceUpdatePlanStatus Status,
	int LocationId,
	string DefendersRace,
	IReadOnlyList<VortexZonePlayerSnapshot> DefenderUpdatePlayers,
	IReadOnlyList<VortexZonePlayerSnapshot> SkippedPlayers,
	string JavaSource)
{
	public IReadOnlyList<int> DefenderObjectIds => DefenderUpdatePlayers.Select(player => player.PlayerObjectId).ToArray();
	public IReadOnlyList<int> SkippedObjectIds => SkippedPlayers.Select(player => player.PlayerObjectId).ToArray();
	public bool ShouldMutateLiveAlliance => false;
	public bool WouldCallUpdateDefenders => DefenderUpdatePlayers.Count > 0;
}

public sealed record VortexDefenderInvitationBatchPlan(
	VortexDefenderInvitationBatchPlanStatus Status,
	int LocationId,
	VortexDefenderAllianceUpdatePlan UpdatePlan,
	IReadOnlyList<VortexDefenderAddPlayerSnapshot> ExistingDefenders,
	VortexDefenderAllianceSnapshot DefenderAlliance,
	IReadOnlyList<VortexDefenderUpdateDefendersPlan> DefenderInvitationPlans,
	string JavaSource)
{
	public IReadOnlyList<int> DefenderObjectIds => DefenderInvitationPlans.Select(plan => plan.PlayerObjectId).ToArray();
	public IReadOnlyList<int> ExistingDefenderObjectIds => ExistingDefenders.Select(defender => defender.PlayerObjectId).ToArray();
	public int InvitationPlanCount => DefenderInvitationPlans.Count;
	public int QuestionWindowIntentCount => DefenderInvitationPlans.Count(plan => plan.HasQuestionWindowIntent);
	public int RequestNotStoredCount => DefenderInvitationPlans.Count(plan => plan.Status == VortexDefenderUpdateDefendersPlanStatus.InvitationRequestNotStored);
	public int AlreadyDefenderCount => DefenderInvitationPlans.Count(plan => plan.Status == VortexDefenderUpdateDefendersPlanStatus.InvitationAlreadyDefender);
	public int AllianceFullCount => DefenderInvitationPlans.Count(plan => plan.Status == VortexDefenderUpdateDefendersPlanStatus.InvitationAllianceFull);
	public bool WouldCallUpdateDefenders => DefenderInvitationPlans.Count > 0;
	public bool WouldInstallAnyRequest => DefenderInvitationPlans.Any(plan => plan.WouldInstallRequest);
	public bool HasAnyQuestionWindowIntent => DefenderInvitationPlans.Any(plan => plan.HasQuestionWindowIntent);
	public bool ShouldMutateLiveRequest => false;
	public bool ShouldSendLivePacket => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveDefenders => false;
}

public sealed class VortexDefenderInvitationBatchRuntimeAdapterService
{
	private readonly VortexDefenderUpdateDefendersRegistrationRuntimeAdapterService _registrationAdapter = new();
	private readonly VortexDefenderQuestionWindowIntentAdapterService _questionWindowIntentAdapter = new();

	public VortexDefenderInvitationBatchRuntimeReport RegisterInvitations(
		IReadOnlyList<Player>? defenders,
		IReadOnlyList<VortexDefenderAddPlayerSnapshot>? existingDefenders = null,
		VortexDefenderAllianceSnapshot? defenderAlliance = null)
	{
		var defenderPlayers = defenders ?? [];
		var existingDefenderSnapshots = existingDefenders ?? [];
		var alliance = defenderAlliance ?? VortexDefenderAllianceSnapshot.Missing;
		var playerReports = defenderPlayers
			.Select(defender =>
			{
				var registration = _registrationAdapter.RegisterInvitation(
					defender,
					existingDefenderSnapshots,
					alliance);
				var questionWindowIntent = _questionWindowIntentAdapter.CreateIntent(registration);

				return new VortexDefenderInvitationBatchRuntimePlayerReport(
					defender.ObjectId,
					registration,
					questionWindowIntent);
			})
			.ToArray();

		return new VortexDefenderInvitationBatchRuntimeReport(
			VortexDefenderInvitationBatchRuntimeReportStatus.Planned,
			existingDefenderSnapshots,
			alliance,
			playerReports,
			JavaSource: "services/vortex/Invasion.updateAlliance -> services/vortex/Invasion.updateDefenders");
	}
}

public enum VortexDefenderInvitationBatchRuntimeReportStatus
{
	Planned,
}

public sealed record VortexDefenderInvitationBatchRuntimePlayerReport(
	int DefenderObjectId,
	VortexDefenderUpdateDefendersRegistrationRuntimeReport RegistrationReport,
	VortexDefenderQuestionWindowIntent QuestionWindowIntent)
{
	public bool Registered => RegistrationReport.Registered;
	public bool Rejected => RegistrationReport.Rejected;
	public bool Skipped => RegistrationReport.Skipped;
	public bool RequestStoredByRegistry => RegistrationReport.RequestStoredByRegistry;
	public bool HasQuestionWindowIntent => QuestionWindowIntent.Created;
	public bool ShouldSendLivePacket => false;
	public bool ShouldExecuteLiveCallback => false;
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveDefenders => false;
}

public sealed record VortexDefenderInvitationBatchRuntimeReport(
	VortexDefenderInvitationBatchRuntimeReportStatus Status,
	IReadOnlyList<VortexDefenderAddPlayerSnapshot> ExistingDefenders,
	VortexDefenderAllianceSnapshot DefenderAlliance,
	IReadOnlyList<VortexDefenderInvitationBatchRuntimePlayerReport> PlayerReports,
	string JavaSource)
{
	public IReadOnlyList<int> DefenderObjectIds => PlayerReports.Select(report => report.DefenderObjectId).ToArray();
	public IReadOnlyList<int> ExistingDefenderObjectIds => ExistingDefenders.Select(defender => defender.PlayerObjectId).ToArray();
	public int DefenderCount => PlayerReports.Count;
	public int RegisteredCount => PlayerReports.Count(report => report.Registered);
	public int RejectedCount => PlayerReports.Count(report => report.Rejected);
	public int SkippedCount => PlayerReports.Count(report => report.Skipped);
	public int RequestStoredCount => PlayerReports.Count(report => report.RequestStoredByRegistry);
	public int QuestionWindowIntentCount => PlayerReports.Count(report => report.HasQuestionWindowIntent);
	public bool ShouldSendLivePacket => false;
	public bool ShouldExecuteLiveCallback => false;
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveDefenders => false;
}

public sealed record VortexZonePlayerSnapshot(
	int PlayerObjectId,
	string Race,
	bool IsOnline = true)
{
	public static VortexZonePlayerSnapshot FromPlayer(Player player)
	{
		ArgumentNullException.ThrowIfNull(player);
		return new VortexZonePlayerSnapshot(player.ObjectId, player.Race, player.IsOnline);
	}
}
