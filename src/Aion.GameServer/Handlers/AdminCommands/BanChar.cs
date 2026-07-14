using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/BanChar (nrg).</summary>
public class BanChar : AdminCommand
{
    public BanChar()
        : base("banchar")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length < 3)
        {
            SendInfo(admin, true);
            return;
        }

        int playerId = 0;
        string playerName = Util.ConvertName(paramsArr[0]);

        // First, try to find player in the World
        Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(playerName);
        if (player != null)
            playerId = player.GetObjectId();

        // Second, try to get player Id from offline player from database
        if (playerId == 0)
            playerId = PlayerDAO.GetPlayerIdByName(playerName);

        // Third, fail
        if (playerId == 0)
        {
            PacketSendUtility.SendMessage(admin, "Player " + playerName + " was not found!");
            SendInfo(admin, true);
            return;
        }

        int dayCount = -1;
        if (!TryParseInt(paramsArr[1], out dayCount))
        {
            PacketSendUtility.SendMessage(admin, "Second parameter is not an int");
            SendInfo(admin, true);
            return;
        }

        if (dayCount < 0)
        {
            PacketSendUtility.SendMessage(admin, "Second parameter has to be a positive daycount or 0 for infinity");
            SendInfo(admin, true);
            return;
        }

        string reason = Util.ConvertName(paramsArr[2]);
        for (int itr = 3; itr < paramsArr.Length; itr++)
            reason += " " + paramsArr[itr];

        PacketSendUtility.SendMessage(admin, "Char " + playerName + " is now banned for the next " + dayCount + " days!");

        PunishmentService.BanChar(playerId, dayCount, reason);
    }

    private void Info(Player player, string message)
    {
        SendInfo(player, false);
    }

    private void SendInfo(Player player, bool withNote)
    {
        PacketSendUtility.SendMessage(player, "Syntax: //banChar <playername> <days>/0 (for permanent) <reason>");
        if (withNote)
            PacketSendUtility.SendMessage(player, "Note: the current day is defined as a whole day even if it has just a few hours left!");
    }
}
