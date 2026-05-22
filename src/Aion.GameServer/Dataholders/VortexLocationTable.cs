using System.Collections.ObjectModel;
using Aion.GameServer.World;

namespace Aion.GameServer.Dataholders;

public sealed class VortexLocationTable
{
	private readonly IReadOnlyDictionary<int, VortexLocationSummary> _locationsById;

	public VortexLocationTable(IReadOnlyList<VortexLocationSummary> locations)
	{
		Locations = locations;
		_locationsById = new ReadOnlyDictionary<int, VortexLocationSummary>(
			locations.ToDictionary(location => location.Id));
	}

	public IReadOnlyList<VortexLocationSummary> Locations { get; }

	public int Count => Locations.Count;

	public VortexLocationSummary? GetLocation(int id)
	{
		return _locationsById.GetValueOrDefault(id);
	}

	public VortexLocationSummary? GetLocationByInvasionWorld(int invasionWorldId)
	{
		// Java parity: dataholders/VortexData.getVortexLocation(int invasionWorldId).
		return Locations.FirstOrDefault(location => location.InvasionWorldId == invasionWorldId);
	}
}

public sealed record VortexLocationSummary(
	int Id,
	string DefendersRace,
	string InvadersRace,
	WorldPosition HomePoint,
	WorldPosition ResurrectionPoint,
	WorldPosition StartPoint)
{
	public int HomeWorldId => HomePoint.WorldId;

	public int InvasionWorldId => StartPoint.WorldId;
}
