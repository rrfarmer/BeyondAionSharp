using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Scripting;
using Aion.Commons.Scripting.ClassListener;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Materials;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Model.Vortex;
using Aion.GameServer.Services;
using Aion.GameServer.World.Zone.Handler;

namespace Aion.GameServer.World.Zone;

/// <summary>
/// Java parity: world/zone/ZoneService (ATracer, antness). Builds per-map ZoneInstances and registers zone/material handlers. Twin of
/// InstanceEngine: implements GameEngine via Java-faithful Init() (the reworked async GameEngine contract is red-tolerated, matching
/// InstanceEngine). Class&lt;? extends ZoneHandler&gt; -> Type; getAnnotation -> GetCustomAttribute; newInstance -> Activator.CreateInstance;
/// switch-case on ZoneClassName (PascalCase); synchronized -> lock; SingletonHolder nested. Most zone/data deps red-tolerated.
/// </summary>
public sealed class ZoneService : GameEngine
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ZoneService));
    private readonly Dictionary<ZoneName, Type> zoneHandlers = new Dictionary<ZoneName, Type>();
    private readonly Dictionary<ZoneName, IZoneHandler> collidableHandlers = new Dictionary<ZoneName, IZoneHandler>();
    private readonly IDictionary<int, List<ZoneInfo>> zoneByMapIdMap = DataManager.ZONE_DATA.GetZones();
    private readonly object sync = new object();

    private ZoneService()
    {
    }

    public void Init()
    {
        ScriptManager scriptManager = new ScriptManager();
        AggregatedClassListener acl = new AggregatedClassListener();
        acl.AddClassListener(new OnClassLoadUnloadListener());
        acl.AddClassListener(new ZoneHandlerClassListener());
        scriptManager.SetGlobalClassListener(acl);
        scriptManager.Load(WorldConfig.ZONE_HANDLER_DIRECTORY);
        log.LogInformation("Loaded " + zoneHandlers.Count + " zone handlers.");
    }

    public IZoneHandler GetNewZoneHandler(ZoneName zoneName)
    {
        IZoneHandler zoneHandler = collidableHandlers.GetValueOrDefault(zoneName);
        if (zoneHandler != null)
            return zoneHandler;
        Type zoneClass = zoneHandlers.GetValueOrDefault(zoneName);
        if (zoneClass != null)
        {
            try
            {
                zoneHandler = (IZoneHandler)Activator.CreateInstance(zoneClass);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Can't instantiate zone handler " + zoneName);
            }
        }

        return zoneHandler != null ? zoneHandler : new GeneralZoneHandler();
    }

    public void AddZoneHandlerClass(Type handler)
    {
        ZoneNameAnnotation idAnnotation = handler.GetCustomAttribute<ZoneNameAnnotation>();
        if (idAnnotation != null)
        {
            string[] zoneNames = idAnnotation.Value.Split(' ');
            foreach (string zoneNameString in zoneNames)
            {
                try
                {
                    ZoneName zoneName = ZoneName.Get(zoneNameString.Trim());
                    if (zoneName == ZoneName.Get("NONE"))
                        throw new Exception();
                    zoneHandlers[zoneName] = handler;
                }
                catch (Exception)
                {
                    log.LogWarning("Missing ZoneName: " + idAnnotation.Value);
                }
            }
        }
    }

    public void AddZoneHandlerClass(ZoneName zoneName, Type handler)
    {
        zoneHandlers[zoneName] = handler;
    }

    public Dictionary<ZoneName, ZoneInstance> GetZoneInstancesByWorldId(int mapId)
    {
        Dictionary<ZoneName, ZoneInstance> zones = new Dictionary<ZoneName, ZoneInstance>();
        int worldSize = DataManager.WORLD_MAPS_DATA.GetTemplate(mapId).GetWorldSize();
        WorldZoneTemplate zone = new WorldZoneTemplate(worldSize, mapId);
        PolyArea fullArea = new PolyArea(zone.GetName(), mapId, zone.GetPoints().GetPoint(), zone.GetPoints().GetBottom(), zone.GetPoints().GetTop());
        ZoneInstance fullMap = new ZoneInstance(mapId, new ZoneInfo(fullArea, zone));
        fullMap.AddHandler(GetNewZoneHandler(zone.GetName()));
        zones[zone.GetName()] = fullMap;

        List<ZoneInfo> areas = this.zoneByMapIdMap.GetValueOrDefault(mapId);
        if (areas == null)
            return zones;

        foreach (ZoneInfo area in areas)
        {
            ZoneInstance instance;
            switch (area.GetZoneTemplate().GetZoneType())
            {
                case ZoneClassName.Fly:
                    instance = new FlyZoneInstance(mapId, area);
                    break;
                case ZoneClassName.NoFly:
                    instance = new NoFlyZoneInstance(mapId, area);
                    break;
                case ZoneClassName.Fort:
                    instance = new SiegeZoneInstance(mapId, area);
                    SiegeLocation siege = DataManager.SIEGE_LOCATION_DATA.GetSiegeLocations().GetValueOrDefault(area.GetZoneTemplate().GetSiegeId()[0]);
                    if (siege != null)
                    {
                        siege.AddZone((SiegeZoneInstance)instance);
                        ShieldService.GetInstance().AttachShield(siege);
                    }
                    break;
                case ZoneClassName.Artifact:
                    instance = new SiegeZoneInstance(mapId, area);
                    foreach (int artifactId in area.GetZoneTemplate().GetSiegeId())
                    {
                        SiegeLocation artifact = DataManager.SIEGE_LOCATION_DATA.GetArtifacts().GetValueOrDefault(artifactId);
                        if (artifact == null)
                        {
                            log.LogWarning("Missing siege location data for zone " + area.GetZoneTemplate().GetName().Name);
                        }
                        else
                        {
                            artifact.AddZone((SiegeZoneInstance)instance);
                        }
                    }
                    break;
                case ZoneClassName.Pvp:
                    instance = new PvPZoneInstance(mapId, area);
                    break;
                default:
                    InvasionZoneInstance invasionZone = GetIZI(area);
                    if (invasionZone != null)
                    {
                        instance = invasionZone;
                    }
                    else
                    {
                        instance = new ZoneInstance(mapId, area);
                    }
                    break;
            }
            instance.AddHandler(GetNewZoneHandler(area.GetZoneTemplate().GetName()));
            zones[area.GetZoneTemplate().GetName()] = instance;
        }
        return zones;
    }

    private InvasionZoneInstance GetIZI(ZoneInfo area)
    {
        if (area.GetZoneTemplate().GetName().Name.Equals("WAILING_CLIFFS_220050000")
            || area.GetZoneTemplate().GetName().Name.Equals("BALTASAR_CEMETERY_220050000")
            || area.GetZoneTemplate().GetName().Name.Equals("THE_LEGEND_SHRINE_220050000")
            || area.GetZoneTemplate().GetName().Name.Equals("SUDORVILLE_220050000")
            || area.GetZoneTemplate().GetName().Name.Equals("BALTASAR_HILL_VILLAGE_220050000")
            || area.GetZoneTemplate().GetName().Name.Equals("BRUSTHONIN_MITHRIL_MINE_220050000"))
        {
            return ValidateZone(area);
        }
        else if (area.GetZoneTemplate().GetName().Name.Equals("JAMANOK_INN_210060000")
            || area.GetZoneTemplate().GetName().Name.Equals("THE_STALKING_GROUNDS_210060000")
            || area.GetZoneTemplate().GetName().Name.Equals("BLACK_ROCK_HOT_SPRING_210060000")
            || area.GetZoneTemplate().GetName().Name.Equals("FREGIONS_FLAME_210060000"))
        {
            return ValidateZone(area);
        }
        return null;
    }

    private InvasionZoneInstance ValidateZone(ZoneInfo area)
    {
        int mapId = area.GetZoneTemplate().GetMapid();
        VortexLocation vortex = DataManager.VORTEX_DATA.GetVortexLocation(mapId);
        if (vortex != null)
        {
            InvasionZoneInstance instance = new InvasionZoneInstance(mapId, area);
            vortex.AddZone(instance);
            return instance;
        }
        return null;
    }

    public void SaveMaterialZones()
    {
        List<ZoneTemplate> templates = new List<ZoneTemplate>();
        foreach (WorldMapTemplate map in DataManager.WORLD_MAPS_DATA)
        {
            List<ZoneInfo> areas = this.zoneByMapIdMap.GetValueOrDefault(map.GetMapId());
            if (areas == null)
                continue;
            foreach (ZoneInfo zone in areas)
            {
                if (collidableHandlers.ContainsKey(zone.GetArea().GetZoneName()))
                {
                    templates.Add(zone.GetZoneTemplate());
                }
            }
        }
        templates.Sort((a, b) => a.GetMapid().CompareTo(b.GetMapid()));

        ZoneData zoneData = new ZoneData();
        zoneData.zoneList = templates;
        zoneData.SaveData();
    }

    public void CreateMaterialZoneTemplate(Spatial geometry, int worldId, ZoneName zoneName)
    {
        lock (sync)
        {
            if (zoneName == ZoneName.None)
                return;

            IZoneHandler handler = collidableHandlers.GetValueOrDefault(zoneName);
            if (handler == null)
            {
                if (geometry.GetMaterialId() == 11)
                {
                    handler = ShieldService.GetInstance().TryRegisterShield(worldId, geometry);
                    if (handler == null)
                        return;
                }
                else
                {
                    MaterialTemplate template = DataManager.MATERIAL_DATA.GetTemplate(geometry.GetMaterialId());
                    if (template == null)
                        return;
                    handler = new MaterialZoneHandler(geometry, template);
                }
                collidableHandlers[zoneName] = handler;
            }
            else
            {
                log.LogWarning("Duplicate material mesh: " + zoneName.ToString());
            }

            List<ZoneInfo> areas = zoneByMapIdMap.GetValueOrDefault(worldId);
            if (areas == null)
            {
                areas = new List<ZoneInfo>();
                zoneByMapIdMap[worldId] = areas;
            }
            ZoneInfo zoneInfo = null;
            foreach (ZoneInfo area in areas)
            {
                if (area.GetZoneTemplate().GetName().Equals(zoneName))
                {
                    zoneInfo = area;
                    break;
                }
            }
            if (zoneInfo == null)
            {
                MaterialZoneTemplate zoneTemplate = new MaterialZoneTemplate(geometry, worldId);
                // maybe add to zone data if needed search ?
                Area zoneInfoArea = null;
                if (zoneTemplate.GetSphere() != null)
                {
                    zoneInfoArea = new SphereArea(zoneName, worldId, zoneTemplate.GetSphere().GetX(), zoneTemplate.GetSphere().GetY(),
                        zoneTemplate.GetSphere().GetZ(), zoneTemplate.GetSphere().GetR());
                }
                else if (zoneTemplate.GetCylinder() != null)
                {
                    zoneInfoArea = new CylinderArea(zoneName, worldId, zoneTemplate.GetCylinder().GetX(), zoneTemplate.GetCylinder().GetY(),
                        zoneTemplate.GetCylinder().GetR(), zoneTemplate.GetCylinder().GetBottom(), zoneTemplate.GetCylinder().GetTop());
                }
                else if (zoneTemplate.GetSemisphere() != null)
                {
                    zoneInfoArea = new SemisphereArea(zoneName, worldId, zoneTemplate.GetSemisphere().GetX(), zoneTemplate.GetSemisphere().GetY(),
                        zoneTemplate.GetSemisphere().GetZ(), zoneTemplate.GetSemisphere().GetR());
                }
                if (zoneInfoArea != null)
                {
                    zoneInfo = new ZoneInfo(zoneInfoArea, zoneTemplate);
                    areas.Add(zoneInfo);
                }
            }
        }
    }

    public static ZoneService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly ZoneService instance = new ZoneService();
    }
}
