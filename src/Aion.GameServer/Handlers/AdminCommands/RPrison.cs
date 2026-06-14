using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/RPrison (lord_rex). Command: //rprison &lt;player&gt; - removes player from prison.</summary>
public class RPrison : AdminCommand
{
    public RPrison()
        : base("rprison")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0 || paramsArr.Length > 2)
        {
            PacketSendUtility.SendMessage(admin, "syntax //rprison <player>");
            return;
        }

        try
        {
            Player playerFromPrison = Aion.GameServer.World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[0]));

            if (playerFromPrison != null)
            {
                PunishmentService.SetIsInPrison(playerFromPrison, false, 0, "");
                PacketSendUtility.SendMessage(admin, "Player " + playerFromPrison.GetName() + " removed from prison.");
            }
        }
        catch (Exception)
        {
            PacketSendUtility.SendMessage(admin, "Usage: //rprison <player>");
        }
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "syntax //rprison <player>");
    }
}
