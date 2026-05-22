using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class RiftLocationTable
{
	private readonly IReadOnlyDictionary<int, RiftLocationSummary> _locationsById;
	private readonly IReadOnlyDictionary<int, IReadOnlyList<RiftLocationSummary>> _locationsByWorldId;

	public RiftLocationTable(IReadOnlyList<RiftLocationSummary> locations)
	{
		Locations = locations;
		_locationsById = new ReadOnlyDictionary<int, RiftLocationSummary>(
			locations.ToDictionary(location => location.Id));
		_locationsByWorldId = new ReadOnlyDictionary<int, IReadOnlyList<RiftLocationSummary>>(
			locations
				.GroupBy(location => location.WorldId)
				.ToDictionary(
					group => group.Key,
					group => (IReadOnlyList<RiftLocationSummary>)group.ToArray()));
	}

	public IReadOnlyList<RiftLocationSummary> Locations { get; }

	public int Count => Locations.Count;

	public RiftLocationSummary? GetLocation(int riftId)
	{
		return _locationsById.GetValueOrDefault(riftId);
	}

	public IReadOnlyList<RiftLocationSummary> GetLocationsForWorld(int worldId)
	{
		return _locationsByWorldId.GetValueOrDefault(worldId) ?? Array.Empty<RiftLocationSummary>();
	}

	public bool Contains(int riftId)
	{
		return _locationsById.ContainsKey(riftId);
	}
}

public sealed record RiftLocationSummary(
	int Id,
	int WorldId,
	bool HasSpawns,
	bool AutoCloseable);
