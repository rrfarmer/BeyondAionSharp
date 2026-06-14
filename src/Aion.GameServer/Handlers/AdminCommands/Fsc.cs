using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>
/// Java parity: data/handlers/admincommands/Fsc (Luno). Creates and sends custom packets from server to client for development purposes.
/// </summary>
public class Fsc : AdminCommand
{
    public Fsc()
        : base("fsc")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length < 3)
        {
            PacketSendUtility.SendMessage(player, "Incorrent number of params in //fsc command");
            return;
        }

        int id = Decode(paramsArr[0]);
        string format = paramsArr[1];

        SM_CUSTOM_PACKET packet = new SM_CUSTOM_PACKET(id);

        int i = 0;
        foreach (char c in format.ToCharArray())
        {
            packet.AddElement(SM_CUSTOM_PACKET.PacketElementType.GetByCode(c), paramsArr[i + 2]);
            i++;
        }
        PacketSendUtility.SendPacket(player, packet);
    }

    /// <summary>Java parity: Integer.decode(String) - decodes a decimal, hexadecimal (0x/0X/#) or octal (leading 0) string into an int.</summary>
    private static int Decode(string nm)
    {
        int radix = 10;
        int index = 0;
        bool negative = false;

        if (nm.Length == 0)
            throw new FormatException("Zero length string");
        char firstChar = nm[0];
        if (firstChar == '-')
        {
            negative = true;
            index++;
        }
        else if (firstChar == '+')
        {
            index++;
        }

        if (nm.Substring(index).StartsWith("0x") || nm.Substring(index).StartsWith("0X"))
        {
            index += 2;
            radix = 16;
        }
        else if (nm.Substring(index).StartsWith("#"))
        {
            index++;
            radix = 16;
        }
        else if (nm.Substring(index).StartsWith("0") && nm.Length > 1 + index)
        {
            index++;
            radix = 8;
        }

        if (nm.Substring(index).StartsWith("-") || nm.Substring(index).StartsWith("+"))
            throw new FormatException("Sign character in wrong position");

        int result = Convert.ToInt32(nm.Substring(index), radix);
        return negative ? -result : result;
    }
}
