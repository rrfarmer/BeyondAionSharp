using Aion.GameServer.Controllers.Movement;

namespace Aion.GameServer.Model.GameObjects;

public sealed class PlayerMovementState
{
	public byte Mask { get; set; } = MovementMask.Immediate;

	public float TargetX { get; set; }

	public float TargetY { get; set; }

	public float TargetZ { get; set; }

	public float VectorX { get; set; }

	public float VectorY { get; set; }

	public float VectorZ { get; set; }

	public byte GlideFlag { get; set; }

	public int GeyserLocationId { get; set; }

	public int VehicleUnk1 { get; set; }

	public int VehicleUnk2 { get; set; }

	public float VehicleX { get; set; }

	public float VehicleY { get; set; }

	public float VehicleZ { get; set; }

	public bool IsJumping { get; set; }

	public int FlightDistance { get; set; }

	public void SetNewDirection(float x, float y, float z)
	{
		// Java parity: controllers/movement/CreatureMoveController.setNewDirection.
		TargetX = x;
		TargetY = y;
		TargetZ = z;
	}
}
