using Aion.GameServer.Configs.Main;

namespace Aion.GameServer.World;

/// <summary>
/// Region-id encoding/decoding math for 2D/3D map regions.
/// Java parity: world/RegionUtil.
/// </summary>
public static class RegionUtil
{
    public const int X_3D_OFFSET = 1000000;
    public const int Y_3D_OFFSET = 1000;
    public const int X_2D_OFFSET = 1000;

    // Java parity: get2DRegionId(int regionSize, float x, float y)
    public static int Get2DRegionId(int regionSize, float x, float y) =>
        (int)x / regionSize * X_2D_OFFSET + (int)y / regionSize;

    // Java parity: get3DRegionId(int regionSize, float x, float y, float z)
    public static int Get3DRegionId(int regionSize, float x, float y, float z) =>
        (int)x / regionSize * X_3D_OFFSET + (int)y / regionSize * Y_3D_OFFSET + (int)z / regionSize;

    // Java parity: get2dRegionId(float x, float y)
    public static int Get2dRegionId(float x, float y) => Get2DRegionId(WorldConfig.WorldRegionSize, x, y);

    // Java parity: get3dRegionId(float x, float y, float z)
    public static int Get3dRegionId(float x, float y, float z) => Get3DRegionId(WorldConfig.WorldRegionSize, x, y, z);

    // Java parity: getXFrom2dRegionId(int regionId)
    public static int GetXFrom2dRegionId(int regionId) => regionId / X_2D_OFFSET * WorldConfig.WorldRegionSize;

    // Java parity: getYFrom2dRegionId(int regionId)
    public static int GetYFrom2dRegionId(int regionId) => regionId % X_2D_OFFSET * WorldConfig.WorldRegionSize;

    // Java parity: getXFrom3dRegionId(int regionId)
    public static int GetXFrom3dRegionId(int regionId) => regionId / X_3D_OFFSET * WorldConfig.WorldRegionSize;

    // Java parity: getYFrom3dRegionId(int regionId)
    public static int GetYFrom3dRegionId(int regionId) => regionId % X_3D_OFFSET / Y_3D_OFFSET * WorldConfig.WorldRegionSize;

    // Java parity: getZFrom3dRegionId(int regionId)
    public static int GetZFrom3dRegionId(int regionId) => regionId % X_3D_OFFSET % Y_3D_OFFSET * WorldConfig.WorldRegionSize;
}
