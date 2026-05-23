using Aion.GameServer.World;

namespace Aion.GameServer.Dataholders;

public sealed class FlightZoneTable
{
	public static readonly FlightZoneTable Empty = new([]);

	private readonly IReadOnlyDictionary<int, IReadOnlyList<FlightZoneSummary>> _zonesByMapId;

	public FlightZoneTable(IReadOnlyList<FlightZoneSummary> zones)
	{
		Zones = zones;
		_zonesByMapId = zones
			.GroupBy(zone => zone.MapId)
			.ToDictionary(
				group => group.Key,
				group => (IReadOnlyList<FlightZoneSummary>)group.ToArray());
	}

	public IReadOnlyList<FlightZoneSummary> Zones { get; }

	public int Count => Zones.Count;

	public IReadOnlyList<FlightZoneSummary> GetZonesByMapId(int mapId)
	{
		return _zonesByMapId.TryGetValue(mapId, out var zones) ? zones : Array.Empty<FlightZoneSummary>();
	}
}

public sealed record FlightZoneSummary(
	int MapId,
	string Name,
	FlightZoneType ZoneType,
	int Flags,
	float Bottom,
	float Top,
	IReadOnlyList<ZonePoint2D> Points)
{
	public bool Contains(WorldPosition position)
	{
		return MapId == position.WorldId && Contains(position.X, position.Y, position.Z);
	}

	public bool Contains(float x, float y, float z)
	{
		// Java parity: model/geometry/AbstractArea.isInside3D + PolyArea.isInside2D.
		if (z < Bottom || z > Top || Points.Count < 3)
			return false;

		var inside = false;
		for (var i = 0; i < Points.Count; i++)
		{
			var j = i == 0 ? Points.Count - 1 : i - 1;
			var pointI = Points[i];
			var pointJ = Points[j];
			if ((pointI.Y > y) != (pointJ.Y > y)
				&& x < (pointJ.X - pointI.X) * (y - pointI.Y) / (pointJ.Y - pointI.Y) + pointI.X)
			{
				inside = !inside;
			}
		}

		return inside;
	}
}

public readonly record struct ZonePoint2D(float X, float Y);

public enum FlightZoneType
{
	Fly,
	NoFly,
}
