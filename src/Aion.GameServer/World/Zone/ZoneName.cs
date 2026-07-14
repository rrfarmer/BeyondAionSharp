using System.Collections.Concurrent;
using Aion.GameServer.Utils;

namespace Aion.GameServer.World.Zone;

/// <summary>
/// Interned zone name registry — zones are identified by upper-cased string names.
/// Java parity: world/zone/ZoneName.
/// </summary>
public sealed class ZoneName
{
    private static readonly ConcurrentDictionary<string, ZoneName> _zoneNames = new();

    // Java parity: ZoneName.NONE
    public static readonly ZoneName None = new("NONE");

    // Java-name alias (call sites use the Java SCREAMING_SNAKE name; same instance, additive).
    public static ZoneName NONE => None;

    static ZoneName()
    {
        _zoneNames[None.Name] = None;
    }

    private readonly string _name;

    private ZoneName(string name)
    {
        _name = name;
    }

    // Java parity: name()
    public string Name => _name;

    // Java parity: id() is stable Java String.hashCode(), used as MapRegion's final zone-ordering key.
    public int Id() => JavaString.HashCode(_name);

    // Java parity: createOrGet(String name)
    public static ZoneName CreateOrGet(string name) =>
        _zoneNames.GetOrAdd(name.ToUpperInvariant(), static n => new ZoneName(n));

    // Java parity: getId(String name)
    public static int GetId(string name) =>
        _zoneNames.GetValueOrDefault(name.ToUpperInvariant(), None).Id();

    // Java parity: get(String name) — returns NONE and warns if name not found
    public static ZoneName Get(string name)
    {
        name = name.ToUpperInvariant();
        if (!_zoneNames.TryGetValue(name, out var zoneName))
        {
            // Java parity: log.warn("Missing zone : " + name)
            Console.Error.WriteLine($"[ZoneName] Missing zone: {name}");
            return None;
        }
        return zoneName;
    }

    public override string ToString() => _name;
}
