using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Assemblednpc;

/// <summary>Java parity: model/templates/assemblednpc/AssembledNpcTemplate (xTz).</summary>
[XmlType("AssembledNpcTemplate")]
public class AssembledNpcTemplate
{
    [XmlAttribute("nr")] private int nr;
    [XmlAttribute("routeId")] private int routeId;
    [XmlAttribute("mapId")] private int mapId;
    [XmlAttribute("liveTime")] private int liveTime;
    [XmlElement("assembled_part")] private List<AssembledNpcPartTemplate> parts;

    public int GetNr()
    {
        return nr;
    }

    public int GetRouteId()
    {
        return routeId;
    }

    public int GetMapId()
    {
        return mapId;
    }

    public int GetLiveTime()
    {
        return liveTime;
    }

    public List<AssembledNpcPartTemplate> GetAssembledNpcPartTemplates()
    {
        return parts;
    }

    [XmlType("AssembledNpcPart")]
    public class AssembledNpcPartTemplate
    {
        [XmlAttribute("npcId")] private int npcId;
        [XmlAttribute("staticId")] private int staticId;

        public int GetNpcId()
        {
            return npcId;
        }

        public int GetStaticId()
        {
            return staticId;
        }
    }
}
