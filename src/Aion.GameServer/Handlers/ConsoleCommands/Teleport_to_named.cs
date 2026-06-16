using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Teleport_to_named (ginho1).</summary>
public class Teleport_to_named : ConsoleCommand
{
    public Teleport_to_named()
        : base("teleport_to_named")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            Info(admin, null);
            return;
        }

        string npcName = paramsArr[0];

        FileInfo xml = new FileInfo("./data/handlers/consolecommands/data/npcs.xml");
        NpcData data = JAXBUtil.Deserialize<NpcData>(xml);
        NpcTemplate npcTemplate = data.GetNpcTemplate(npcName);

        if (npcTemplate != null)
        {
            PacketSendUtility.SendMessage(admin, "Teleporting to Npc: " + npcTemplate.GetTemplateId());
            TeleportService.TeleportToNpc(admin, npcTemplate.GetTemplateId());
        }
    }

    private void Info(Player admin, string message)
    {
        PacketSendUtility.SendMessage(admin, "syntax ///teleport_to_named <named name>");
    }

    [XmlRoot("npc")]
    public class NpcTemplate
    {
        [XmlAttribute("id")]
        public string id;

        [XmlAttribute("name")]
        public string name;

        public string GetName() => name;

        // Java parity: afterUnmarshal parsed the @XmlID String id into an int.
        public int GetTemplateId() => int.Parse(id);
    }

    [XmlRoot("npcs")]
    public class NpcData
    {
        [XmlElement("npc")]
        public List<NpcTemplate> its;

        public NpcTemplate GetNpcTemplate(string npc)
        {
            foreach (NpcTemplate it in GetData())
            {
                if (it.GetName().ToLower().Equals(npc.ToLower()))
                    return it;
            }
            return null;
        }

        protected List<NpcTemplate> GetData() => its;
    }
}
