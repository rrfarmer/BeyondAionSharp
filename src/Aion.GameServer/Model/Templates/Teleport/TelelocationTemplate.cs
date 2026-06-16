using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Teleport;

/// <summary>Java parity: model/templates/teleport/TelelocationTemplate (orz).</summary>
[XmlRoot("teleloc_template")]
public class TelelocationTemplate : IL10n
{
    /// <summary>Location Id.</summary>
    [XmlAttribute("loc_id")] public int locId;

    [XmlAttribute("mapid")] public int mapid = 0;
    [XmlAttribute("name_id")] public int nameId;
    [XmlAttribute("posX")] public float x = 0;
    [XmlAttribute("posY")] public float y = 0;
    [XmlAttribute("posZ")] public float z = 0;
    [XmlAttribute("heading")] public int heading = 0;

    public int GetLocId()
    {
        return locId;
    }

    public int GetMapId()
    {
        return mapid;
    }

    public int GetL10nId()
    {
        return nameId;
    }

    public float GetX()
    {
        return x;
    }

    public float GetY()
    {
        return y;
    }

    public float GetZ()
    {
        return z;
    }

    public int GetHeading()
    {
        return heading;
    }
}
