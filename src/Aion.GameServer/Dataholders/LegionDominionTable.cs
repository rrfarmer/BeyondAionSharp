using Aion.GameServer.Utils;

namespace Aion.GameServer.Dataholders;

public sealed record LegionDominionLocationSummary(int Id, int NameId)
{
	public string? L10n => ChatUtil.L10n(NameId);
}

public sealed class LegionDominionTable
{
	private readonly IReadOnlyDictionary<int, LegionDominionLocationSummary> _locationsById;

	public LegionDominionTable(IReadOnlyList<LegionDominionLocationSummary> locations)
	{
		_locationsById = locations.ToDictionary(location => location.Id);
		Locations = locations;
	}

	public IReadOnlyList<LegionDominionLocationSummary> Locations { get; }

	public int Count => _locationsById.Count;

	public LegionDominionLocationSummary? GetLocation(int locationId)
	{
		// Java parity: LegionDominionService.getLegionDominionLoc returns the location by id.
		return _locationsById.GetValueOrDefault(locationId);
	}
}
