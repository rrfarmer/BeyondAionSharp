using System;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Scene;

namespace Aion.GameServer.Model.Templates.Zone;

/// <summary>Java parity: model/templates/zone/MaterialZoneTemplate (Rolandas, Neon) : ZoneTemplate. ZoneTemplate protected fields→public props (Mapid/Flags/AreaType/Cylinder/Semisphere/Sphere/XmlName); Math.sqrt→Math.Sqrt; Vector3f.x→.X. geoEngine BoundingBox/Vector3f/Spatial red-tolerated.</summary>
public class MaterialZoneTemplate : ZoneTemplate
{
    public MaterialZoneTemplate(Spatial geometry, int mapId)
    {
        Mapid = mapId;
        Flags = DataManager.WORLD_MAPS_DATA.GetTemplate(mapId).GetFlags();
        XmlName = geometry.GetName() + "_" + mapId;
        BoundingBox box = (BoundingBox)geometry.GetWorldBound();
        Vector3f center = box.GetCenter();
        // don't use polygons for small areas, they are bugged in Java API
        if (geometry.GetName().Contains("CYLINDER") || geometry.GetName().Contains("CONE") || geometry.GetName().Contains("H_COLUME"))
        {
            AreaType = AreaType.Cylinder;
            float r = (float)System.Math.Sqrt(box.GetXExtent() * box.GetXExtent() + box.GetYExtent() * box.GetYExtent());
            Cylinder = new Cylinder(center.X, center.Y, r + 1, center.Z + box.GetZExtent() + 1, center.Z - box.GetZExtent() - 1);
        }
        else if (geometry.GetName().Contains("SEMISPHERE"))
        {
            AreaType = AreaType.Semisphere;
            Semisphere = new Semisphere(center.X, center.Y, center.Z, CalculateDistanceFromCenterToCorner(box) + 1);
        }
        else
        {
            AreaType = AreaType.Sphere;
            Sphere = new Sphere(center.X, center.Y, center.Z, CalculateDistanceFromCenterToCorner(box) + 1);
        }
    }

    private float CalculateDistanceFromCenterToCorner(BoundingBox box)
    {
        // all corners are the same distance from the center of the box
        float distanceFromCenterToEdgeSquared = box.GetXExtent() * box.GetXExtent() + box.GetYExtent() * box.GetYExtent();
        float distanceFromCenterToConerSquared = distanceFromCenterToEdgeSquared + box.GetZExtent() * box.GetZExtent();
        return (float)System.Math.Sqrt(distanceFromCenterToConerSquared);
    }
}
