using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Flypath;

/// <summary>Java parity: model/templates/flypath/FlyPathEntry.</summary>
[XmlRoot("flypath_location")]
public class FlyPathEntry
{
    [XmlAttribute("id")] private int id;

    [XmlAttribute("sx")] private float startX;
    [XmlAttribute("sy")] private float startY;
    [XmlAttribute("sz")] private float startZ;
    [XmlAttribute("sworld")] private int sworld;

    [XmlAttribute("ex")] private float endX;
    [XmlAttribute("ey")] private float endY;
    [XmlAttribute("ez")] private float endZ;
    [XmlAttribute("eworld")] private int eworld;

    [XmlAttribute("time")] private float time;

    public int GetId()
    {
        return id;
    }

    public float GetStartX()
    {
        return startX;
    }

    public float GetStartY()
    {
        return startY;
    }

    public float GetStartZ()
    {
        return startZ;
    }

    public float GetEndX()
    {
        return endX;
    }

    public float GetEndY()
    {
        return endY;
    }

    public float GetEndZ()
    {
        return endZ;
    }

    public int GetStartWorldId()
    {
        return sworld;
    }

    public int GetEndWorldId()
    {
        return eworld;
    }

    public int GetTimeInMs()
    {
        return (int)(time * 1000);
    }
}
