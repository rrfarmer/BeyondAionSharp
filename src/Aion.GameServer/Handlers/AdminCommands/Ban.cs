using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Ban (Watson).</summary>
public class Ban : AdminCommand
{
    public Ban()
        : base("ban")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length < 1)
        {
            PacketSendUtility.SendMessage(admin, "Syntax: //ban <player> [account|ip|full] [time in minutes]");
            return;
        }

        // We need to get player's account ID
        string name = Util.ConvertName(paramsArr[0]);
        int accountId = 0;
        string accountIp = "";

        // First, try to find player in the World
        Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(name);
        if (player != null)
        {
            accountId = player.GetClientConnection().GetAccount().GetId();
            accountIp = player.GetClientConnection().GetIP();
        }

        // Second, try to get account ID of offline player from database
        if (accountId == 0)
            accountId = PlayerDAO.GetAccountIdByName(name);

        // Third, fail
        if (accountId == 0)
        {
            PacketSendUtility.SendMessage(admin, "Player " + name + " was not found!");
            PacketSendUtility.SendMessage(admin, "Syntax: //ban <player> [account|ip|full] [time in minutes]");
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
                PacketSendUtility.SendMessage(admin, "Syntax: //ban <player> [account|ip|full] [time in minutes]");
                return;
            }
        }

        int time = 0; // Default: infinity
        if (paramsArr.Length > 2)
        {
            if (!int.TryParse(paramsArr[2], out time))
            {
                PacketSendUtility.SendMessage(admin, "Syntax: //ban <player> [account|ip|full] [time in minutes]");
                return;
            }
        }
        if (time == 0)
        {
            time = 60 * 24 * 365 * 10; // pseudo infinity. TODO: rework
        }

        LoginServer.GetInstance().SendBanPacket(type, accountId, accountIp, time, admin.GetObjectId());
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "Syntax: //ban <player> [account|ip|full] [time in minutes]");
    }
}
