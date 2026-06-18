using System;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Say (Divinity, Neon).</summary>
public class Say : AdminCommand
{
    public Say()
        : base("say", "Lets your target say a message.")
    {
        SetSyntaxInfo("<message> - Sends the message as your target (npc only).");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        if (admin.GetTarget() is not Npc npc)
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }

        PacketSendUtility.BroadcastPacket(admin, new SM_MESSAGE(npc, string.Join(" ", paramsArr), ChatType.NORMAL), true);
    }
}
