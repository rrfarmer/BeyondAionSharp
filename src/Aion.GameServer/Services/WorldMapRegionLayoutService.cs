using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class WorldMapRegionLayoutService
{
	public static WorldMapRegionLayout CreateLayoutForWorld(
		int worldId,
		int worldSize,
		int regionSize = WorldRegionIdService.DefaultRegionSize)
	{
		return CreateLayout(
			worldSize,
			WorldRegionKeyProjectionService.GetJavaRegionDimension(worldId),
			regionSize);
	}

	public static WorldMapRegionLayout CreateLayout(
		int worldSize,
		WorldMapRegionDimension dimension,
		int regionSize = WorldRegionIdService.DefaultRegionSize)
	{
		if (worldSize < 0)
			throw new ArgumentOutOfRangeException(nameof(worldSize), worldSize, "World size must be non-negative.");
		if (regionSize <= 0)
			throw new ArgumentOutOfRangeException(nameof(regionSize), regionSize, "Region size must be positive.");

		return dimension switch
		{
			WorldMapRegionDimension.TwoDimensional => Create2DLayout(worldSize, regionSize),
			WorldMapRegionDimension.ThreeDimensional => Create3DLayout(worldSize, regionSize),
			_ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unsupported Java world-map region dimension."),
		};
	}

	public static WorldMapRegionLayoutResolution ResolvePosition(
		WorldMapRegionLayout layout,
		WorldPosition position)
	{
		var regionId = layout.Dimension switch
		{
			WorldMapRegionDimension.TwoDimensional => WorldRegionIdService.Get2DRegionId(position.X, position.Y, layout.RegionSize),
			WorldMapRegionDimension.ThreeDimensional => WorldRegionIdService.Get3DRegionId(position.X, position.Y, position.Z, layout.RegionSize),
			_ => throw new ArgumentOutOfRangeException(nameof(layout), layout.Dimension, "Unsupported Java world-map region dimension."),
		};

		var exists = layout.NeighbourRegionIds.TryGetValue(regionId, out var neighbourRegionIds);
		return new WorldMapRegionLayoutResolution(
			position,
			layout.Dimension,
			regionId,
			exists,
			neighbourRegionIds ?? Array.Empty<int>(),
			layout.Dimension == WorldMapRegionDimension.TwoDimensional
				? "WorldMap2DInstance.getRegion -> regions.get(RegionUtil.get2dRegionId(x, y))"
				: "WorldMap3DInstance.getRegion -> regions.get(RegionUtil.get3dRegionId(x, y, z))");
	}

	private static WorldMapRegionLayout Create2DLayout(int worldSize, int regionSize)
	{
		// Java parity breadcrumb: WorldMap2DInstance.initMapRegions precreates x/y regions
		// with inclusive x <= size and y <= size loops.
		var regionIds = new List<int>();
		for (var x = 0; x <= worldSize; x += regionSize)
		{
			for (var y = 0; y <= worldSize; y += regionSize)
				regionIds.Add(WorldRegionIdService.Get2DRegionId(x, y, regionSize));
		}

		var regionSet = regionIds.ToHashSet();
		var neighbourIds = new Dictionary<int, IReadOnlyList<int>>();
		for (var x = 0; x <= worldSize; x += regionSize)
		{
			for (var y = 0; y <= worldSize; y += regionSize)
			{
				var regionId = WorldRegionIdService.Get2DRegionId(x, y, regionSize);
				var neighbours = new List<int>();
				for (var x2 = x - regionSize; x2 <= x + regionSize; x2 += regionSize)
				{
					for (var y2 = y - regionSize; y2 <= y + regionSize; y2 += regionSize)
					{
						if (x2 == x && y2 == y)
							continue;

						var neighbourId = WorldRegionIdService.Get2DRegionId(x2, y2, regionSize);
						if (regionSet.Contains(neighbourId))
							neighbours.Add(neighbourId);
					}
				}

				neighbourIds[regionId] = neighbours;
			}
		}

		return new WorldMapRegionLayout(
			worldSize,
			regionSize,
			WorldMapRegionDimension.TwoDimensional,
			JavaRoundedRegionMaxZ(worldSize, regionSize),
			regionIds,
			neighbourIds);
	}

	private static WorldMapRegionLayout Create3DLayout(int worldSize, int regionSize)
	{
		var maxZ = JavaRoundedRegionMaxZ(worldSize, regionSize);
		// Java parity breadcrumb: WorldMap3DInstance.initMapRegions precomputes ids
		// before parallel creation, using z < maxZ rather than z <= maxZ.
		var regionIds = new List<int>();
		for (var x = 0; x <= worldSize; x += regionSize)
		{
			for (var y = 0; y <= worldSize; y += regionSize)
			{
				for (var z = 0; z < maxZ; z += regionSize)
					regionIds.Add(WorldRegionIdService.Get3DRegionId(x, y, z, regionSize));
			}
		}

		var regionSet = regionIds.ToHashSet();
		var neighbourIds = new Dictionary<int, IReadOnlyList<int>>();
		for (var x = 0; x <= worldSize; x += regionSize)
		{
			for (var y = 0; y <= worldSize; y += regionSize)
			{
				for (var z = 0; z < maxZ; z += regionSize)
				{
					var regionId = WorldRegionIdService.Get3DRegionId(x, y, z, regionSize);
					var neighbours = new List<int>();
					for (var x2 = x - regionSize; x2 <= x + regionSize; x2 += regionSize)
					{
						for (var y2 = y - regionSize; y2 <= y + regionSize; y2 += regionSize)
						{
							for (var z2 = z - regionSize; z2 < z + regionSize; z2 += regionSize)
							{
								if (x2 == x && y2 == y && z2 == z)
									continue;

								var neighbourId = WorldRegionIdService.Get3DRegionId(x2, y2, z2, regionSize);
								if (regionSet.Contains(neighbourId))
									neighbours.Add(neighbourId);
							}
						}
					}

					neighbourIds[regionId] = neighbours;
				}
			}
		}

		return new WorldMapRegionLayout(
			worldSize,
			regionSize,
			WorldMapRegionDimension.ThreeDimensional,
			maxZ,
			regionIds,
			neighbourIds);
	}

	private static int JavaRoundedRegionMaxZ(int worldSize, int regionSize)
	{
		// Java Math.round(float) is floor(value + 0.5f) for positive values.
		return (int)MathF.Floor((float)worldSize / regionSize + 0.5f) * regionSize;
	}
}

public sealed record WorldMapRegionLayout(
	int WorldSize,
	int RegionSize,
	WorldMapRegionDimension Dimension,
	int MaxZ,
	IReadOnlyList<int> RegionIds,
	IReadOnlyDictionary<int, IReadOnlyList<int>> NeighbourRegionIds);

public sealed record WorldMapRegionLayoutResolution(
	WorldPosition Position,
	WorldMapRegionDimension Dimension,
	int RegionId,
	bool RegionExists,
	IReadOnlyList<int> NeighbourRegionIds,
	string JavaSource);
