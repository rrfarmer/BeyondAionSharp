using System;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/BanMac (KID, nrg).</summary>
public class BanMac : AdminCommand
{
    public BanMac()
        : base("banmac")
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length < 1)
        {
            Info(player, "Please add one or more parameters");
            return;
        }

        int time;
        string address;
        string targetName = "direct_type";

        // try parsing
        if (!int.TryParse(paramsArr[0], out time))
        {
            Info(player, "Please enter a valid integer amount of minutes");
            return;
        }

        if (time == 0) // 0 is 10 years since system don't allow infinte banns without rework - it's pseudo infinity
            time = 60 * 24 * 365 * 10;

        // is mac defined?
        if (paramsArr.Length > 1)
        {
            address = paramsArr[1];
        }
        else
        { // no address defined
            VisibleObject target = player.GetTarget();
            if (target is Player)
            {
                if (target.Equals(player))
                {
                    Info(player, "Omg, disselect yourself please.");
                    return;
                }

                Player targetpl = (Player)target;
                address = targetpl.GetClientConnection().GetMacAddress();
                targetName = targetpl.GetName();
                targetpl.GetClientConnection().Close();
            }
            else
            {
                Info(player, "You should select a player or give me any mac address");
                return;
            }
        }

        BannedMacManager.GetInstance().BanAddress(address, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + time * 60 * 1000,
            "author=" + player.GetName() + ", " + player.GetObjectId() + "; target=" + targetName);
    }

    private void Info(Player player, string message)
    {
        if (!message.Equals(""))
            PacketSendUtility.SendMessage(player, message);
        PacketSendUtility.SendMessage(player, "Syntax: //banmac [time in minutes] <mac>");
        PacketSendUtility.SendMessage(player, "Note: 0 minutes will cause permanent ban");
    }
}
