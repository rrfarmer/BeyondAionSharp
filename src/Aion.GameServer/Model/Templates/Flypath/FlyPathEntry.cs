using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Flypath;

/// <summary>Java parity: model/templates/flypath/FlyPathEntry.</summary>
[XmlRoot("flypath_location")]
public class FlyPathEntry
{
    [XmlAttribute("id")] public int id;

    [XmlAttribute("sx")] public float startX;
    [XmlAttribute("sy")] public float startY;
    [XmlAttribute("sz")] public float startZ;
    [XmlAttribute("sworld")] public int sworld;

    [XmlAttribute("ex")] public float endX;
    [XmlAttribute("ey")] public float endY;
    [XmlAttribute("ez")] public float endZ;
    [XmlAttribute("eworld")] public int eworld;

    [XmlAttribute("time")] public float time;

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
