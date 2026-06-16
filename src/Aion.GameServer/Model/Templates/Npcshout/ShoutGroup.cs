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
        if (shoutNpcs == null)
        {
            shoutNpcs = new List<ShoutList>();
        }
        return this.shoutNpcs;
    }

    public string GetClientAi()
    {
        return clientAi;
    }

    public void MakeNull()
    {
        this.shoutNpcs = null;
        this.clientAi = null;
    }
}
