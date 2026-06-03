namespace Aion.GameServer.Services;

public sealed class VortexInvaderKiskZoneMembershipPlanService
{
	public VortexInvaderKiskZoneMembershipPlan CreateEnterPlan(
		VortexKiskZoneSnapshot kisk,
		bool isInvaderRace)
	{
		ArgumentNullException.ThrowIfNull(kisk);

		return new VortexInvaderKiskZoneMembershipPlan(
			isInvaderRace
				? VortexInvaderKiskZoneMembershipPlanStatus.EnterRecordInvaderKisk
				: VortexInvaderKiskZoneMembershipPlanStatus.EnterNonInvaderRace,
			kisk,
			IsInvaderRace: isInvaderRace,
			IsStillInsideLocation: false,
			WouldRecordInvaderKisk: isInvaderRace,
			WouldRemoveInvaderKisk: false,
			JavaSource: "model/vortex/VortexLocation.onEnterZone");
	}

	public VortexInvaderKiskZoneMembershipPlan CreateLeavePlan(
		VortexKiskZoneSnapshot kisk,
		bool isStillInsideLocation)
	{
		ArgumentNullException.ThrowIfNull(kisk);

		return new VortexInvaderKiskZoneMembershipPlan(
			isStillInsideLocation
				? VortexInvaderKiskZoneMembershipPlanStatus.LeaveStillInsideLocation
				: VortexInvaderKiskZoneMembershipPlanStatus.LeaveRemoveInvaderKisk,
			kisk,
			IsInvaderRace: null,
			isStillInsideLocation,
			WouldRecordInvaderKisk: false,
			WouldRemoveInvaderKisk: !isStillInsideLocation,
			JavaSource: "model/vortex/VortexLocation.onLeaveZone");
	}
}

public enum VortexInvaderKiskZoneMembershipPlanStatus
{
	EnterRecordInvaderKisk,
	EnterNonInvaderRace,
	LeaveStillInsideLocation,
	LeaveRemoveInvaderKisk,
}

public sealed record VortexKiskZoneSnapshot(
	int KiskObjectId,
	string Race);

public sealed record VortexInvaderKiskZoneMembershipPlan(
	VortexInvaderKiskZoneMembershipPlanStatus Status,
	VortexKiskZoneSnapshot Kisk,
	bool? IsInvaderRace,
	bool IsStillInsideLocation,
	bool WouldRecordInvaderKisk,
	bool WouldRemoveInvaderKisk,
	string JavaSource)
{
	public int KiskObjectId => Kisk.KiskObjectId;
	public string Race => Kisk.Race;
	public bool ShouldMutateLiveKiskMap => false;
	public bool ShouldKillOrDespawnKisk => false;
}
