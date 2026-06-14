using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/UnBanMac (KID).</summary>
public class UnBanMac : AdminCommand
{
    public UnBanMac()
        : base("unbanmac")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length < 1)
        {
            Info(player, null);
            return;
        }

        string address = paramsArr[0];
        bool result = BannedMacManager.GetInstance().UnbanAddress(address,
            "uban;mac=" + address + ", " + player.GetObjectId() + "; admin=" + player.GetName());
        if (result)
            PacketSendUtility.SendMessage(player, "mac " + address + " has unbanned");
        else
            PacketSendUtility.SendMessage(player, "mac " + address + " is not banned");
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "Syntax: //unbanmac <mac>");
    }
}
