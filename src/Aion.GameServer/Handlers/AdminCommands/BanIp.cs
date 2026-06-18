using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/BanIp (Watson).</summary>
public class BanIp : AdminCommand
{
    public BanIp()
        : base("banip")
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length < 1)
        {
            PacketSendUtility.SendMessage(player, "Syntax: //banip <mask> [time in minutes]");
            return;
        }

        string mask = paramsArr[0];

        int time = 0; // Default: infinity
        if (paramsArr.Length > 1)
        {
            if (!int.TryParse(paramsArr[1], out time))
            {
                Info(player, "For input string: \"" + paramsArr[1] + "\"");
                return;
            }
        }
        if (time == 0)
        {
            time = 60 * 24 * 365 * 10; // pseudo infinity
        }

        LoginServer.GetInstance().SendBanPacket((byte)2, 0, mask, time, player.GetObjectId());
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "Syntax: //banip <mask> [time in minutes]");
    }
}
