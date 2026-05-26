using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerKnownListPacketConstructionFactPlanStatus
{
	Complete,
	Blocked,
}

public enum PlayerKnownListPacketConstructionFactBlocker
{
	MissingViewerPlayer,
	MissingSubjectPlayer,
	MissingRideInfo,
	MissingRideAttackSpeedFacts,
	MissingAbnormalEffectFacts,
}

public enum PlayerKnownListPacketConstructionAttackSpeedFactSource
{
	None,
	Supplied,
	ResolvedApproximation,
}

public sealed record PlayerKnownListPacketConstructionAttackSpeedFacts(
	int BaseAttackSpeed,
	int CurrentAttackSpeed);

public sealed record PlayerKnownListPacketConstructionFactPlanRequest(
	Player? ViewerPlayer,
	Player? SubjectPlayer,
	PlayerKnownListOperationSideEffectDirectionFacts DirectionFacts,
	bool ViewerIsEnemyToSubject = false,
	bool EitherPlayerNeutralToAllPlayers = false,
	IReadOnlyList<SmAbnormalEffectEntry>? AbnormalEffects = null,
	int? AbnormalEffectMask = null,
	int AbnormalEffectSlots = SmAbnormalEffect.FullSkillTargetSlots,
	PlayerKnownListPacketConstructionAttackSpeedFacts? RideAttackSpeedFacts = null,
	float? RideMovementSpeed = null,
	PlayerKnownListAttackSpeedFactResolution? RideAttackSpeedResolution = null);

public sealed record PlayerKnownListPacketConstructionFactPlan(
	PlayerKnownListPacketConstructionFactPlanStatus Status,
	PlayerKnownListOperationSideEffectPacketConstructionFacts? Facts,
	IReadOnlyList<PlayerKnownListPacketConstructionFactBlocker> Blockers,
	bool ExecutesLivePackets,
	bool IsLive,
	bool IsJavaControllerParity,
	string JavaSource,
	PlayerKnownListPacketConstructionAttackSpeedFactSource RideAttackSpeedFactSource = PlayerKnownListPacketConstructionAttackSpeedFactSource.None,
	PlayerKnownListAttackSpeedFactResolutionStatus? RideAttackSpeedResolutionStatus = null);

public sealed class PlayerKnownListPacketConstructionFactPlanService
{
	public PlayerKnownListPacketConstructionFactPlan Plan(
		PlayerKnownListPacketConstructionFactPlanRequest request)
	{
		// Java parity breadcrumb: PlayerController.sendPlayerInfoPackets obtains
		// viewer-sensitive SM_PLAYER_INFO facts from AionConnection.activePlayer
		// and subject packet facts from the live Player, motion, ride, stats, and
		// EffectController state. This service only composes supplied snapshots.
		var blockers = new List<PlayerKnownListPacketConstructionFactBlocker>();
		var rideAttackSpeed = ResolveRideAttackSpeed(request);

		if (request.ViewerPlayer is null)
			blockers.Add(PlayerKnownListPacketConstructionFactBlocker.MissingViewerPlayer);

		if (request.SubjectPlayer is null)
			blockers.Add(PlayerKnownListPacketConstructionFactBlocker.MissingSubjectPlayer);

		if (request.DirectionFacts.SubjectIsInRideMode)
		{
			if (request.SubjectPlayer?.RideInfo is null)
				blockers.Add(PlayerKnownListPacketConstructionFactBlocker.MissingRideInfo);

			if (rideAttackSpeed.Facts is null)
				blockers.Add(PlayerKnownListPacketConstructionFactBlocker.MissingRideAttackSpeedFacts);
		}

		if (request.DirectionFacts.SubjectHasAbnormalEffects
			&& (request.AbnormalEffects is null || request.AbnormalEffectMask is null))
		{
			blockers.Add(PlayerKnownListPacketConstructionFactBlocker.MissingAbnormalEffectFacts);
		}

		if (blockers.Count > 0 || request.ViewerPlayer is null || request.SubjectPlayer is null)
		{
			return CreatePlan(
				PlayerKnownListPacketConstructionFactPlanStatus.Blocked,
				Facts: null,
				blockers,
				rideAttackSpeed.Source,
				rideAttackSpeed.ResolutionStatus);
		}

		var viewerContext = new SmPlayerInfoViewerContext(
			request.ViewerPlayer.Race,
			request.ViewerIsEnemyToSubject,
			request.EitherPlayerNeutralToAllPlayers);
		var rideMovementSpeed = request.DirectionFacts.SubjectIsInRideMode
			? request.RideMovementSpeed ?? PlayerMovementSpeedResolver.ResolveKnownMovementSpeed(request.SubjectPlayer)
			: 0;
		var facts = new PlayerKnownListOperationSideEffectPacketConstructionFacts(
			request.SubjectPlayer,
			request.SubjectPlayer.Motions.Where(motion => motion.IsActive).ToArray(),
			viewerContext,
			request.AbnormalEffects,
			request.AbnormalEffectMask ?? 0,
			request.AbnormalEffectSlots,
			rideMovementSpeed,
			rideAttackSpeed.Facts?.BaseAttackSpeed ?? 0,
			rideAttackSpeed.Facts?.CurrentAttackSpeed ?? 0);

		return CreatePlan(
			PlayerKnownListPacketConstructionFactPlanStatus.Complete,
			facts,
			blockers,
			rideAttackSpeed.Source,
			rideAttackSpeed.ResolutionStatus);
	}

	private static PlayerKnownListPacketConstructionFactPlan CreatePlan(
		PlayerKnownListPacketConstructionFactPlanStatus status,
		PlayerKnownListOperationSideEffectPacketConstructionFacts? Facts,
		IReadOnlyList<PlayerKnownListPacketConstructionFactBlocker> blockers,
		PlayerKnownListPacketConstructionAttackSpeedFactSource rideAttackSpeedFactSource,
		PlayerKnownListAttackSpeedFactResolutionStatus? rideAttackSpeedResolutionStatus) =>
		new(
			status,
			Facts,
			blockers,
			ExecutesLivePackets: false,
			IsLive: false,
			IsJavaControllerParity: false,
			"Non-live fact planner for com.aionemu.gameserver.controllers.PlayerController.sendPlayerInfoPackets and see/notSee packet construction; does not execute live known-list callbacks or send packets.",
			rideAttackSpeedFactSource,
			rideAttackSpeedResolutionStatus);

	private static ResolvedRideAttackSpeed ResolveRideAttackSpeed(
		PlayerKnownListPacketConstructionFactPlanRequest request)
	{
		if (!request.DirectionFacts.SubjectIsInRideMode)
		{
			return new ResolvedRideAttackSpeed(
				Facts: null,
				PlayerKnownListPacketConstructionAttackSpeedFactSource.None,
				request.RideAttackSpeedResolution?.Status);
		}

		// Supplied packet facts remain authoritative. The disabled resolver result
		// is consumed only when callers opt in by providing an explicit result.
		if (request.RideAttackSpeedFacts is { } suppliedFacts)
		{
			return new ResolvedRideAttackSpeed(
				suppliedFacts,
				PlayerKnownListPacketConstructionAttackSpeedFactSource.Supplied,
				request.RideAttackSpeedResolution?.Status);
		}

		if (request.RideAttackSpeedResolution is { Facts: { } resolvedFacts } resolution)
		{
			return new ResolvedRideAttackSpeed(
				resolvedFacts,
				PlayerKnownListPacketConstructionAttackSpeedFactSource.ResolvedApproximation,
				resolution.Status);
		}

		return new ResolvedRideAttackSpeed(
			Facts: null,
			PlayerKnownListPacketConstructionAttackSpeedFactSource.None,
			request.RideAttackSpeedResolution?.Status);
	}

	private sealed record ResolvedRideAttackSpeed(
		PlayerKnownListPacketConstructionAttackSpeedFacts? Facts,
		PlayerKnownListPacketConstructionAttackSpeedFactSource Source,
		PlayerKnownListAttackSpeedFactResolutionStatus? ResolutionStatus);
}
