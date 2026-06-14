using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/MovePlayerToPlayer (Tanelorn). Admin moveplayertoplayer command.</summary>
public class MovePlayerToPlayer : AdminCommand
{
    public MovePlayerToPlayer()
        : base("moveplayertoplayer")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length < 2)
        {
            PacketSendUtility.SendMessage(admin, "syntax //moveplayertoplayer <characterNameToMove> <characterNameDestination>");
            return;
        }

        Player playerToMove = Aion.GameServer.World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[0]));
        if (playerToMove == null)
        {
            PacketSendUtility.SendMessage(admin, "The specified player is not online.");
            return;
        }

        Player playerDestination = Aion.GameServer.World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[1]));
        if (playerDestination == null)
        {
            PacketSendUtility.SendMessage(admin, "The destination player is not online.");
            return;
        }

        if (playerToMove.Equals(playerDestination))
        {
            PacketSendUtility.SendMessage(admin, "Cannot move the specified player to their own position.");
            return;
        }

        TeleportService.TeleportTo(playerToMove, playerDestination.GetWorldId(), playerDestination.GetInstanceId(), playerDestination.GetX(),
            playerDestination.GetY(), playerDestination.GetZ(), playerDestination.GetHeading());

        PacketSendUtility.SendMessage(admin, "Teleported player " + playerToMove.GetName() + " to the location of player " + playerDestination.GetName()
            + ".");
        PacketSendUtility.SendMessage(playerToMove, "You have been teleported by an administrator.");
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "syntax //moveplayertoplayer <characterNameToMove> <characterNameDestination>");
    }
}
