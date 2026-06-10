using System;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.World;

/// <summary>
/// Java parity: world/WorldMap2DInstance (ATracer). 2D (x,y) region grid map instance. Function->Func; Math.round(float) kept as
/// floor(x+0.5f) for Java round-half-up semantics. MapRegion/InstanceHandler red-tolerated.
/// </summary>
public class WorldMap2DInstance : WorldMapInstance
{
    private readonly int ownerId;

    public WorldMap2DInstance(WorldMap parent, int instanceId, int ownerId, int maxPlayers, Func<WorldMapInstance, InstanceHandler> instanceHandlerSupplier)
        : base(parent, instanceId, maxPlayers, instanceHandlerSupplier)
    {
        this.ownerId = ownerId;
    }

    protected override MapRegion CreateMapRegion(int regionId)
    {
        float startX = RegionUtil.GetXFrom2dRegionId(regionId);
        float startY = RegionUtil.GetYFrom2dRegionId(regionId);
        int size = this.GetParent().GetWorldSize();
        float maxZ = (float)Math.Floor((float)size / regionSize + 0.5f) * regionSize;
        ZoneInstance[] zones = FilterZones(this.GetMapId(), regionId, startX, startY, 0, maxZ);
        return new MapRegion(regionId, this, zones);
    }

    protected override void InitMapRegions()
    {
        int size = this.GetParent().GetWorldSize();
        // Create all mapRegion
        for (int x = 0; x <= size; x = x + regionSize)
        {
            for (int y = 0; y <= size; y = y + regionSize)
            {
                int regionId = RegionUtil.Get2dRegionId(x, y);
                regions[regionId] = CreateMapRegion(regionId);
            }
        }

        // Add Neighbour
        for (int x = 0; x <= size; x = x + regionSize)
        {
            for (int y = 0; y <= size; y = y + regionSize)
            {
                int regionId = RegionUtil.Get2dRegionId(x, y);
                MapRegion mapRegion = regions[regionId];
                for (int x2 = x - regionSize; x2 <= x + regionSize; x2 += regionSize)
                {
                    for (int y2 = y - regionSize; y2 <= y + regionSize; y2 += regionSize)
                    {
                        if (x2 == x && y2 == y)
                            continue;
                        int neighbourId = RegionUtil.Get2dRegionId(x2, y2);
                        if (regions.TryGetValue(neighbourId, out MapRegion neighbour) && neighbour != null)
                            mapRegion.AddNeighbourRegion(neighbour);
                    }
                }
            }
        }
    }

    public override MapRegion GetRegion(float x, float y, float z)
    {
        int regionId = RegionUtil.Get2dRegionId(x, y);
        return regions.GetValueOrDefault(regionId);
    }

    public override int GetOwnerId()
    {
        return ownerId;
    }

    public override bool IsPersonal()
    {
        return ownerId != 0;
    }
}
