namespace Aion.GameServer.Dataholders;

public sealed class WindstreamTable
{
	// Java parity: dataholders/WindstreamData indexes WindstreamTemplates by mapId.
	private readonly IReadOnlyDictionary<int, IReadOnlyList<WindstreamLocationSummary>> _locationsByMapId;

	public WindstreamTable(IReadOnlyList<WindstreamLocationSummary> locations)
	{
		Locations = locations;
		_locationsByMapId = locations
			.GroupBy(loc => loc.MapId)
			.ToDictionary(
				group => group.Key,
				group => (IReadOnlyList<WindstreamLocationSummary>)group.ToArray());
	}

	public IReadOnlyList<WindstreamLocationSummary> Locations { get; }

	public int Count => Locations.Count;

	public IReadOnlyList<WindstreamLocationSummary> GetByMapId(int mapId)
	{
		// Java parity: WindstreamData.getStreamTemplate(mapId) returns null when not found.
		return _locationsByMapId.GetValueOrDefault(mapId) ?? Array.Empty<WindstreamLocationSummary>();
	}
}

public sealed record WindstreamLocationSummary(
	// Java parity: Location2D.getFlyPathType().getId() -> FlyPathType: GEYSER=0, ONE_WAY=1, TWO_WAY=2.
	int FlyPathId,
	int MapId,
	int StreamId,
	int State);
