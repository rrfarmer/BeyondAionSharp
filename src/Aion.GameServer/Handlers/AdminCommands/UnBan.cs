using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/UnBan (Watson).</summary>
public class UnBan : AdminCommand
{
    public UnBan()
        : base("unban")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length < 1)
        {
            PacketSendUtility.SendMessage(admin, "Syntax: //unban <player> [account|ip|full]");
            return;
        }

        // Banned player must be offline, so get his account ID from database
        string name = Util.ConvertName(paramsArr[0]);
        int accountId = PlayerDAO.GetAccountIdByName(name);
        if (accountId == 0)
        {
            PacketSendUtility.SendMessage(admin, "Player " + name + " was not found!");
            PacketSendUtility.SendMessage(admin, "Syntax: //unban <player> [account|ip|full]");
            return;
        }

        byte type = 3; // Default: full
        if (paramsArr.Length > 1)
        {
            // Smart Matching
            string stype = paramsArr[1].ToLower();
            if ("account".StartsWith(stype))
                type = 1;
            else if ("ip".StartsWith(stype))
                type = 2;
            else if ("full".StartsWith(stype))
                type = 3;
            else
            {
                PacketSendUtility.SendMessage(admin, "Syntax: //unban <player> [account|ip|full]");
                return;
            }
        }

        // Sends time -1 to unban
        LoginServer.GetInstance().SendBanPacket(type, accountId, "", -1, admin.GetObjectId());
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "Syntax: //unban <player> [account|ip|full]");
    }
}
