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
	float? RideMovementSpeed = null);

public sealed record PlayerKnownListPacketConstructionFactPlan(
	PlayerKnownListPacketConstructionFactPlanStatus Status,
	PlayerKnownListOperationSideEffectPacketConstructionFacts? Facts,
	IReadOnlyList<PlayerKnownListPacketConstructionFactBlocker> Blockers,
	bool ExecutesLivePackets,
	bool IsLive,
	bool IsJavaControllerParity,
	string JavaSource);

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

		if (request.ViewerPlayer is null)
			blockers.Add(PlayerKnownListPacketConstructionFactBlocker.MissingViewerPlayer);

		if (request.SubjectPlayer is null)
			blockers.Add(PlayerKnownListPacketConstructionFactBlocker.MissingSubjectPlayer);

		if (request.DirectionFacts.SubjectIsInRideMode)
		{
			if (request.SubjectPlayer?.RideInfo is null)
				blockers.Add(PlayerKnownListPacketConstructionFactBlocker.MissingRideInfo);

			if (request.RideAttackSpeedFacts is null)
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
				blockers);
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
			request.RideAttackSpeedFacts?.BaseAttackSpeed ?? 0,
			request.RideAttackSpeedFacts?.CurrentAttackSpeed ?? 0);

		return CreatePlan(
			PlayerKnownListPacketConstructionFactPlanStatus.Complete,
			facts,
			blockers);
	}

	private static PlayerKnownListPacketConstructionFactPlan CreatePlan(
		PlayerKnownListPacketConstructionFactPlanStatus status,
		PlayerKnownListOperationSideEffectPacketConstructionFacts? Facts,
		IReadOnlyList<PlayerKnownListPacketConstructionFactBlocker> blockers) =>
		new(
			status,
			Facts,
			blockers,
			ExecutesLivePackets: false,
			IsLive: false,
			IsJavaControllerParity: false,
			"Non-live fact planner for com.aionemu.gameserver.controllers.PlayerController.sendPlayerInfoPackets and see/notSee packet construction; does not execute live known-list callbacks or send packets.");
}
