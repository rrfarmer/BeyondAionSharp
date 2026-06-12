using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.World;

/// <summary>
/// Position of an object in the world.
/// Java parity: world/WorldPosition.
/// </summary>
/// <remarks>
/// Faithful port of the Java mutable class: holds a <see cref="MapRegion"/> reference and derives
/// <c>instanceId</c>/instance-map state from it (no stored instanceId — that was a non-Java artifact
/// of the previous C# <c>readonly record struct</c>, reconciled during the spine big-bang/F4).
/// C#-named data properties (WorldId/X/Y/Z/Heading/InstanceId) are kept alongside the Java getters to
/// ease migration of legacy packet-path readers.
/// </remarks>
public sealed class WorldPosition
{
    private static readonly ILogger Log = NullLogger.Instance;

    private readonly int _mapId;
    private MapRegion? _mapRegion;
    private float _x, _y, _z;
    private byte _heading;
    private volatile bool _isSpawned;

    // Java parity: WorldPosition(int mapId)
    public WorldPosition(int mapId)
    {
        _mapId = mapId;
    }

    // Java parity: WorldPosition(int mapId, float x, float y, float z, byte h)
    public WorldPosition(int mapId, float x, float y, float z, byte h)
        : this(mapId, x, y, z, h, null)
    {
    }

    // Java parity: WorldPosition(int mapId, float x, float y, float z, byte h, MapRegion mapRegion)
    public WorldPosition(int mapId, float x, float y, float z, byte h, MapRegion? mapRegion)
    {
        _mapId = mapId;
        _x = x;
        _y = y;
        _z = z;
        _heading = h;
        _mapRegion = mapRegion;
    }

    // Java parity: getMapId() (warns if mapId == 0)
    public int GetMapId()
    {
        if (_mapId == 0)
            Log.LogWarning("WorldPosition has (mapId == 0) {Position}", ToString());
        return _mapId;
    }

    // Java parity: getX()/getY()/getZ()/getHeading()
    public float GetX() => _x;
    public float GetY() => _y;
    public float GetZ() => _z;
    public byte GetHeading() => _heading;

    // Java parity: getMapRegion()
    public MapRegion? GetMapRegion() => _mapRegion;

    // Java parity: isMapRegionActive()
    public bool IsMapRegionActive() => _mapRegion != null && _mapRegion.IsActive();

    // Java parity: getInstanceId() — derived from the map region's parent instance.
    public int GetInstanceId() => _mapRegion == null ? 1 : _mapRegion.GetParent().GetInstanceId();

    // Java parity: isInstanceMap()
    public bool IsInstanceMap() => _mapRegion != null && _mapRegion.GetParent().GetParent().IsInstanceType();

    // Java parity: getWorldMapInstance()
    public WorldMapInstance GetWorldMapInstance() => _mapRegion!.GetParent();

    // Java parity: isSpawned()
    public bool IsSpawned() => _isSpawned;

    // Java parity: setIsSpawned(boolean) — package-private in Java.
    internal void SetIsSpawned(bool val) => _isSpawned = val;

    // Java parity: setMapRegion(MapRegion) — package-private in Java.
    internal void SetMapRegion(MapRegion r) => _mapRegion = r;

    // Java parity: setXYZH(Float, Float, Float, Byte) — null args leave the value unchanged.
    public void SetXYZH(float? newX, float? newY, float? newZ, byte? newHeading)
    {
        if (newX != null) _x = newX.Value;
        if (newY != null) _y = newY.Value;
        if (newZ != null) _z = newZ.Value;
        if (newHeading != null) _heading = newHeading.Value;
    }

    // Java parity: setZ(float)
    public void SetZ(float z) => _z = z;

    // Java parity: setH(byte)
    public void SetH(byte h) => _heading = h;

    // C#-convenience data accessors for legacy packet-path readers (same underlying fields).
    public int WorldId => _mapId;
    public float X => _x;
    public float Y => _y;
    public float Z => _z;
    public byte Heading => _heading;
    public int InstanceId => GetInstanceId();

    // Java parity: hashCode()
    public override int GetHashCode()
    {
        const int prime = 31;
        int result = 1;
        result = prime * result + _heading;
        result = prime * result + _mapId;
        result = prime * result + BitConverter.SingleToInt32Bits(_x);
        result = prime * result + BitConverter.SingleToInt32Bits(_y);
        result = prime * result + BitConverter.SingleToInt32Bits(_z);
        return result;
    }

    // Java parity: equals(Object)
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;
        if (obj is not WorldPosition other)
            return false;
        return _mapId == other._mapId && _x == other._x && _y == other._y && _z == other._z && _heading == other._heading;
    }

    // Java parity: toString()
    public override string ToString() =>
        "WorldPosition [mapId=" + _mapId + ", x=" + _x + ", y=" + _y + ", z=" + _z + ", heading=" + _heading + ", isSpawned=" + _isSpawned + "]";

    // Java parity: toCoordString()
    public string ToCoordString() =>
        "Map ID: " + _mapId + ", Instance ID: " + GetInstanceId() + "\nX: " + _x + ", Y: " + _y + ", Z: " + _z + ", Heading: " + _heading;
}
