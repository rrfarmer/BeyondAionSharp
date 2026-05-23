using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model.GameObjects;
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
