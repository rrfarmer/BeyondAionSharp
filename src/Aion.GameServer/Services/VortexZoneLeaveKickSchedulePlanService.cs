namespace Aion.GameServer.Services;

public sealed class VortexZoneLeaveKickSchedulePlanService
{
	public const int BattlefieldLeftMessageId = 904305;
	public const int KickDelaySeconds = 10;

	private const string ZoneLeaveJavaSource = "model/vortex/VortexLocation.onLeaveZone";
	private const string KickScheduleJavaSource = "model/vortex/VortexLocation.onLeaveZone -> ThreadPoolManager.schedule -> services/vortex/Invasion.kickPlayer";

	public VortexZoneLeaveKickSchedulePlan CreatePlan(
		int locationId,
		VortexZonePlayerSnapshot player,
		bool hasActiveInvasion,
		bool isInvaderRace,
		IReadOnlySet<int>? passedPlayerObjectIds = null,
		bool isStillInsideLocation = false)
	{
		ArgumentNullException.ThrowIfNull(player);

		var passedPlayers = passedPlayerObjectIds?.ToArray() ?? [];
		var hadPassedPortal = passedPlayers.Contains(player.PlayerObjectId);
		if (isStillInsideLocation)
		{
			return CreateGuardPlan(
				VortexZoneLeaveKickSchedulePlanStatus.StillInsideLocation,
				locationId,
				player,
				hasActiveInvasion,
				isInvaderRace,
				passedPlayers,
				hadPassedPortal,
				WouldRemoveZonePlayer: false);
		}

		if (!hasActiveInvasion)
		{
			return CreateGuardPlan(
				VortexZoneLeaveKickSchedulePlanStatus.InactiveVortex,
				locationId,
				player,
				hasActiveInvasion,
				isInvaderRace,
				passedPlayers,
				hadPassedPortal,
				WouldRemoveZonePlayer: true);
		}

		if (isInvaderRace && !hadPassedPortal)
		{
			return CreateGuardPlan(
				VortexZoneLeaveKickSchedulePlanStatus.InvaderMissingPassedPlayer,
				locationId,
				player,
				hasActiveInvasion,
				isInvaderRace,
				passedPlayers,
				hadPassedPortal,
				WouldRemoveZonePlayer: true);
		}

		var status = isInvaderRace
			? VortexZoneLeaveKickSchedulePlanStatus.InvaderKickScheduled
			: VortexZoneLeaveKickSchedulePlanStatus.DefenderKickScheduled;
		return new VortexZoneLeaveKickSchedulePlan(
			status,
			locationId,
			player,
			passedPlayers,
			isStillInsideLocation,
			hasActiveInvasion,
			isInvaderRace,
			hadPassedPortal,
			WouldRemoveZonePlayer: true,
			WouldSendBattlefieldLeftMessage: isInvaderRace,
			BattlefieldLeftMessageId: isInvaderRace ? BattlefieldLeftMessageId : null,
			WouldScheduleKick: true,
			ScheduledKickIsInvader: isInvaderRace,
			ScheduledKickDelaySeconds: KickDelaySeconds,
			ScheduledKickRequiresOnline: true,
			ScheduledKickRequiresOutsideActiveVortex: true,
			KickScheduleJavaSource);
	}

	private static VortexZoneLeaveKickSchedulePlan CreateGuardPlan(
		VortexZoneLeaveKickSchedulePlanStatus status,
		int locationId,
		VortexZonePlayerSnapshot player,
		bool hasActiveInvasion,
		bool isInvaderRace,
		IReadOnlyList<int> passedPlayerObjectIds,
		bool hadPassedPortal,
		bool WouldRemoveZonePlayer)
	{
		return new VortexZoneLeaveKickSchedulePlan(
			status,
			locationId,
			player,
			passedPlayerObjectIds,
			IsStillInsideLocation: status == VortexZoneLeaveKickSchedulePlanStatus.StillInsideLocation,
			hasActiveInvasion,
			isInvaderRace,
			hadPassedPortal,
			WouldRemoveZonePlayer,
			WouldSendBattlefieldLeftMessage: false,
			BattlefieldLeftMessageId: null,
			WouldScheduleKick: false,
			ScheduledKickIsInvader: null,
			ScheduledKickDelaySeconds: null,
			ScheduledKickRequiresOnline: false,
			ScheduledKickRequiresOutsideActiveVortex: false,
			ZoneLeaveJavaSource);
	}
}

public enum VortexZoneLeaveKickSchedulePlanStatus
{
	StillInsideLocation,
	InactiveVortex,
	InvaderMissingPassedPlayer,
	InvaderKickScheduled,
	DefenderKickScheduled,
}

public sealed record VortexZoneLeaveKickSchedulePlan(
	VortexZoneLeaveKickSchedulePlanStatus Status,
	int LocationId,
	VortexZonePlayerSnapshot Player,
	IReadOnlyList<int> PassedPlayerObjectIds,
	bool IsStillInsideLocation,
	bool HasActiveInvasion,
	bool IsInvaderRace,
	bool HadPassedPortal,
	bool WouldRemoveZonePlayer,
	bool WouldSendBattlefieldLeftMessage,
	int? BattlefieldLeftMessageId,
	bool WouldScheduleKick,
	bool? ScheduledKickIsInvader,
	int? ScheduledKickDelaySeconds,
	bool ScheduledKickRequiresOnline,
	bool ScheduledKickRequiresOutsideActiveVortex,
	string JavaSource)
{
	public int PlayerObjectId => Player.PlayerObjectId;
	public bool ShouldMutateLiveZonePlayers => false;
	public bool ShouldSendLivePacket => false;
	public bool ShouldScheduleLiveKick => false;
	public bool ShouldMutateLiveParticipants => false;
}
