using Aion.GameServer.Controllers.Movement;
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
