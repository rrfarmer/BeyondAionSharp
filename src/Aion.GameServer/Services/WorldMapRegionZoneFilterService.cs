using Aion.GameServer.Dataholders;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class WorldMapRegionZoneFilterService
{
	public static WorldMapRegionZoneFilterResult FilterZones(
		int mapId,
		int regionId,
		WorldMapRegionBounds regionBounds,
		IEnumerable<WorldMapRegionZoneCandidate> zones)
	{
		// Java parity breadcrumb: WorldMapInstance.filterZones builds RegionZone and keeps
		// zones whose Area.intersectsRectangle(regionZone) returns true.
		var matched = new List<WorldMapRegionZoneCandidate>();
		var dummyMisses = new List<WorldMapRegionZoneCandidate>();

		foreach (var zone in zones)
		{
			if (zone.MapId != mapId)
				continue;

			if (IntersectsRegion(zone.Area, regionBounds))
			{
				matched.Add(zone);
				continue;
			}

			if (zone.ZoneClassName == WorldMapRegionZoneClassName.Dummy)
				dummyMisses.Add(zone);
		}

		return new WorldMapRegionZoneFilterResult(
			mapId,
			regionId,
			regionBounds,
			matched,
			dummyMisses,
			"WorldMapInstance.filterZones -> Area.intersectsRectangle(new RegionZone(...))");
	}

	public static WorldMapRegionBounds CreateRegionBounds(WorldMapRegionLayout layout, int regionId)
	{
		return layout.Dimension switch
		{
			WorldMapRegionDimension.TwoDimensional => new WorldMapRegionBounds(
				WorldRegionIdService.GetXFrom2DRegionId(regionId, layout.RegionSize),
				WorldRegionIdService.GetYFrom2DRegionId(regionId, layout.RegionSize),
				MinZ: 0,
				layout.MaxZ,
				layout.RegionSize),
			WorldMapRegionDimension.ThreeDimensional => new WorldMapRegionBounds(
				WorldRegionIdService.GetXFrom3DRegionId(regionId, layout.RegionSize),
				WorldRegionIdService.GetYFrom3DRegionId(regionId, layout.RegionSize),
				WorldRegionIdService.GetZFrom3DRegionId(regionId, layout.RegionSize),
				WorldRegionIdService.GetZFrom3DRegionId(regionId, layout.RegionSize) + layout.RegionSize,
				layout.RegionSize),
			_ => throw new ArgumentOutOfRangeException(nameof(layout), layout.Dimension, "Unsupported Java world-map region dimension."),
		};
	}

	private static bool IntersectsRegion(WorldMapZoneArea area, WorldMapRegionBounds regionBounds)
	{
		if (area is WorldMapRectangleZoneArea)
		{
			// Java parity: RectangleArea.intersectsRectangle is a TODO stub returning false.
			return false;
		}

		if (area.MaxZ < regionBounds.MinZ || area.MinZ > regionBounds.MaxZ)
			return false;

		return area switch
		{
			WorldMapPolygonZoneArea polygon => PolygonIntersectsRectangle(polygon.Points, regionBounds),
			WorldMapCylinderZoneArea cylinder => GetRectangleDistance2D(regionBounds, cylinder.CenterX, cylinder.CenterY) < cylinder.Radius,
			WorldMapSphereZoneArea sphere => GetRectangleDistance3D(regionBounds, sphere.CenterX, sphere.CenterY, sphere.CenterZ) <= sphere.Radius,
			WorldMapSemisphereZoneArea semisphere => (regionBounds.MaxZ >= semisphere.CenterZ || semisphere.CenterZ <= regionBounds.MinZ)
				&& GetRectangleDistance3D(regionBounds, semisphere.CenterX, semisphere.CenterY, semisphere.CenterZ) <= semisphere.Radius,
			_ => false,
		};
	}

	private static bool PolygonIntersectsRectangle(IReadOnlyList<ZonePoint2D> points, WorldMapRegionBounds regionBounds)
	{
		if (points.Count < 3)
			return false;

		var corners = regionBounds.Corners;
		if (points.Any(point => regionBounds.Contains2D(point.X, point.Y)))
			return true;
		if (corners.Any(corner => IsPointInsidePolygon(points, corner.X, corner.Y)))
			return true;

		for (var i = 0; i < points.Count; i++)
		{
			var a = points[i];
			var b = points[(i + 1) % points.Count];
			for (var j = 0; j < corners.Count; j++)
			{
				var c = corners[j];
				var d = corners[(j + 1) % corners.Count];
				if (SegmentsIntersect(a, b, c, d))
					return true;
			}
		}

		return false;
	}

	private static bool IsPointInsidePolygon(IReadOnlyList<ZonePoint2D> points, float x, float y)
	{
		var inside = false;
		for (var i = 0; i < points.Count; i++)
		{
			var j = i == 0 ? points.Count - 1 : i - 1;
			var pointI = points[i];
			var pointJ = points[j];
			if ((pointI.Y > y) != (pointJ.Y > y)
				&& x < (pointJ.X - pointI.X) * (y - pointI.Y) / (pointJ.Y - pointI.Y) + pointI.X)
			{
				inside = !inside;
			}
		}

		return inside;
	}

	private static bool SegmentsIntersect(ZonePoint2D a, ZonePoint2D b, ZonePoint2D c, ZonePoint2D d)
	{
		var d1 = Direction(c, d, a);
		var d2 = Direction(c, d, b);
		var d3 = Direction(a, b, c);
		var d4 = Direction(a, b, d);

		if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0))
			&& ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
		{
			return true;
		}

		return d1 == 0 && OnSegment(c, d, a)
			|| d2 == 0 && OnSegment(c, d, b)
			|| d3 == 0 && OnSegment(a, b, c)
			|| d4 == 0 && OnSegment(a, b, d);
	}

	private static float Direction(ZonePoint2D a, ZonePoint2D b, ZonePoint2D c)
	{
		return (c.X - a.X) * (b.Y - a.Y) - (b.X - a.X) * (c.Y - a.Y);
	}

	private static bool OnSegment(ZonePoint2D a, ZonePoint2D b, ZonePoint2D c)
	{
		return Math.Min(a.X, b.X) <= c.X && c.X <= Math.Max(a.X, b.X)
			&& Math.Min(a.Y, b.Y) <= c.Y && c.Y <= Math.Max(a.Y, b.Y);
	}

	private static double GetRectangleDistance2D(WorldMapRegionBounds regionBounds, float x, float y)
	{
		if (regionBounds.Contains2D(x, y))
			return 0;

		var closestX = Math.Clamp(x, regionBounds.MinX, regionBounds.MaxX);
		var closestY = Math.Clamp(y, regionBounds.MinY, regionBounds.MaxY);
		return Math.Sqrt(Math.Pow(x - closestX, 2) + Math.Pow(y - closestY, 2));
	}

	private static double GetRectangleDistance3D(WorldMapRegionBounds regionBounds, float x, float y, float z)
	{
		var distance2D = GetRectangleDistance2D(regionBounds, x, y);
		if (z >= regionBounds.MinZ && z <= regionBounds.MaxZ)
			return distance2D;

		var closestZ = z < regionBounds.MinZ ? regionBounds.MinZ : regionBounds.MaxZ;
		return Math.Sqrt(Math.Pow(distance2D, 2) + Math.Pow(z - closestZ, 2));
	}
}

public sealed record WorldMapRegionZoneFilterResult(
	int MapId,
	int RegionId,
	WorldMapRegionBounds RegionBounds,
	IReadOnlyList<WorldMapRegionZoneCandidate> MatchedZones,
	IReadOnlyList<WorldMapRegionZoneCandidate> DummyZonesMissingWholeMapIntersection,
	string JavaSource);

public sealed record WorldMapRegionZoneCandidate(
	string ZoneId,
	int MapId,
	WorldMapRegionZoneClassName ZoneClassName,
	WorldMapZoneArea Area);

public enum WorldMapRegionZoneClassName
{
	Other,
	Dummy,
}

public sealed record WorldMapRegionBounds(
	float MinX,
	float MinY,
	float MinZ,
	float MaxZ,
	int RegionSize)
{
	public float MaxX => MinX + RegionSize;

	public float MaxY => MinY + RegionSize;

	public IReadOnlyList<ZonePoint2D> Corners =>
	[
		new(MinX, MinY),
		new(MaxX, MinY),
		new(MaxX, MaxY),
		new(MinX, MaxY),
	];

	public bool Contains2D(float x, float y)
	{
		return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
	}
}

public abstract record WorldMapZoneArea(float MinZ, float MaxZ);

public sealed record WorldMapPolygonZoneArea(
	IReadOnlyList<ZonePoint2D> Points,
	float Bottom,
	float Top) : WorldMapZoneArea(Bottom, Top);

public sealed record WorldMapCylinderZoneArea(
	float CenterX,
	float CenterY,
	float Radius,
	float Bottom,
	float Top) : WorldMapZoneArea(Bottom, Top);

public sealed record WorldMapSphereZoneArea(
	float CenterX,
	float CenterY,
	float CenterZ,
	float Radius) : WorldMapZoneArea(CenterZ - Radius, CenterZ + Radius);

public sealed record WorldMapSemisphereZoneArea(
	float CenterX,
	float CenterY,
	float CenterZ,
	float Radius) : WorldMapZoneArea(CenterZ, CenterZ + Radius);

public sealed record WorldMapRectangleZoneArea(
	float MinX,
	float MinY,
	float MaxX,
	float MaxY,
	float Bottom,
	float Top) : WorldMapZoneArea(Bottom, Top);
