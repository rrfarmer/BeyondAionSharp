using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class PortalLocTable
{
	private readonly IReadOnlyDictionary<int, PortalLocSummary> _locationsById;

	public PortalLocTable(IReadOnlyList<PortalLocSummary> locations)
	{
		Locations = locations;
		_locationsById = new ReadOnlyDictionary<int, PortalLocSummary>(
			locations.ToDictionary(location => location.LocId));
	}

	public IReadOnlyList<PortalLocSummary> Locations { get; }

	public int Count => Locations.Count;

	public PortalLocSummary? GetPortalLoc(int locId)
	{
		// Java parity: dataholders/PortalLocData.getPortalLoc.
		return _locationsById.GetValueOrDefault(locId);
	}
}

public sealed record PortalLocSummary(
	int WorldId,
	int LocId,
	float X,
	float Y,
	float Z,
	byte Heading);
