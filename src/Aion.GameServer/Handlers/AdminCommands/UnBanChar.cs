using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/UnBanChar (nrg).</summary>
public class UnBanChar : AdminCommand
{
    public UnBanChar()
        : base("unbanchar")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length < 1)
        {
            PacketSendUtility.SendMessage(admin, "Syntax: //unbanchar <player>");
            return;
        }

        // Banned player must be offline
        string name = Util.ConvertName(paramsArr[0]);
        int playerId = PlayerDAO.GetPlayerIdByName(name);
        if (playerId == 0)
        {
            PacketSendUtility.SendMessage(admin, "Player " + name + " was not found!");
            PacketSendUtility.SendMessage(admin, "Syntax: //unbanchar <player>");
            return;
        }

        PacketSendUtility.SendMessage(admin, "Character " + name + " is not longer banned!");

        PunishmentService.UnbanChar(playerId);
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "Syntax: //unban <player> [account|ip|full]");
    }
}
