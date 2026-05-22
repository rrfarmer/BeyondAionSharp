namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerRideInfo(
	int NpcId,
	int StartFp,
	int? CostFp,
	float SprintSpeed,
	float FlySpeed,
	float MoveSpeed)
{
	public bool CanSprint()
	{
		// Java parity: model/templates/ride/RideInfo.canSprint.
		return SprintSpeed != 0;
	}
}
