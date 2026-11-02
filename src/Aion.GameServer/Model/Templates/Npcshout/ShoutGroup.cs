using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Npcshout;

/// <summary>Java parity: model/templates/npcshout/ShoutGroup (Rolandas).</summary>
[XmlType("ShoutGroup")]
public class ShoutGroup
{
    [XmlElement("shout_npcs")] public List<ShoutList> shoutNpcs;

    [XmlAttribute("client_ai")] public string clientAi;

    public List<ShoutList> GetShoutNpcs()
    {
        return shoutNpcs == null ? new List<ShoutList>() : shoutNpcs;
    }

    public string GetClientAi()
    {
        return clientAi;
    }
}
