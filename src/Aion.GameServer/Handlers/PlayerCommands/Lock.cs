using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.LoginServer.ServerPackets;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Lock (ViAl, Neon). Enables/disables blocking logins from other computers.</summary>
public class Lock : PlayerCommand
{
    public Lock()
        : base("lock", "Enables/disables blocking logins from other computers.")
    {
        SetSyntaxInfo(
            "<enable> - Allows login from only this computer.",
            "<disable> - Allows login from any computer.");
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }

        if ("enable".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            string hddSerial = player.GetClientConnection().GetHddSerial();
            if (hddSerial == null || hddSerial.Length == 0)
            {
                SendInfo(player, "Couldn't lock your account. Please re-log and try again.");
                return;
            }
            player.GetAccount().SetAllowedHddSerial(hddSerial);
            Aion.GameServer.Network.LoginServer.LoginServer.GetInstance().SendPacket(new SM_CHANGE_ALLOWED_HDD_SERIAL(player.GetAccount()));
            SendInfo(player, "Your account is now locked. You will not be able to login from any other computer from now on.");
        }
        else if ("disable".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            player.GetAccount().SetAllowedHddSerial(null);
            Aion.GameServer.Network.LoginServer.LoginServer.GetInstance().SendPacket(new SM_CHANGE_ALLOWED_HDD_SERIAL(player.GetAccount()));
            SendInfo(player, "Your account is unlocked. You can login from any computer again.");
        }
        else
        {
            SendInfo(player, "Invalid parameter.");
        }
    }
}
