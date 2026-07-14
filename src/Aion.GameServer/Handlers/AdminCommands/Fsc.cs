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

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length < 3)
        {
            PacketSendUtility.SendMessage(player, "Incorrent number of params in //fsc command");
            return;
        }

        int id = DecodeInt(paramsArr[0]);
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

}
