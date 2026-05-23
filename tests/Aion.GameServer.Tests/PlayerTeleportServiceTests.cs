using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerTeleportServiceTests
{
	[Fact]
	public void TeleportToKiskPositionUpdatesAuthoritativePositionAndResetsMovement()
	{
		var previous = new WorldPosition(210010000, 1, 2, 3, 4, InstanceId: 1);
		var destination = new WorldPosition(210010000, 10, 20, 30, 40, InstanceId: 7);
		var player = CreateMovingPlayer(previous);

		var result = PlayerTeleportService.TeleportToKiskPosition(player, destination);

		Assert.Equal(previous, result.PreviousPosition);
		Assert.Equal(destination, result.Destination);
		Assert.True(result.UsesSameWorldSpawnPath);
		Assert.Equal(destination, player.Position);
		Assert.Equal(MovementMask.Immediate, player.Movement.Mask);
		Assert.Equal(destination.X, player.Movement.TargetX);
		Assert.Equal(destination.Y, player.Movement.TargetY);
		Assert.Equal(destination.Z, player.Movement.TargetZ);
		Assert.Equal(0f, player.Movement.VectorX);
		Assert.Equal(0f, player.Movement.VectorY);
		Assert.Equal(0f, player.Movement.VectorZ);
		Assert.Equal(GlideFlag.None, player.Movement.GlideFlag);
		Assert.Equal(0, player.Movement.GeyserLocationId);
		Assert.Equal(0, player.Movement.VehicleUnk1);
		Assert.Equal(0, player.Movement.VehicleUnk2);
		Assert.Equal(0f, player.Movement.VehicleX);
		Assert.Equal(0f, player.Movement.VehicleY);
		Assert.Equal(0f, player.Movement.VehicleZ);
		Assert.False(player.Movement.IsJumping);
		Assert.Equal(0, player.Movement.FlightDistance);
	}

	[Fact]
	public void TeleportToKiskPositionUsesFullSpawnPathWhenWorldChanges()
	{
		var player = CreateMovingPlayer(new WorldPosition(210010000, 1, 2, 3, 4, InstanceId: 1));
		var destination = new WorldPosition(220010000, 10, 20, 30, 40, InstanceId: 1);

		var result = PlayerTeleportService.TeleportToKiskPosition(player, destination);

		Assert.False(result.UsesSameWorldSpawnPath);
		Assert.Equal(destination, player.Position);
	}

	private static Player CreateMovingPlayer(WorldPosition position)
	{
		var player = new Player { Position = position };
		player.Movement.Mask = (byte)(MovementMask.Absolute | MovementMask.Manual | MovementMask.Position | MovementMask.Vehicle);
		player.Movement.SetNewDirection(100, 200, 300);
		player.Movement.VectorX = 1;
		player.Movement.VectorY = 2;
		player.Movement.VectorZ = 3;
		player.Movement.GlideFlag = GlideFlag.Geyser;
		player.Movement.GeyserLocationId = 55;
		player.Movement.VehicleUnk1 = 66;
		player.Movement.VehicleUnk2 = 77;
		player.Movement.VehicleX = 4;
		player.Movement.VehicleY = 5;
		player.Movement.VehicleZ = 6;
		player.Movement.IsJumping = true;
		player.Movement.FlightDistance = 88;
		return player;
	}
}
