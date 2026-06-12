using System.Xml.Serialization;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Model.Templates.Zone;

/// <summary>Java parity: model/templates/zone/ZoneTemplate — XML descriptor for a named zone area.</summary>
[XmlType("Zone")]
public class ZoneTemplate
{
    [XmlElement("points")]    public Points?    Points    { get; set; }
    [XmlElement("cylinder")]  public Cylinder?  Cylinder  { get; set; }
    [XmlElement("sphere")]    public Sphere?    Sphere    { get; set; }
    [XmlElement("semisphere")] public Semisphere? Semisphere { get; set; }

    [XmlAttribute("flags")]    public int Flags    { get; set; } = -1;
    [XmlAttribute("priority")] public int Priority { get; set; }
    [XmlAttribute("mapid")]    public int Mapid    { get; set; }

    // Java: @XmlAttribute(name="siege_id") List<Integer> siegeId
    [XmlAttribute("siege_id")] public string? SiegeIdRaw { get; set; }
    private List<int>? _siegeId;
    [XmlIgnore]
    public List<int>? SiegeId
    {
        get
        {
            if (_siegeId == null && SiegeIdRaw != null)
                _siegeId = SiegeIdRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(int.Parse).ToList();
            return _siegeId;
        }
    }

    [XmlAttribute("town_id")] public int TownId { get; set; }

    [XmlAttribute("area_type")] public AreaType AreaType { get; set; } = AreaType.Polygon;
    [XmlAttribute("zone_type")] public ZoneClassName ZoneType { get; set; } = ZoneClassName.SUB;

    // Java: @XmlAttribute(name="name") + afterUnmarshal builds ZoneName
    private string? _name;
    private ZoneName? _zoneName;

    [XmlAttribute("name")]
    public string? XmlName
    {
        get => _name;
        set
        {
            if (value == null) return;
            _zoneName = ZoneName.CreateOrGet(value);
            _name = _zoneName.Name;
        }
    }

    // Java parity: getXmlName()
    public string? GetXmlName()    => _name;
    public ZoneName? GetName()     => _zoneName;
    public Points?   GetPoints()   => Points;
    public Cylinder? GetCylinder() => Cylinder;
    public Sphere?   GetSphere()   => Sphere;
    public Semisphere? GetSemisphere() => Semisphere;
    public int       GetPriority() => Priority;
    public int       GetMapid()    => Mapid;
    public AreaType  GetAreaType() => AreaType;
    public ZoneClassName GetZoneType() => ZoneType;
    public List<int>? GetSiegeId() => SiegeId;
    public int       GetFlags()    => Flags;
    public int       GetTownId()   => TownId;
}
