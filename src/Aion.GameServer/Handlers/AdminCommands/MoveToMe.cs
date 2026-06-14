using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/MoveToMe (Cyrakuse, Estrayl). Teleports a player (optional his team) to the user.</summary>
public class MoveToMe : AdminCommand
{
    public MoveToMe()
        : base("movetome", "Teleports a player (optional his team) to the user.")
    {
        SetSyntaxInfo(
            "<name> - Teleports only the player.",
            "<name> <(g)rp|(a)lli> - Teleports either the players group or his alliance including him.");
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            SendInfo(admin);
            return;
        }
        string playerName = Util.ConvertName(paramsArr[0]);
        Player playerToMove = Aion.GameServer.World.World.GetInstance().GetPlayer(playerName);
        if (playerToMove == null)
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(playerName));
            return;
        }
        if (paramsArr.Length >= 2)
        {
            if (!playerToMove.IsInTeam())
            {
                SendInfo(admin, "The player does not belong to a team.");
                return;
            }
            TemporaryPlayerTeam teamToMove;
            switch (paramsArr[1].ToLower())
            {
                case "g":
                case "grp":
                case "group":
                    teamToMove = playerToMove.GetPlayerGroup();
                    break;
                case "a":
                case "alli":
                case "alliance":
                    teamToMove = playerToMove.GetCurrentTeam();
                    break;
                default:
                    SendInfo(admin);
                    return;
            }
            if (teamToMove == null)
            {
                SendInfo(admin, playerToMove.GetName() + " currently has no team.");
                return;
            }
            foreach (Player p in teamToMove.GetOnlineMembers())
                TeleportPlayer(p, admin);
        }
        else
        {
            TeleportPlayer(playerToMove, admin);
        }
    }

    private void TeleportPlayer(Player playerToMove, Player admin)
    {
        TeleportService.TeleportTo(playerToMove, admin.GetPosition());
        SendInfo(admin, "Teleported " + playerToMove.GetName() + " to your location.");
        SendInfo(playerToMove, "You have been teleported by " + admin.GetName() + ".");
    }
}
