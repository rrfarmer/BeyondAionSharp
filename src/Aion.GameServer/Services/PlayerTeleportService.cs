using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class PlayerTeleportService
{
	public static PlayerTeleportResult TeleportToKiskPosition(Player player, WorldPosition destination)
	{
		// Java parity: services/teleport/TeleportService.teleportTo(Player, WorldPosition) from PlayerReviveService.kiskRevive.
		var previousPosition = player.Position;
		player.Position = destination;
		ResetMovementToDestination(player, destination);
		return new PlayerTeleportResult(previousPosition, destination, UsesSameWorldSpawnPath: previousPosition.WorldId == destination.WorldId);
	}

	public static PlayerTeleportResult TeleportWithinSameInstance(Player player, WorldPosition destination)
	{
		// Java parity: services/teleport/TeleportService.teleportTo same map + same instance runs SpawnTask immediately with TeleportAnimation.NONE.
		var previousPosition = player.Position;
		player.Position = destination;
		player.PortAnimation = TeleportAnimation.None.DefaultArrivalAnimation;
		ResetMovementToDestination(player, destination);
		return new PlayerTeleportResult(
			previousPosition,
			destination,
			UsesSameWorldSpawnPath: previousPosition.WorldId == destination.WorldId
				&& previousPosition.InstanceId == destination.InstanceId);
	}

	public static PendingPlayerTeleport QueuePendingTeleport(
		Player player,
		WorldPosition destination,
		TeleportAnimation? animation = null)
	{
		// Java parity: services/teleport/TeleportService.sendLoc stores SpawnTask under TaskId.TELEPORT until the client sends CM_TELEPORT_ANIMATION_DONE.
		var pendingTeleport = new PendingPlayerTeleport(destination, animation ?? TeleportAnimation.FadeOutBeam);
		player.PendingTeleport = pendingTeleport;
		return pendingTeleport;
	}

	public static PlayerTeleportResult? CompletePendingTeleport(Player player)
	{
		// Java parity: CM_TELEPORT_ANIMATION_DONE.runImpl getAndRemoveTask(TaskId.TELEPORT), then run the pending SpawnTask at most once.
		var pendingTeleport = player.PendingTeleport;
		if (pendingTeleport == null)
			return null;

		player.PendingTeleport = null;
		var previousPosition = player.Position;
		player.Position = pendingTeleport.Destination;
		player.PortAnimation = pendingTeleport.Animation.DefaultArrivalAnimation;
		ResetMovementToDestination(player, pendingTeleport.Destination);
		return new PlayerTeleportResult(
			previousPosition,
			pendingTeleport.Destination,
			UsesSameWorldSpawnPath: previousPosition.WorldId == pendingTeleport.Destination.WorldId
				&& previousPosition.InstanceId == pendingTeleport.Destination.InstanceId);
	}

	public static PendingPlayerTeleport? CancelPendingTeleport(Player player)
	{
		// Java parity: CM_TELEPORT_ANIMATION_DONE consumes TaskId.TELEPORT even when SpawnTask falls back without moving.
		var pendingTeleport = player.PendingTeleport;
		player.PendingTeleport = null;
		return pendingTeleport;
	}

	private static void ResetMovementToDestination(Player player, WorldPosition destination)
	{
		// Java parity breadcrumb: World.setPosition updates the authoritative position before spawn packets are sent.
		var movement = player.Movement;
		movement.Mask = MovementMask.Immediate;
		movement.SetNewDirection(destination.X, destination.Y, destination.Z);
		movement.VectorX = 0;
		movement.VectorY = 0;
		movement.VectorZ = 0;
		movement.GlideFlag = GlideFlag.None;
		movement.GeyserLocationId = 0;
		movement.VehicleUnk1 = 0;
		movement.VehicleUnk2 = 0;
		movement.VehicleX = 0;
		movement.VehicleY = 0;
		movement.VehicleZ = 0;
		movement.IsJumping = false;
		movement.FlightDistance = 0;
	}
}

public sealed record PlayerTeleportResult(
	WorldPosition PreviousPosition,
	WorldPosition Destination,
	bool UsesSameWorldSpawnPath);

public sealed record PendingTeleportRequestResult(
	PendingPlayerTeleport PendingTeleport,
	GameServerPacket Packet);

public sealed record InstancePortalTransferResult(
	PendingTeleportRequestResult Teleport,
	InstanceEntranceCooldownResult Cooldown);

public sealed record AllocatedInstancePortalTransferResult(
	InstancePortalRuntimePlan RuntimePlan,
	InstancePortalTransferResult Transfer);

public sealed record PortalContinueTransferResult(
	PortalContinueTransferKind Kind,
	PendingTeleportRequestResult? Teleport,
	InstanceEntranceCooldownResult? Cooldown,
	InstancePortalRuntimePlan? AllocatedRuntimePlan,
	WorldMapInstanceRuntimeState? RegisteredInstance,
	PortalTeamEntryPlan? TeamPlan,
	GroupPortalTransferPlan? GroupTransferPlan)
{
	public static PortalContinueTransferResult OpenWorld(PendingTeleportRequestResult teleport)
	{
		return new PortalContinueTransferResult(
			PortalContinueTransferKind.OpenWorld,
			teleport,
			null,
			null,
			null,
			null,
			null);
	}

	public static PortalContinueTransferResult FromRegisteredInstance(
		InstancePortalTransferResult transfer,
		WorldMapInstanceRuntimeState instance)
	{
		return new PortalContinueTransferResult(
			PortalContinueTransferKind.RegisteredInstance,
			transfer.Teleport,
			transfer.Cooldown,
			null,
			instance,
			null,
			null);
	}

	public static PortalContinueTransferResult AllocatedInstance(AllocatedInstancePortalTransferResult transfer)
	{
		return new PortalContinueTransferResult(
			PortalContinueTransferKind.AllocatedInstance,
			transfer.Transfer.Teleport,
			transfer.Transfer.Cooldown,
			transfer.RuntimePlan,
			null,
			null,
			null);
	}

	public static PortalContinueTransferResult UnsupportedTeamPortal(
		PortalTeamEntryPlan teamPlan,
		PortalLocSummary? portalLoc = null,
		int? playerObjectId = null)
	{
		return new PortalContinueTransferResult(
			PortalContinueTransferKind.UnsupportedTeamPortal,
			null,
			null,
			null,
			teamPlan.RegisteredInstance,
			teamPlan,
			GroupPortalTransferPlan.FromTeamPlan(teamPlan, portalLoc, playerObjectId));
	}
}

public enum PortalContinueTransferKind
{
	OpenWorld,
	RegisteredInstance,
	AllocatedInstance,
	UnsupportedTeamPortal,
}

public sealed record GroupPortalTransferPlan(
	int TeamId,
	IReadOnlyList<int> MemberObjectIds,
	int MaxPlayers,
	GroupPortalTransferState State,
	WorldMapInstanceRuntimeState? RegisteredInstance,
	GroupPortalTransferBlockedReason BlockedReason,
	GroupPortalMemberInstanceScanPlan MemberInstanceScanPlan,
	GroupPortalCapacityPlan CapacityPlan,
	GroupPortalAllocationPlan AllocationPlan,
	GroupPortalExecutionPlan ExecutionPlan)
{
	public static GroupPortalTransferPlan? FromTeamPlan(
		PortalTeamEntryPlan teamPlan,
		PortalLocSummary? portalLoc = null,
		int? playerObjectId = null)
	{
		if (teamPlan.Kind != PortalTeamEntryKind.Group)
			return null;

		// Java parity: services/teleport/PortalService.port group branch after checkAndRemoveRequiredItems.
		if (teamPlan.TeamId <= 0)
		{
			return new GroupPortalTransferPlan(
				teamPlan.TeamId,
				teamPlan.MemberObjectIds,
				teamPlan.MaxPlayers,
				GroupPortalTransferState.InvalidTeamId,
				null,
				GroupPortalTransferBlockedReason.MissingTeamId,
				CreateMemberInstanceScanPlan(
					teamPlan,
					GroupPortalMemberInstanceScanState.BlockedInvalidTeamId,
					GroupPortalMemberInstanceScanBlockedReason.MissingTeamId),
				CreateCapacityPlan(
					teamPlan,
					GroupPortalCapacityState.BlockedInvalidTeamId,
					GroupPortalCapacityBlockedReason.MissingTeamId),
				CreateAllocationPlan(
					teamPlan,
					portalLoc,
					GroupPortalAllocationState.BlockedInvalidTeamId,
					GroupPortalAllocationBlockedReason.MissingTeamId),
				CreateExecutionPlan(
					teamPlan,
					portalLoc,
					playerObjectId,
					GroupPortalExecutionState.BlockedInvalidTeamId,
					GroupPortalExecutionBlockedReason.MissingTeamId));
		}

		var state = teamPlan.RegisteredInstance == null
			? GroupPortalTransferState.FreshInstanceAllocationNeeded
			: GroupPortalTransferState.RegisteredInstanceTransfer;
		return new GroupPortalTransferPlan(
			teamPlan.TeamId,
			teamPlan.MemberObjectIds,
			teamPlan.MaxPlayers,
			state,
			teamPlan.RegisteredInstance,
			GroupPortalTransferBlockedReason.GroupFanoutNotImplemented,
			CreateMemberInstanceScanPlan(teamPlan, state),
			CreateCapacityPlan(teamPlan, state),
			CreateAllocationPlan(teamPlan, portalLoc, state),
			CreateExecutionPlan(teamPlan, portalLoc, playerObjectId, state));
	}

	private static GroupPortalMemberInstanceScanPlan CreateMemberInstanceScanPlan(
		PortalTeamEntryPlan teamPlan,
		GroupPortalTransferState transferState)
	{
		if (transferState == GroupPortalTransferState.RegisteredInstanceTransfer)
		{
			return CreateMemberInstanceScanPlan(
				teamPlan,
				GroupPortalMemberInstanceScanState.NotNeededRegisteredTeamInstance,
				GroupPortalMemberInstanceScanBlockedReason.RegisteredTeamInstanceAlreadyResolved);
		}

		if (teamPlan.MemberObjectIds.Count == 0)
		{
			return CreateMemberInstanceScanPlan(
				teamPlan,
				GroupPortalMemberInstanceScanState.BlockedNoMemberCandidates,
				GroupPortalMemberInstanceScanBlockedReason.NoMemberObjectIds);
		}

		// Java parity: PortalService.port can scan group.getMembers() for member solo registrations when instanceGroupReq is false.
		return CreateMemberInstanceScanPlan(
			teamPlan,
			GroupPortalMemberInstanceScanState.WouldScanMemberObjectIds,
			GroupPortalMemberInstanceScanBlockedReason.LiveGroupAggregateNotPorted);
	}

	private static GroupPortalMemberInstanceScanPlan CreateMemberInstanceScanPlan(
		PortalTeamEntryPlan teamPlan,
		GroupPortalMemberInstanceScanState state,
		GroupPortalMemberInstanceScanBlockedReason blockedReason)
	{
		var candidates = state == GroupPortalMemberInstanceScanState.WouldScanMemberObjectIds
			? teamPlan.MemberObjectIds
			: Array.Empty<int>();
		return new GroupPortalMemberInstanceScanPlan(candidates, state, blockedReason);
	}

	private static GroupPortalCapacityPlan CreateCapacityPlan(
		PortalTeamEntryPlan teamPlan,
		GroupPortalTransferState transferState)
	{
		if (transferState == GroupPortalTransferState.RegisteredInstanceTransfer
			&& teamPlan.RegisteredInstance != null)
		{
			// Java parity: PortalService.port checks instance.getPlayersInside().size() < maxPlayers before transfer.
			var playerCount = teamPlan.RegisteredInstance.PlayerCount;
			var state = playerCount < teamPlan.MaxPlayers
				? GroupPortalCapacityState.WouldPassCapacityGuard
				: GroupPortalCapacityState.WouldFailCapacityGuard;
			var reason = playerCount < teamPlan.MaxPlayers
				? GroupPortalCapacityBlockedReason.GroupFanoutNotImplemented
				: GroupPortalCapacityBlockedReason.RegisteredInstanceFull;
			return new GroupPortalCapacityPlan(teamPlan.MaxPlayers, playerCount, state, reason);
		}

		if (transferState == GroupPortalTransferState.FreshInstanceAllocationNeeded)
		{
			return new GroupPortalCapacityPlan(
				teamPlan.MaxPlayers,
				CurrentPlayerCount: null,
				GroupPortalCapacityState.UnknownUntilInstanceAllocated,
				GroupPortalCapacityBlockedReason.InstanceAllocationNotPorted);
		}

		return CreateCapacityPlan(
			teamPlan,
			GroupPortalCapacityState.BlockedInvalidTeamId,
			GroupPortalCapacityBlockedReason.MissingTeamId);
	}

	private static GroupPortalCapacityPlan CreateCapacityPlan(
		PortalTeamEntryPlan teamPlan,
		GroupPortalCapacityState state,
		GroupPortalCapacityBlockedReason blockedReason)
	{
		return new GroupPortalCapacityPlan(teamPlan.MaxPlayers, CurrentPlayerCount: null, state, blockedReason);
	}

	private static GroupPortalAllocationPlan CreateAllocationPlan(
		PortalTeamEntryPlan teamPlan,
		PortalLocSummary? portalLoc,
		GroupPortalTransferState transferState)
	{
		if (transferState == GroupPortalTransferState.RegisteredInstanceTransfer)
		{
			return CreateAllocationPlan(
				teamPlan,
				portalLoc,
				GroupPortalAllocationState.NotNeededRegisteredTeamInstance,
				GroupPortalAllocationBlockedReason.RegisteredTeamInstanceAlreadyResolved);
		}

		if (transferState == GroupPortalTransferState.FreshInstanceAllocationNeeded)
		{
			// Java parity: PortalService.port group allocation calls InstanceService.getNextAvailableInstance(mapId, difficult, maxPlayers), then registerTeam(group).
			return CreateAllocationPlan(
				teamPlan,
				portalLoc,
				GroupPortalAllocationState.WouldAllocateAndRegisterTeam,
				GroupPortalAllocationBlockedReason.InstanceAllocationNotPorted);
		}

		return CreateAllocationPlan(
			teamPlan,
			portalLoc,
			GroupPortalAllocationState.BlockedInvalidTeamId,
			GroupPortalAllocationBlockedReason.MissingTeamId);
	}

	private static GroupPortalAllocationPlan CreateAllocationPlan(
		PortalTeamEntryPlan teamPlan,
		PortalLocSummary? portalLoc,
		GroupPortalAllocationState state,
		GroupPortalAllocationBlockedReason blockedReason)
	{
		return new GroupPortalAllocationPlan(
			portalLoc?.WorldId,
			DifficultyId: null,
			teamPlan.MaxPlayers,
			IntendedRegisteredTeamId: state == GroupPortalAllocationState.WouldAllocateAndRegisterTeam ? teamPlan.TeamId : null,
			state,
			blockedReason);
	}

	private static GroupPortalExecutionPlan CreateExecutionPlan(
		PortalTeamEntryPlan teamPlan,
		PortalLocSummary? portalLoc,
		int? playerObjectId,
		GroupPortalTransferState transferState)
	{
		if (transferState == GroupPortalTransferState.RegisteredInstanceTransfer
			&& teamPlan.RegisteredInstance != null
			&& portalLoc != null)
		{
			// Java parity: PortalService.transfer sets start position, registers the player, teleports with FADE_OUT_BEAM, then applies cooldown if not reentering.
			var destination = new WorldPosition(
				portalLoc.WorldId,
				portalLoc.X,
				portalLoc.Y,
				portalLoc.Z,
				portalLoc.Heading,
				teamPlan.RegisteredInstance.InstanceId);
			var cooldownState = teamPlan.Reenter
				? GroupPortalCooldownPreviewState.SkippedForReentry
				: GroupPortalCooldownPreviewState.WouldEvaluateAfterTeleport;
			return new GroupPortalExecutionPlan(
				teamPlan.RegisteredInstance.InstanceId,
				destination,
				playerObjectId,
				teamPlan.Reenter,
				TeleportAnimation.FadeOutBeam,
				cooldownState,
				GroupPortalExecutionState.WouldTransferToRegisteredInstance,
				GroupPortalExecutionBlockedReason.GroupFanoutNotImplemented);
		}

		if (transferState == GroupPortalTransferState.FreshInstanceAllocationNeeded)
		{
			return CreateExecutionPlan(
				teamPlan,
				portalLoc,
				playerObjectId,
				GroupPortalExecutionState.BlockedUntilInstanceAllocation,
				GroupPortalExecutionBlockedReason.InstanceAllocationNotPorted);
		}

		return CreateExecutionPlan(
			teamPlan,
			portalLoc,
			playerObjectId,
			GroupPortalExecutionState.BlockedInvalidTeamId,
			GroupPortalExecutionBlockedReason.MissingTeamId);
	}

	private static GroupPortalExecutionPlan CreateExecutionPlan(
		PortalTeamEntryPlan teamPlan,
		PortalLocSummary? portalLoc,
		int? playerObjectId,
		GroupPortalExecutionState state,
		GroupPortalExecutionBlockedReason blockedReason)
	{
		return new GroupPortalExecutionPlan(
			TargetInstanceId: null,
			StartPosition: portalLoc == null
				? null
				: new WorldPosition(portalLoc.WorldId, portalLoc.X, portalLoc.Y, portalLoc.Z, portalLoc.Heading),
			PlayerObjectIdToRegister: state == GroupPortalExecutionState.BlockedUntilInstanceAllocation ? playerObjectId : null,
			teamPlan.Reenter,
			TeleportAnimation.FadeOutBeam,
			GroupPortalCooldownPreviewState.UnknownUntilTransfer,
			state,
			blockedReason);
	}
}

public sealed record GroupPortalMemberInstanceScanPlan(
	IReadOnlyList<int> CandidateObjectIds,
	GroupPortalMemberInstanceScanState State,
	GroupPortalMemberInstanceScanBlockedReason BlockedReason);

public sealed record GroupPortalCapacityPlan(
	int MaxPlayers,
	int? CurrentPlayerCount,
	GroupPortalCapacityState State,
	GroupPortalCapacityBlockedReason BlockedReason);

public sealed record GroupPortalAllocationPlan(
	int? TargetWorldId,
	byte? DifficultyId,
	int MaxPlayers,
	int? IntendedRegisteredTeamId,
	GroupPortalAllocationState State,
	GroupPortalAllocationBlockedReason BlockedReason);

public sealed record GroupPortalExecutionPlan(
	int? TargetInstanceId,
	WorldPosition? StartPosition,
	int? PlayerObjectIdToRegister,
	bool Reenter,
	TeleportAnimation TeleportAnimation,
	GroupPortalCooldownPreviewState CooldownState,
	GroupPortalExecutionState State,
	GroupPortalExecutionBlockedReason BlockedReason);

public enum GroupPortalTransferState
{
	InvalidTeamId,
	FreshInstanceAllocationNeeded,
	RegisteredInstanceTransfer,
}

public enum GroupPortalTransferBlockedReason
{
	MissingTeamId,
	GroupFanoutNotImplemented,
}

public enum GroupPortalMemberInstanceScanState
{
	NotNeededRegisteredTeamInstance,
	WouldScanMemberObjectIds,
	BlockedInvalidTeamId,
	BlockedNoMemberCandidates,
}

public enum GroupPortalMemberInstanceScanBlockedReason
{
	RegisteredTeamInstanceAlreadyResolved,
	LiveGroupAggregateNotPorted,
	MissingTeamId,
	NoMemberObjectIds,
}

public enum GroupPortalCapacityState
{
	WouldPassCapacityGuard,
	WouldFailCapacityGuard,
	UnknownUntilInstanceAllocated,
	BlockedInvalidTeamId,
}

public enum GroupPortalCapacityBlockedReason
{
	GroupFanoutNotImplemented,
	RegisteredInstanceFull,
	InstanceAllocationNotPorted,
	MissingTeamId,
}

public enum GroupPortalAllocationState
{
	NotNeededRegisteredTeamInstance,
	WouldAllocateAndRegisterTeam,
	BlockedInvalidTeamId,
}

public enum GroupPortalAllocationBlockedReason
{
	RegisteredTeamInstanceAlreadyResolved,
	InstanceAllocationNotPorted,
	MissingTeamId,
}

public enum GroupPortalExecutionState
{
	WouldTransferToRegisteredInstance,
	BlockedUntilInstanceAllocation,
	BlockedInvalidTeamId,
}

public enum GroupPortalExecutionBlockedReason
{
	GroupFanoutNotImplemented,
	InstanceAllocationNotPorted,
	MissingTeamId,
}

public enum GroupPortalCooldownPreviewState
{
	WouldEvaluateAfterTeleport,
	SkippedForReentry,
	UnknownUntilTransfer,
}
