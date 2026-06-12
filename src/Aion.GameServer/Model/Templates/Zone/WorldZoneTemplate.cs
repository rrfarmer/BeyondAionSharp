using System;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Model.Templates.Zone;

/// <summary>Java parity: model/templates/zone/WorldZoneTemplate (Rolandas) : ZoneTemplate. Math.round(float)→(int)Math.Floor(x+0.5f); Point2D.x/y→X/Y props; ZoneTemplate fields→public props. WorldConfig red-tolerated.</summary>
public class WorldZoneTemplate : ZoneTemplate
{
    public WorldZoneTemplate(int size, int mapId)
    {
        float maxZ = (int)System.Math.Floor((float)size / WorldConfig.WORLD_REGION_SIZE + 0.5f) * WorldConfig.WORLD_REGION_SIZE;
        Points = new Points(-1, maxZ + 1);
        Point2D point = new();
        point.X = -1;
        point.Y = -1;
        Points.GetPoint().Add(point);
        point = new Point2D();
        point.X = -1;
        point.Y = size + 1;
        Points.GetPoint().Add(point);
        point = new Point2D();
        point.X = size + 1;
        point.Y = size + 1;
        Points.GetPoint().Add(point);
        point = new Point2D();
        point.X = size + 1;
        point.Y = -1;
        Points.GetPoint().Add(point);
        ZoneType = ZoneClassName.DUMMY;
        Mapid = mapId;
        Flags = DataManager.WORLD_MAPS_DATA.GetTemplate(mapId).GetFlags();
        XmlName = mapId.ToString();
    }
}
