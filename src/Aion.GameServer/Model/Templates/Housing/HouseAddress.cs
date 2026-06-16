using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HouseAddress (Rolandas).</summary>
[XmlRoot("address")]
public class HouseAddress
{
    [XmlIgnore] private HousingLand land;

    // Java parity: nullable Float/Integer attributes. XmlSerializer cannot bind a Nullable<float>/Nullable<int>
    // attribute (throws at serializer construction -> whole load aborts -> hollow fallback), so public string proxies
    // carry the wire value and the backing fields stay the faithful nullable types (null when the attribute is absent).
    [XmlIgnore] public float? exitZ;

    [XmlIgnore] public float? exitY;

    [XmlIgnore] public float? exitX;

    [XmlIgnore] public int? exitMap;

    [XmlAttribute("exit_z")]
    public string ExitZRaw
    {
        get => exitZ?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        set => exitZ = string.IsNullOrEmpty(value) ? null : float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [XmlAttribute("exit_y")]
    public string ExitYRaw
    {
        get => exitY?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        set => exitY = string.IsNullOrEmpty(value) ? null : float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [XmlAttribute("exit_x")]
    public string ExitXRaw
    {
        get => exitX?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        set => exitX = string.IsNullOrEmpty(value) ? null : float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [XmlAttribute("exit_map")]
    public string ExitMapRaw
    {
        get => exitMap?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        set => exitMap = string.IsNullOrEmpty(value) ? null : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [XmlAttribute("z")] public float z;

    [XmlAttribute("y")] public float y;

    [XmlAttribute("x")] public float x;

    [XmlAttribute("town")] public int townId;

    [XmlAttribute("map")] public int map;

    [XmlAttribute("id")] public int id;

    // Java parity: afterUnmarshal(Unmarshaller, Object parent) — XmlSerializer has no parent callback; invoked by HousingLand's loader.
    public void AfterUnmarshal(object parent)
    {
        this.land = (HousingLand) parent;
        if (this.land == null)
            throw new System.NullReferenceException();
    }

    public HousingLand GetLand()
    {
        return land;
    }

    public float? GetExitZ()
    {
        return exitZ;
    }

    public float? GetExitY()
    {
        return exitY;
    }

    public float? GetExitX()
    {
        return exitX;
    }

    public int? GetExitMapId()
    {
        return exitMap;
    }

    public float GetZ()
    {
        return z;
    }

    public float GetY()
    {
        return y;
    }

    public float GetX()
    {
        return x;
    }

    public int GetMapId()
    {
        return map;
    }

    public int GetId()
    {
        return id;
    }

    public int GetTownId()
    {
        return townId;
    }
}
