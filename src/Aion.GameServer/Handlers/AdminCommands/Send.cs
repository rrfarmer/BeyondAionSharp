using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>
/// Java parity: data/handlers/admincommands/Send (Aquanox, Neon). Sends custom packets from server to client based on
/// xml mappings in folder "./data/packets". Command details: "//send [file]".
/// </summary>
public class Send : AdminCommand
{
    private const string FOLDER = "./data/packets/";
    private const string SCHEMAFILE = FOLDER + "packets.xsd";

    public Send()
        : base("send", "Sends custom packets.")
    {
        SetSyntaxInfo("<file> - Sends packets to your client, based on the ./data/packets/<file>.xml template.");
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        string fileName = paramsArr[0] + ".xml";
        FileInfo file = new FileInfo(FOLDER + fileName);

        if (!file.Exists)
        {
            SendInfo(admin, "File " + fileName + " not found");
            return;
        }

        Packets packetsTemplate = JAXBUtil.Deserialize<Packets>(file, SCHEMAFILE);
        SendPackets(admin, packetsTemplate);
    }

    private void SendPackets(Player player, Packets packets)
    {
        string senderObjectId = player.GetObjectId().ToString();
        string targetObjectId = player.GetTarget() != null ? player.GetTarget().GetObjectId().ToString() : "0";
        long delay = 0;
        foreach (Packet packetTemplate in packets)
        {
            SM_CUSTOM_PACKET packet = new SM_CUSTOM_PACKET(packetTemplate.GetOpcode());

            foreach (Part part in packetTemplate.GetParts())
            {
                SM_CUSTOM_PACKET.PacketElementType byCode = SM_CUSTOM_PACKET.PacketElementType.GetByCode(part.GetTypeChar());

                string value = part.GetValue();

                if (value.Contains("${objectId}"))
                    value = value.Replace("${objectId}", senderObjectId);
                if (value.Contains("${targetObjectId}"))
                    value = value.Replace("${targetObjectId}", targetObjectId);

                for (int i = 0; i < part.GetRepeatCount(); i++)
                    packet.AddElement(byCode, value);
            }

            delay += packetTemplate.GetDelay();

            ThreadPoolManager.GetInstance().Schedule(() => PacketSendUtility.SendPacket(player, packet), delay);

            delay += packets.GetDelay();
        }
    }

    // Java parity: Integer.decode (supports 0x/0X hex, leading-0 octal, decimal, optional leading sign).
    private static int Decode(string nm)
    {
        int index = 0;
        int sign = 1;
        if (nm.StartsWith("-"))
        {
            sign = -1;
            index++;
        }
        else if (nm.StartsWith("+"))
        {
            index++;
        }

        int radix;
        if (nm.Length > index + 1 && (nm[index] == '0') && (nm[index + 1] == 'x' || nm[index + 1] == 'X'))
        {
            index += 2;
            radix = 16;
        }
        else if (nm.Length > index && nm[index] == '#')
        {
            index++;
            radix = 16;
        }
        else if (nm.Length > index + 1 && nm[index] == '0')
        {
            index++;
            radix = 8;
        }
        else
        {
            radix = 10;
        }

        return sign * Convert.ToInt32(nm.Substring(index), radix);
    }

    [XmlRoot("packets")]
    public class Packets : System.Collections.Generic.IEnumerable<Packet>
    {
        [XmlElement("packet")]
        public List<Packet> packets;

        [XmlAttribute("delay")]
        public long delay = -1;

        public long GetDelay()
        {
            return delay;
        }

        public IEnumerator<Packet> GetEnumerator()
        {
            return packets.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return packets.GetEnumerator();
        }
    }

    [XmlRoot("packet")]
    public class Packet
    {
        [XmlElement("part")]
        public List<Part> parts = new List<Part>();

        [XmlAttribute("opcode")]
        public string opcode = "-1";

        [XmlAttribute("delay")]
        public long delay = 0;

        public int GetOpcode()
        {
            return Decode(opcode);
        }

        public ICollection<Part> GetParts()
        {
            return parts;
        }

        public long GetDelay()
        {
            return delay;
        }
    }

    [XmlRoot("part")]
    public class Part
    {
        [XmlAttribute("type")]
        public string type = null;

        [XmlAttribute("value")]
        public string value = null;

        [XmlAttribute("repeat")]
        public int repeatCount = 1;

        // Java parity: Part.getType() returns char; renamed GetTypeChar in C# because object.GetType() is reserved.
        public char GetTypeChar()
        {
            return type[0];
        }

        public string GetValue()
        {
            return value;
        }

        public int GetRepeatCount()
        {
            return repeatCount;
        }
    }
}
