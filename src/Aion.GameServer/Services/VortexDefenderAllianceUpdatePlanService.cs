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
