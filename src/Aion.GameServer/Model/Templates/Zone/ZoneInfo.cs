using Aion.GameServer.Model.Geometry;

namespace Aion.GameServer.Model.Templates.Zone;

/// <summary>Java parity: model/templates/zone/ZoneInfo — pairs an Area instance with its template.</summary>
public class ZoneInfo
{
    private readonly Area _area;
    private readonly ZoneTemplate _zoneTemplate;

    public ZoneInfo(Area area, ZoneTemplate zoneTemplate)
    {
        _area         = area;
        _zoneTemplate = zoneTemplate;
    }

    public Area         GetArea()         => _area;
    public ZoneTemplate GetZoneTemplate() => _zoneTemplate;
}
