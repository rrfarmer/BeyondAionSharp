using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Shield;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/ShieldService (xavier, Rolandas, SVDNESS). Map.of/Set.of→Dictionary/HashSet; ConcurrentHashMap→ConcurrentDictionary; computeIfAbsent→GetOrAdd; Map.get→GetValueOrDefault, Map.remove→TryRemove; List.remove(idx)→RemoveAt; instanceof BoundingBox bb→is; switch-arrows→switch; stream().anyMatch→Any; slf4j parameterized warn→LogWarning. ShieldObserver/Spatial/BoundingBox/Vector3f red-tolerated.</summary>
public class ShieldService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ShieldService));
    private static readonly Dictionary<int, ISet<string>> IGNORED_SHIELDS_BY_MAP_ID = new Dictionary<int, ISet<string>>
    {
        { 310100000, new HashSet<string> { "BU_AB_CASTLESHIELD_SAMJUNG_03C_TYPE2_487543" } }, // Azoturan Fortress
        { 400010000, new HashSet<string> { "BU_AB_SAMJUNG_BASE_01_SHIELD_313626", "BU_AB_SAMJUNG_BASE_01_SHIELD_299314", "BU_AB_SAMJUNG_BASE_01_SHIELD_137227" } } // artifact
    };
    private readonly ConcurrentDictionary<int, ShieldTemplate> sphereShields = new ConcurrentDictionary<int, ShieldTemplate>();
    private readonly ConcurrentDictionary<int, List<SiegeShield>> registeredShields = new ConcurrentDictionary<int, List<SiegeShield>>();

    private ShieldService()
    {
        foreach (ShieldTemplate template in DataManager.SHIELD_DATA.GetShieldTemplates())
        {
            sphereShields[template.GetId()] = template;
        }
    }

    public void LogDetachedShields()
    {
        foreach (KeyValuePair<int, List<SiegeShield>> kv in registeredShields)
        {
            if (kv.Value.Count != 0)
                log.LogWarning("{Count} geo shield(s) are not attached to a SiegeLocation on map {MapId}: {Shields}", kv.Value.Count, kv.Key, kv.Value);
        }
    }

    public ShieldObserver CreateShieldObserver(FortressLocation location, Creature observed)
    {
        ShieldTemplate template = sphereShields.GetValueOrDefault(location.GetLocationId());
        return template == null ? null : new ShieldObserver(location, template, observed);
    }

    /// <summary>Registers geo shield for zone lookup</summary>
    public SiegeShield TryRegisterShield(int worldId, Spatial geometry)
    {
        if (!GeoDataConfig.GEO_SHIELDS_ENABLE || IsIgnored(worldId, geometry.GetName()))
            return null;
        SiegeShield shield = new SiegeShield(geometry);
        registeredShields.GetOrAdd(worldId, _ => new List<SiegeShield>()).Add(shield);
        return shield;
    }

    /// <summary>Attaches geo shield and removes obsolete sphere shield if such exists. Should be called when geo shields and SiegeZoneInstance were created.</summary>
    public void AttachShield(SiegeLocation location)
    {
        var mapId = location.GetTemplate().GetWorldId();
        var mapShields = registeredShields.GetValueOrDefault(mapId);
        if (mapShields == null)
        {
            return;
        }
        List<SiegeShield> attached = new List<SiegeShield>();
        for (int i = mapShields.Count - 1; i >= 0; i--)
        {
            var shield = mapShields[i];
            if (IsShieldInsideLocation(shield, location))
            {
                attached.Add(shield);
                mapShields.RemoveAt(i);
                sphereShields.TryRemove(location.GetLocationId(), out _);
                shield.SetSiegeLocationId(location.GetLocationId());
            }
        }
        if (attached.Count == 0 && location.GetType_() != SiegeType.OUTPOST && location.GetLocationId() != 1241) // Outposts and Miren don't have shields
            log.LogWarning("Could not find a shield for location ID {LocationId}.", location.GetLocationId());
    }

    private bool IsShieldInsideLocation(SiegeShield shield, SiegeLocation location)
    {
        var wb = shield.GetGeometry().GetWorldBound();
        var center = wb.GetCenter();
        if (location.IsInsideLocation(center.GetX(), center.GetY(), center.GetZ()))
            return true;
        if (wb is BoundingBox bb)
        {
            var min = bb.GetMin(null);
            var max = bb.GetMax(null);
            switch (shield.GetGeometry().GetName())
            {
                case "PR_A_AIRBUNKER_EFFECT_01A_CHILD1_324011":
                case "PR_A_AIRBUNKER_EFFECT_01A_CHILD2_324011":
                    min.z -= 6;
                    break;
            }
            RectangleArea rectangleArea = new RectangleArea(null, 0, min.x, min.y, max.x, max.y, min.z, max.z);
            if (location.GetZone().Any(z => z.GetAreaTemplate().IntersectsRectangle(rectangleArea)))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsIgnored(int mapId, string geometryName)
    {
        var ignoredShields = IGNORED_SHIELDS_BY_MAP_ID.GetValueOrDefault(mapId);
        return ignoredShields != null && ignoredShields.Contains(geometryName);
    }

    private static class SingletonHolder
    {
        internal static readonly ShieldService instance = new ShieldService();
    }

    public static ShieldService GetInstance()
    {
        return SingletonHolder.instance;
    }
}
