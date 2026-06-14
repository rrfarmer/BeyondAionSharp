using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/SPrison (lord_rex). Command: //sprison &lt;player&gt; &lt;delay&gt;(minutes) - sends player to prison.</summary>
public class SPrison : AdminCommand
{
    public SPrison()
        : base("sprison")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 2)
        {
            SendInfo(admin);
            return;
        }

        try
        {
            Player playerToPrison = Aion.GameServer.World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[0]));
            int delay = int.Parse(paramsArr[1]);

            string reason = Util.ConvertName(paramsArr[2]);
            for (int itr = 3; itr < paramsArr.Length; itr++)
                reason += " " + paramsArr[itr];

            if (playerToPrison != null)
            {
                PunishmentService.SetIsInPrison(playerToPrison, true, delay, reason);
                PacketSendUtility.SendMessage(admin, "Player " + playerToPrison.GetName() + " sent to prison for " + delay + " because " + reason + ".");
            }
        }
        catch (Exception)
        {
            SendInfo(admin);
        }
    }

    private void Info(Player player, string message)
    {
        SendInfo(player);
    }

    private void SendInfo(Player player)
    {
        PacketSendUtility.SendMessage(player, "syntax //sprison <player> <delay> <reason>");
    }
}
