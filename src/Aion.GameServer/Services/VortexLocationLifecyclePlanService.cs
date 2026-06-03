namespace Aion.GameServer.Services;

public sealed class VortexLocationLifecyclePlanService
{
	private readonly VortexInvaderKiskZoneMembershipPlanService _kiskPlanner = new();
	private readonly VortexInvaderPassedPortalUpdatePlanService _invaderEnterPlanner = new();
	private readonly VortexDefenderZoneEntryUpdatePlanService _defenderEnterPlanner = new();
	private readonly VortexZoneLeaveKickSchedulePlanService _leavePlanner = new();

	public VortexLocationLifecyclePlan CreateEnterPlan(
		int locationId,
		VortexLocationLifecycleCreatureKind creatureKind,
		int objectId,
		string race,
		bool isInvaderRace,
		bool hasActiveInvasion,
		bool isNewZonePlayer = true,
		bool isInGroup = false,
		bool isInAlliance = false,
		IReadOnlySet<int>? passedPlayerObjectIds = null,
		IReadOnlyList<VortexInvaderUpdatePlayerSnapshot>? existingInvaders = null,
		VortexInvaderAllianceSnapshot? invaderAlliance = null,
		IReadOnlySet<int>? existingDefenderObjectIds = null,
		VortexDefenderAllianceSnapshot? defenderAlliance = null,
		bool requestSlotAvailable = true)
	{
		if (creatureKind == VortexLocationLifecycleCreatureKind.Kisk)
		{
			var kisk = new VortexKiskZoneSnapshot(objectId, race);
			return new VortexLocationLifecyclePlan(
				VortexLocationLifecyclePlanStatus.EnterKisk,
				VortexLocationLifecycleEventKind.Enter,
				creatureKind,
				locationId,
				objectId,
				race,
				isInvaderRace,
				IsNewZonePlayer: false,
				IsStillInsideLocation: false,
				hasActiveInvasion,
				KiskPlan: _kiskPlanner.CreateEnterPlan(kisk, isInvaderRace),
				InvaderEnterPlan: null,
				DefenderEnterPlan: null,
				LeavePlan: null,
				WouldRecordZonePlayer: false,
				WouldRemoveZonePlayer: false,
				JavaSource: "model/vortex/VortexLocation.onEnterZone");
		}

		if (creatureKind != VortexLocationLifecycleCreatureKind.Player)
		{
			return CreateIgnoredPlan(
				VortexLocationLifecyclePlanStatus.EnterIgnoredCreature,
				VortexLocationLifecycleEventKind.Enter,
				creatureKind,
				locationId,
				objectId,
				race,
				isInvaderRace,
				isNewZonePlayer,
				isStillInsideLocation: false,
				hasActiveInvasion,
				javaSource: "model/vortex/VortexLocation.onEnterZone");
		}

		if (isInvaderRace)
		{
			var invader = new VortexInvaderUpdatePlayerSnapshot(objectId, isInGroup, isInAlliance);
			var invaderPlan = _invaderEnterPlanner.CreatePlan(
				locationId,
				invader,
				hasActiveInvasion,
				passedPlayerObjectIds,
				existingInvaders,
				invaderAlliance,
				isNewZonePlayer,
				isInvaderRace);
			return new VortexLocationLifecyclePlan(
				VortexLocationLifecyclePlanStatus.EnterInvaderPlayer,
				VortexLocationLifecycleEventKind.Enter,
				creatureKind,
				locationId,
				objectId,
				race,
				isInvaderRace,
				isNewZonePlayer,
				IsStillInsideLocation: false,
				hasActiveInvasion,
				KiskPlan: null,
				InvaderEnterPlan: invaderPlan,
				DefenderEnterPlan: null,
				LeavePlan: null,
				WouldRecordZonePlayer: isNewZonePlayer,
				WouldRemoveZonePlayer: false,
				JavaSource: "model/vortex/VortexLocation.onEnterZone");
		}

		var defender = new VortexZonePlayerSnapshot(objectId, race);
		var defenderPlan = _defenderEnterPlanner.CreatePlan(
			locationId,
			defender,
			hasActiveInvasion,
			existingDefenderObjectIds,
			defenderAlliance,
			requestSlotAvailable,
			isNewZonePlayer,
			isInvaderRace);
		return new VortexLocationLifecyclePlan(
			VortexLocationLifecyclePlanStatus.EnterDefenderPlayer,
			VortexLocationLifecycleEventKind.Enter,
			creatureKind,
			locationId,
			objectId,
			race,
			isInvaderRace,
			isNewZonePlayer,
			IsStillInsideLocation: false,
			hasActiveInvasion,
			KiskPlan: null,
			InvaderEnterPlan: null,
			DefenderEnterPlan: defenderPlan,
			LeavePlan: null,
			WouldRecordZonePlayer: isNewZonePlayer,
			WouldRemoveZonePlayer: false,
			JavaSource: "model/vortex/VortexLocation.onEnterZone");
	}

	public VortexLocationLifecyclePlan CreateLeavePlan(
		int locationId,
		VortexLocationLifecycleCreatureKind creatureKind,
		int objectId,
		string race,
		bool isInvaderRace,
		bool hasActiveInvasion,
		bool isStillInsideLocation,
		IReadOnlySet<int>? passedPlayerObjectIds = null,
		bool isOnline = true)
	{
		if (creatureKind == VortexLocationLifecycleCreatureKind.Kisk)
		{
			var kisk = new VortexKiskZoneSnapshot(objectId, race);
			var kiskPlan = _kiskPlanner.CreateLeavePlan(kisk, isStillInsideLocation);
			return new VortexLocationLifecyclePlan(
				VortexLocationLifecyclePlanStatus.LeaveKisk,
				VortexLocationLifecycleEventKind.Leave,
				creatureKind,
				locationId,
				objectId,
				race,
				isInvaderRace,
				IsNewZonePlayer: false,
				IsStillInsideLocation: isStillInsideLocation,
				hasActiveInvasion,
				KiskPlan: kiskPlan,
				InvaderEnterPlan: null,
				DefenderEnterPlan: null,
				LeavePlan: null,
				WouldRecordZonePlayer: false,
				WouldRemoveZonePlayer: false,
				JavaSource: "model/vortex/VortexLocation.onLeaveZone");
		}

		if (creatureKind != VortexLocationLifecycleCreatureKind.Player)
		{
			return CreateIgnoredPlan(
				VortexLocationLifecyclePlanStatus.LeaveIgnoredCreature,
				VortexLocationLifecycleEventKind.Leave,
				creatureKind,
				locationId,
				objectId,
				race,
				isInvaderRace,
				isNewZonePlayer: false,
				isStillInsideLocation,
				hasActiveInvasion,
				javaSource: "model/vortex/VortexLocation.onLeaveZone");
		}

		var player = new VortexZonePlayerSnapshot(objectId, race, isOnline);
		var leavePlan = _leavePlanner.CreatePlan(
			locationId,
			player,
			hasActiveInvasion,
			isInvaderRace,
			passedPlayerObjectIds,
			isStillInsideLocation);
		return new VortexLocationLifecyclePlan(
			VortexLocationLifecyclePlanStatus.LeavePlayer,
			VortexLocationLifecycleEventKind.Leave,
			creatureKind,
			locationId,
			objectId,
			race,
			isInvaderRace,
			IsNewZonePlayer: false,
			isStillInsideLocation,
			hasActiveInvasion,
			KiskPlan: null,
			InvaderEnterPlan: null,
			DefenderEnterPlan: null,
			LeavePlan: leavePlan,
			WouldRecordZonePlayer: false,
			WouldRemoveZonePlayer: leavePlan.WouldRemoveZonePlayer,
			JavaSource: "model/vortex/VortexLocation.onLeaveZone");
	}

	private static VortexLocationLifecyclePlan CreateIgnoredPlan(
		VortexLocationLifecyclePlanStatus status,
		VortexLocationLifecycleEventKind eventKind,
		VortexLocationLifecycleCreatureKind creatureKind,
		int locationId,
		int objectId,
		string race,
		bool isInvaderRace,
		bool isNewZonePlayer,
		bool isStillInsideLocation,
		bool hasActiveInvasion,
		string javaSource)
	{
		return new VortexLocationLifecyclePlan(
			status,
			eventKind,
			creatureKind,
			locationId,
			objectId,
			race,
			isInvaderRace,
			isNewZonePlayer,
			isStillInsideLocation,
			hasActiveInvasion,
			KiskPlan: null,
			InvaderEnterPlan: null,
			DefenderEnterPlan: null,
			LeavePlan: null,
			WouldRecordZonePlayer: false,
			WouldRemoveZonePlayer: false,
			javaSource);
	}
}

public enum VortexLocationLifecycleEventKind
{
	Enter,
	Leave,
}

public enum VortexLocationLifecycleCreatureKind
{
	Other,
	Kisk,
	Player,
}

public enum VortexLocationLifecyclePlanStatus
{
	EnterIgnoredCreature,
	EnterKisk,
	EnterInvaderPlayer,
	EnterDefenderPlayer,
	LeaveIgnoredCreature,
	LeaveKisk,
	LeavePlayer,
}

public sealed record VortexLocationLifecyclePlan(
	VortexLocationLifecyclePlanStatus Status,
	VortexLocationLifecycleEventKind EventKind,
	VortexLocationLifecycleCreatureKind CreatureKind,
	int LocationId,
	int ObjectId,
	string Race,
	bool IsInvaderRace,
	bool IsNewZonePlayer,
	bool IsStillInsideLocation,
	bool HasActiveInvasion,
	VortexInvaderKiskZoneMembershipPlan? KiskPlan,
	VortexInvaderPassedPortalUpdatePlan? InvaderEnterPlan,
	VortexDefenderZoneEntryUpdatePlan? DefenderEnterPlan,
	VortexZoneLeaveKickSchedulePlan? LeavePlan,
	bool WouldRecordZonePlayer,
	bool WouldRemoveZonePlayer,
	string JavaSource)
{
	public bool HasKiskPlan => KiskPlan is not null;
	public bool HasInvaderEnterPlan => InvaderEnterPlan is not null;
	public bool HasDefenderEnterPlan => DefenderEnterPlan is not null;
	public bool HasLeavePlan => LeavePlan is not null;
	public bool ShouldMutateLiveZonePlayers => false;
	public bool ShouldMutateLiveKiskMap => false;
	public bool ShouldMutateLiveParticipants => false;
	public bool ShouldMutateLiveRequests => false;
	public bool ShouldSendLivePacket => false;
	public bool ShouldScheduleLiveTask => false;
}
