using System;
using System.Collections.Generic;
using System.Reflection;
using Aion.GameServer.Model;

namespace Aion.GameServer.Services.Rift;

/// <summary>Java parity: services/rift/RiftEnum (Source). Value-carrying Java enum -> sealed class with static readonly named instances (SCREAMING_SNAKE preserved) + Values() array; constructor overloads chain like Java; getRift(id)/getVortex(race) static lookups throw ArgumentException (IllegalArgumentException). Race red-tolerated.</summary>
public sealed class RiftEnum
{
    public static readonly RiftEnum KAISINEL_AM = new RiftEnum(1170, "KAISINEL_AM", "KAISINEL_AS", 24, 45, 65, Race.ASMODIANS, true);
    public static readonly RiftEnum ELTNEN_AM = new RiftEnum(2120, "ELTNEN_AM", "MORHEIM_AS", 12, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum ELTNEN_BM = new RiftEnum(2121, "ELTNEN_BM", "MORHEIM_BS", 20, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum ELTNEN_CM = new RiftEnum(2122, "ELTNEN_CM", "MORHEIM_CS", 35, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum ELTNEN_DM = new RiftEnum(2123, "ELTNEN_DM", "MORHEIM_DS", 35, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum ELTNEN_EM = new RiftEnum(2124, "ELTNEN_EM", "MORHEIM_ES", 45, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum ELTNEN_FM = new RiftEnum(2125, "ELTNEN_FM", "MORHEIM_FS", 50, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum ELTNEN_GM = new RiftEnum(2126, "ELTNEN_GM", "MORHEIM_GS", 50, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum HEIRON_AM = new RiftEnum(2140, "HEIRON_AM", "BELUSLAN_AS", 24, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum HEIRON_BM = new RiftEnum(2141, "HEIRON_BM", "BELUSLAN_BS", 36, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum HEIRON_CM = new RiftEnum(2142, "HEIRON_CM", "BELUSLAN_CS", 48, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum HEIRON_DM = new RiftEnum(2143, "HEIRON_DM", "BELUSLAN_DS", 48, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum HEIRON_EM = new RiftEnum(2144, "HEIRON_EM", "BELUSLAN_ES", 60, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum HEIRON_FM = new RiftEnum(2145, "HEIRON_FM", "BELUSLAN_FS", 72, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum HEIRON_GM = new RiftEnum(2146, "HEIRON_GM", "BELUSLAN_GS", 72, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum INGGISON_AM = new RiftEnum(2150, "INGGISON_AM", "GELKMAROS_AS", 150, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum INGGISON_BM = new RiftEnum(2151, "INGGISON_BM", "GELKMAROS_BS", 150, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum INGGISON_CM = new RiftEnum(2152, "INGGISON_CM", "GELKMAROS_CS", 150, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum INGGISON_DM = new RiftEnum(2153, "INGGISON_DM", "GELKMAROS_DS", 150, 20, 65, Race.ASMODIANS);
    public static readonly RiftEnum CYGNEA_AM = new RiftEnum(2170, "CYGNEA_AM", "ENSHAR_AS", 12, 50, 65, Race.ASMODIANS);
    public static readonly RiftEnum CYGNEA_BM = new RiftEnum(2171, "CYGNEA_BM", "ENSHAR_BS", 36, 50, 65, Race.ASMODIANS);
    public static readonly RiftEnum CYGNEA_CM = new RiftEnum(2172, "CYGNEA_CM", "ENSHAR_CS", 48, 55, 65, Race.ASMODIANS);
    public static readonly RiftEnum CYGNEA_DM = new RiftEnum(2173, "CYGNEA_DM", "ENSHAR_DS", 48, 55, 65, Race.ASMODIANS);
    public static readonly RiftEnum CYGNEA_EM = new RiftEnum(2174, "CYGNEA_EM", "ENSHAR_ES", 48, 55, 65, Race.ASMODIANS);
    public static readonly RiftEnum CYGNEA_FM = new RiftEnum(2175, "CYGNEA_FM", "ENSHAR_FS", 48, 55, 65, Race.ASMODIANS);
    public static readonly RiftEnum CYGNEA_GM = new RiftEnum(2176, "CYGNEA_GM", "ENSHAR_GS", 144, 60, 65, Race.ASMODIANS, false, true);
    public static readonly RiftEnum CYGNEA_HM = new RiftEnum(2177, "CYGNEA_HM", "ENSHAR_HS", 144, 60, 65, Race.ASMODIANS, false, true);
    public static readonly RiftEnum CYGNEA_IM = new RiftEnum(2178, "CYGNEA_IM", "ENSHAR_IS", 144, 60, 65, Race.ASMODIANS, false, true);
    public static readonly RiftEnum CYGNEA_VIL1M = new RiftEnum(2189, "CYGNEA_VIL1M", "ENSHAR_VIL1S", 72, 55, 65, Race.ASMODIANS, false, false, true);
    public static readonly RiftEnum CYGNEA_VIL2M = new RiftEnum(2190, "CYGNEA_VIL2M", "ENSHAR_VIL2S", 72, 55, 65, Race.ASMODIANS, false, false, true);
    public static readonly RiftEnum CYGNEA_VIL3M = new RiftEnum(2191, "CYGNEA_VIL3M", "ENSHAR_VIL3S", 72, 55, 65, Race.ASMODIANS, false, false, true);
    public static readonly RiftEnum MARCHUTAN_AM = new RiftEnum(1280, "MARCHUTAN_AM", "MARCHUTAN_AS", 24, 45, 65, Race.ELYOS, true);
    public static readonly RiftEnum MORHEIM_AM = new RiftEnum(2220, "MORHEIM_AM", "ELTNEN_AS", 12, 20, 65, Race.ELYOS);
    public static readonly RiftEnum MORHEIM_BM = new RiftEnum(2221, "MORHEIM_BM", "ELTNEN_BS", 20, 20, 65, Race.ELYOS);
    public static readonly RiftEnum MORHEIM_CM = new RiftEnum(2222, "MORHEIM_CM", "ELTNEN_CS", 35, 20, 65, Race.ELYOS);
    public static readonly RiftEnum MORHEIM_DM = new RiftEnum(2223, "MORHEIM_DM", "ELTNEN_DS", 35, 20, 65, Race.ELYOS);
    public static readonly RiftEnum MORHEIM_EM = new RiftEnum(2224, "MORHEIM_EM", "ELTNEN_ES", 45, 20, 65, Race.ELYOS);
    public static readonly RiftEnum MORHEIM_FM = new RiftEnum(2225, "MORHEIM_FM", "ELTNEN_FS", 50, 20, 65, Race.ELYOS);
    public static readonly RiftEnum MORHEIM_GM = new RiftEnum(2226, "MORHEIM_GM", "ELTNEN_GS", 50, 20, 65, Race.ELYOS);
    public static readonly RiftEnum BELUSLAN_AM = new RiftEnum(2240, "BELUSLAN_AM", "HEIRON_AS", 24, 20, 65, Race.ELYOS);
    public static readonly RiftEnum BELUSLAN_BM = new RiftEnum(2241, "BELUSLAN_BM", "HEIRON_BS", 36, 20, 65, Race.ELYOS);
    public static readonly RiftEnum BELUSLAN_CM = new RiftEnum(2242, "BELUSLAN_CM", "HEIRON_CS", 48, 20, 65, Race.ELYOS);
    public static readonly RiftEnum BELUSLAN_DM = new RiftEnum(2243, "BELUSLAN_DM", "HEIRON_DS", 48, 20, 65, Race.ELYOS);
    public static readonly RiftEnum BELUSLAN_EM = new RiftEnum(2244, "BELUSLAN_EM", "HEIRON_ES", 60, 20, 65, Race.ELYOS);
    public static readonly RiftEnum BELUSLAN_FM = new RiftEnum(2245, "BELUSLAN_FM", "HEIRON_FS", 72, 20, 65, Race.ELYOS);
    public static readonly RiftEnum BELUSLAN_GM = new RiftEnum(2246, "BELUSLAN_GM", "HEIRON_GS", 72, 20, 65, Race.ELYOS);
    public static readonly RiftEnum GELKMAROS_AM = new RiftEnum(2270, "GELKMAROS_AM", "INGGISON_AS", 150, 20, 65, Race.ELYOS);
    public static readonly RiftEnum GELKMAROS_BM = new RiftEnum(2271, "GELKMAROS_BM", "INGGISON_BS", 150, 20, 65, Race.ELYOS);
    public static readonly RiftEnum GELKMAROS_CM = new RiftEnum(2272, "GELKMAROS_CM", "INGGISON_CS", 150, 20, 65, Race.ELYOS);
    public static readonly RiftEnum GELKMAROS_DM = new RiftEnum(2273, "GELKMAROS_DM", "INGGISON_DS", 150, 20, 65, Race.ELYOS);
    public static readonly RiftEnum ENSHAR_AM = new RiftEnum(2280, "ENSHAR_AM", "CYGNEA_AS", 12, 50, 65, Race.ELYOS);
    public static readonly RiftEnum ENSHAR_BM = new RiftEnum(2281, "ENSHAR_BM", "CYGNEA_BS", 36, 50, 65, Race.ELYOS);
    public static readonly RiftEnum ENSHAR_CM = new RiftEnum(2282, "ENSHAR_CM", "CYGNEA_CS", 48, 55, 65, Race.ELYOS);
    public static readonly RiftEnum ENSHAR_DM = new RiftEnum(2283, "ENSHAR_DM", "CYGNEA_DS", 48, 55, 65, Race.ELYOS);
    public static readonly RiftEnum ENSHAR_EM = new RiftEnum(2284, "ENSHAR_EM", "CYGNEA_ES", 48, 55, 65, Race.ELYOS);
    public static readonly RiftEnum ENSHAR_FM = new RiftEnum(2285, "ENSHAR_FM", "CYGNEA_FS", 48, 55, 65, Race.ELYOS);
    public static readonly RiftEnum ENSHAR_GM = new RiftEnum(2286, "ENSHAR_GM", "CYGNEA_GS", 144, 60, 65, Race.ELYOS, false, true);
    public static readonly RiftEnum ENSHAR_HM = new RiftEnum(2287, "ENSHAR_HM", "CYGNEA_HS", 144, 60, 65, Race.ELYOS, false, true);
    public static readonly RiftEnum ENSHAR_IM = new RiftEnum(2288, "ENSHAR_IM", "CYGNEA_IS", 144, 60, 65, Race.ELYOS, false, true);
    public static readonly RiftEnum ENSHAR_VIL1M = new RiftEnum(2289, "ENSHAR_VIL1M", "CYGNEA_VIL1S", 72, 55, 65, Race.ELYOS, false, false, true);
    public static readonly RiftEnum ENSHAR_VIL2M = new RiftEnum(2290, "ENSHAR_VIL2M", "CYGNEA_VIL2S", 72, 55, 65, Race.ELYOS, false, false, true);
    public static readonly RiftEnum ENSHAR_VIL3M = new RiftEnum(2291, "ENSHAR_VIL3M", "CYGNEA_VIL3S", 72, 55, 65, Race.ELYOS, false, false, true);

    private static readonly RiftEnum[] _values =
    {
        KAISINEL_AM, ELTNEN_AM, ELTNEN_BM, ELTNEN_CM, ELTNEN_DM, ELTNEN_EM, ELTNEN_FM, ELTNEN_GM,
        HEIRON_AM, HEIRON_BM, HEIRON_CM, HEIRON_DM, HEIRON_EM, HEIRON_FM, HEIRON_GM,
        INGGISON_AM, INGGISON_BM, INGGISON_CM, INGGISON_DM,
        CYGNEA_AM, CYGNEA_BM, CYGNEA_CM, CYGNEA_DM, CYGNEA_EM, CYGNEA_FM, CYGNEA_GM, CYGNEA_HM, CYGNEA_IM,
        CYGNEA_VIL1M, CYGNEA_VIL2M, CYGNEA_VIL3M,
        MARCHUTAN_AM, MORHEIM_AM, MORHEIM_BM, MORHEIM_CM, MORHEIM_DM, MORHEIM_EM, MORHEIM_FM, MORHEIM_GM,
        BELUSLAN_AM, BELUSLAN_BM, BELUSLAN_CM, BELUSLAN_DM, BELUSLAN_EM, BELUSLAN_FM, BELUSLAN_GM,
        GELKMAROS_AM, GELKMAROS_BM, GELKMAROS_CM, GELKMAROS_DM,
        ENSHAR_AM, ENSHAR_BM, ENSHAR_CM, ENSHAR_DM, ENSHAR_EM, ENSHAR_FM, ENSHAR_GM, ENSHAR_HM, ENSHAR_IM,
        ENSHAR_VIL1M, ENSHAR_VIL2M, ENSHAR_VIL3M
    };

    private string name; // assigned via reflection in static ctor to mirror Java enum.name()
    private readonly int id;
    private readonly string master;
    private readonly string slave;
    private readonly int entries;
    private readonly int minLevel;
    private readonly int maxLevel;
    private readonly Race destination;
    private readonly bool vortex;
    private readonly bool canBeVolatileField;
    private readonly bool isInvasionRift;

    private RiftEnum(int id, string master, string slave, int entries, int minLevel, int maxLevel, Race destination)
        : this(id, master, slave, entries, minLevel, maxLevel, destination, false, false, false)
    {
    }

    private RiftEnum(int id, string master, string slave, int entries, int minLevel, int maxLevel, Race destination, bool vortex)
        : this(id, master, slave, entries, minLevel, maxLevel, destination, vortex, false, false)
    {
    }

    private RiftEnum(int id, string master, string slave, int entries, int minLevel, int maxLevel, Race destination, bool vortex, bool canBeVolatile)
        : this(id, master, slave, entries, minLevel, maxLevel, destination, vortex, canBeVolatile, false)
    {
    }

    private RiftEnum(int id, string master, string slave, int entries, int minLevel, int maxLevel, Race destination, bool vortex,
        bool canBeVolatile, bool isInvasionRift)
    {
        this.id = id;
        this.master = master;
        this.slave = slave;
        this.entries = entries;
        this.minLevel = minLevel;
        this.maxLevel = maxLevel;
        this.destination = destination;
        this.vortex = vortex;
        this.canBeVolatileField = canBeVolatile;
        this.isInvasionRift = isInvasionRift;
    }

    static RiftEnum()
    {
        foreach (FieldInfo f in typeof(RiftEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (f.FieldType == typeof(RiftEnum))
                ((RiftEnum)f.GetValue(null)).name = f.Name;
        }
    }

    public string Name()
    {
        return name;
    }

    public static RiftEnum[] Values()
    {
        return _values;
    }

    public static RiftEnum GetRift(int id)
    {
        foreach (RiftEnum rift in Values())
        {
            if (rift.GetId() == id)
            {
                return rift;
            }
        }
        throw new ArgumentException("Unsupported rift id: " + id);
    }

    public static RiftEnum GetVortex(Race race)
    {
        foreach (RiftEnum rift in Values())
        {
            if (rift.IsVortex() && rift.GetDestination().Equals(race))
            {
                return rift;
            }
        }
        throw new ArgumentException("Unsupported vortex race: " + race);
    }

    public int GetId()
    {
        return id;
    }

    public string GetMaster()
    {
        return master;
    }

    public string GetSlave()
    {
        return slave;
    }

    public int GetEntries()
    {
        return entries;
    }

    public int GetMinLevel()
    {
        return minLevel;
    }

    public int GetMaxLevel()
    {
        return maxLevel;
    }

    public Race GetDestination()
    {
        return destination;
    }

    public bool IsVortex()
    {
        return vortex;
    }

    public bool CanBeVolatile()
    {
        return canBeVolatileField;
    }

    public bool IsInvasionRift()
    {
        return isInvasionRift;
    }
}
