using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class RideTable
{
	private readonly IReadOnlyDictionary<int, RideInfoSummary> _ridesByNpcId;

	public RideTable(IReadOnlyList<RideInfoSummary> rides)
	{
		Rides = rides;
		_ridesByNpcId = new ReadOnlyDictionary<int, RideInfoSummary>(
			rides.ToDictionary(ride => ride.NpcId));
	}

	public IReadOnlyList<RideInfoSummary> Rides { get; }

	public int Count => Rides.Count;

	public RideInfoSummary? GetRideInfo(int npcId)
	{
		return _ridesByNpcId.GetValueOrDefault(npcId);
	}
}

public sealed record RideInfoSummary(
	int NpcId,
	int Type,
	float MoveSpeed,
	float FlySpeed,
	float SprintSpeed,
	int StartFp,
	int CostFp)
{
	public bool CanSprint()
	{
		// Java parity: model/templates/ride/RideInfo.canSprint.
		return SprintSpeed != 0;
	}
}
