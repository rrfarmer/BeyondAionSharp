using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.World;

/// <summary>
/// Java parity: world/WorldMap3DInstance (ATracer). 3D (x,y,z) region grid map instance. parallelStream().forEach ->
/// Parallel.ForEach with lock(regions); Math.round(float) -> floor(x+0.5f). MapRegion/IInstanceHandler red-tolerated.
/// </summary>
public class WorldMap3DInstance : WorldMapInstance
{
    public WorldMap3DInstance(WorldMap parent, int instanceId, int maxPlayers, Func<WorldMapInstance, IInstanceHandler> instanceHandlerSupplier)
        : base(parent, instanceId, maxPlayers, instanceHandlerSupplier)
    {
    }

    public override MapRegion GetRegion(float x, float y, float z)
    {
        int regionId = RegionUtil.Get3dRegionId(x, y, z);
        return regions.GetValueOrDefault(regionId);
    }

    protected override void InitMapRegions()
    {
        int size = GetParent().GetWorldSize();
        float maxZ = (float)Math.Floor((float)size / regionSize + 0.5f) * regionSize;

        List<int> regionIds = new List<int>();
        for (int x = 0; x <= size; x = x + regionSize)
        {
            for (int y = 0; y <= size; y = y + regionSize)
            {
                for (int z = 0; z < maxZ; z = z + regionSize)
                {
                    regionIds.Add(RegionUtil.Get3dRegionId(x, y, z));
                }
            }
        }
        Parallel.ForEach(regionIds, regionId =>
        {
            MapRegion mapRegion = CreateMapRegion(regionId);
            lock (regions)
            {
                regions[regionId] = mapRegion;
            }
        });

        // Add Neighbour
        for (int x = 0; x <= size; x = x + regionSize)
        {
            for (int y = 0; y <= size; y = y + regionSize)
            {
                for (int z = 0; z < maxZ; z = z + regionSize)
                {
                    int regionId = RegionUtil.Get3dRegionId(x, y, z);
                    MapRegion mapRegion = regions[regionId];
                    for (int x2 = x - regionSize; x2 <= x + regionSize; x2 += regionSize)
                    {
                        for (int y2 = y - regionSize; y2 <= y + regionSize; y2 += regionSize)
                        {
                            for (int z2 = z - regionSize; z2 < z + regionSize; z2 += regionSize)
                            {
                                if (x2 == x && y2 == y && z2 == z)
                                    continue;
                                int neighbourId = RegionUtil.Get3dRegionId(x2, y2, z2);
                                if (regions.TryGetValue(neighbourId, out MapRegion neighbour) && neighbour != null)
                                    mapRegion.AddNeighbourRegion(neighbour);
                            }
                        }
                    }
                }
            }
        }
    }

    protected override MapRegion CreateMapRegion(int regionId)
    {
        float startX = RegionUtil.GetXFrom3dRegionId(regionId);
        float startY = RegionUtil.GetYFrom3dRegionId(regionId);
        float startZ = RegionUtil.GetZFrom3dRegionId(regionId);
        ZoneInstance[] zones = FilterZones(this.GetMapId(), regionId, startX, startY, startZ, startZ + regionSize);
        return new MapRegion(regionId, this, zones);
    }

    public override bool IsPersonal()
    {
        return false;
    }

    public override int GetOwnerId()
    {
        return 0;
    }
}
