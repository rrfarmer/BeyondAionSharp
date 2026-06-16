using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Assemblednpc;

/// <summary>Java parity: model/templates/assemblednpc/AssembledNpcTemplate (xTz).</summary>
[XmlType("AssembledNpcTemplate")]
public class AssembledNpcTemplate
{
    [XmlAttribute("nr")] public int nr;
    [XmlAttribute("routeId")] public int routeId;
    [XmlAttribute("mapId")] public int mapId;
    [XmlAttribute("liveTime")] public int liveTime;
    [XmlElement("assembled_part")] public List<AssembledNpcPartTemplate> parts;

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
        [XmlAttribute("npcId")] public int npcId;
        [XmlAttribute("staticId")] public int staticId;

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
