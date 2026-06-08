using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Geometry;

namespace Aion.GameServer.World.Zone;

/// <summary>
/// Axis-aligned region square used to test which zones intersect a map region.
/// Java parity: world/zone/RegionZone.
/// </summary>
public class RegionZone : RectangleArea
{
    // Java parity: passes null ZoneName / worldId 0; spans one WORLD_REGION_SIZE square.
    public RegionZone(float startX, float startY, float minZ, float maxZ)
        : base(null!, 0, startX, startY, startX + WorldConfig.WorldRegionSize, startY + WorldConfig.WorldRegionSize, minZ, maxZ)
    {
    }

    // Java parity: isInside(AbstractArea) — always true (region squares are containers).
    public bool IsInside(AbstractArea area) => true;
}
