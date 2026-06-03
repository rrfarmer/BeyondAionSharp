namespace Aion.GameServer.Services;

public sealed class VortexDefenderZoneEntryUpdatePlanService
{
	private const string ZoneEntryJavaSource = "model/vortex/VortexLocation.onEnterZone";
	private const string UpdateDefendersJavaSource = "model/vortex/VortexLocation.onEnterZone -> services/vortex/Invasion.updateDefenders";

	private readonly VortexDefenderInvitationPlanService _invitationPlanner = new();

	public VortexDefenderZoneEntryUpdatePlan CreatePlan(
		int locationId,
		VortexZonePlayerSnapshot defender,
		bool hasActiveInvasion,
		IReadOnlySet<int>? existingDefenderObjectIds = null,
		VortexDefenderAllianceSnapshot? defenderAlliance = null,
		bool requestSlotAvailable = true,
		bool isNewZonePlayer = true,
		bool isInvaderRace = false)
	{
		ArgumentNullException.ThrowIfNull(defender);

		var existingDefenders = existingDefenderObjectIds ?? new HashSet<int>();
		var alliance = defenderAlliance ?? VortexDefenderAllianceSnapshot.Missing;

		if (!isNewZonePlayer)
		{
			return CreateGuardPlan(
				VortexDefenderZoneEntryUpdatePlanStatus.NotNewZonePlayer,
				locationId,
				defender,
				existingDefenders,
				alliance,
				requestSlotAvailable,
				isNewZonePlayer,
				hasActiveInvasion,
				isInvaderRace);
		}

		if (!hasActiveInvasion)
		{
			return CreateGuardPlan(
				VortexDefenderZoneEntryUpdatePlanStatus.InactiveVortex,
				locationId,
				defender,
				existingDefenders,
				alliance,
				requestSlotAvailable,
				isNewZonePlayer,
				hasActiveInvasion,
				isInvaderRace);
		}

		if (isInvaderRace)
		{
			return CreateGuardPlan(
				VortexDefenderZoneEntryUpdatePlanStatus.InvaderRace,
				locationId,
				defender,
				existingDefenders,
				alliance,
				requestSlotAvailable,
				isNewZonePlayer,
				hasActiveInvasion,
				isInvaderRace);
		}

		var invitationPlan = _invitationPlanner.CreatePlan(defender, existingDefenders, alliance, requestSlotAvailable);
		var status = invitationPlan.Status switch
		{
			VortexDefenderInvitationPlanStatus.AlreadyDefender => VortexDefenderZoneEntryUpdatePlanStatus.AlreadyDefender,
			VortexDefenderInvitationPlanStatus.AllianceFull => VortexDefenderZoneEntryUpdatePlanStatus.DefenderAllianceFull,
			VortexDefenderInvitationPlanStatus.RequestNotStored => VortexDefenderZoneEntryUpdatePlanStatus.RequestNotStored,
			_ => VortexDefenderZoneEntryUpdatePlanStatus.InvitationPlanned,
		};

		return new VortexDefenderZoneEntryUpdatePlan(
			status,
			locationId,
			defender,
			existingDefenders.ToArray(),
			alliance,
			invitationPlan,
			requestSlotAvailable,
			isNewZonePlayer,
			hasActiveInvasion,
			isInvaderRace,
			WouldCallUpdateDefenders: true,
			UpdateDefendersJavaSource);
	}

	private static VortexDefenderZoneEntryUpdatePlan CreateGuardPlan(
		VortexDefenderZoneEntryUpdatePlanStatus status,
		int locationId,
		VortexZonePlayerSnapshot defender,
		IReadOnlySet<int> existingDefenderObjectIds,
		VortexDefenderAllianceSnapshot defenderAlliance,
		bool requestSlotAvailable,
		bool isNewZonePlayer,
		bool hasActiveInvasion,
		bool isInvaderRace)
	{
		return new VortexDefenderZoneEntryUpdatePlan(
			status,
			locationId,
			defender,
			existingDefenderObjectIds.ToArray(),
			defenderAlliance,
			InvitationPlan: null,
			requestSlotAvailable,
			isNewZonePlayer,
			hasActiveInvasion,
			isInvaderRace,
			WouldCallUpdateDefenders: false,
			ZoneEntryJavaSource);
	}
}

public enum VortexDefenderZoneEntryUpdatePlanStatus
{
	NotNewZonePlayer,
	InactiveVortex,
	InvaderRace,
	AlreadyDefender,
	DefenderAllianceFull,
	RequestNotStored,
	InvitationPlanned,
}

public sealed record VortexDefenderZoneEntryUpdatePlan(
	VortexDefenderZoneEntryUpdatePlanStatus Status,
	int LocationId,
	VortexZonePlayerSnapshot Defender,
	IReadOnlyList<int> ExistingDefenderObjectIds,
	VortexDefenderAllianceSnapshot DefenderAlliance,
	VortexDefenderInvitationPlan? InvitationPlan,
	bool RequestSlotAvailable,
	bool IsNewZonePlayer,
	bool HasActiveInvasion,
	bool IsInvaderRace,
	bool WouldCallUpdateDefenders,
	string JavaSource)
{
	public int PlayerObjectId => Defender.PlayerObjectId;
	public bool HasInvitationPlan => InvitationPlan is not null;
	public bool WouldInstallRequest => InvitationPlan?.WouldInstallRequest == true;
	public bool HasQuestionWindowIntent => InvitationPlan?.HasQuestionWindowIntent == true;
	public bool ShouldRecordZonePlayer => false;
	public bool ShouldMutateLiveRequest => false;
	public bool ShouldSendLivePacket => false;
	public bool ShouldMutateLiveDefenders => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveGroup => false;
}
