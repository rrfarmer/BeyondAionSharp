using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HouseAddress (Rolandas).</summary>
[XmlRoot("address")]
public class HouseAddress
{
    [XmlIgnore] private HousingLand land;

    [XmlAttribute("exit_z")] private float? exitZ;

    [XmlAttribute("exit_y")] private float? exitY;

    [XmlAttribute("exit_x")] private float? exitX;

    [XmlAttribute("exit_map")] private int? exitMap;

    [XmlAttribute("z")] private float z;

    [XmlAttribute("y")] private float y;

    [XmlAttribute("x")] private float x;

    [XmlAttribute("town")] private int townId;

    [XmlAttribute("map")] private int map;

    [XmlAttribute("id")] private int id;

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
