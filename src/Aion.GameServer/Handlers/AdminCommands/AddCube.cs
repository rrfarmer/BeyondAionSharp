using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/AddCube (Kamui).</summary>
public class AddCube : AdminCommand
{
    public AddCube()
        : base("addcube")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length != 1)
        {
            PacketSendUtility.SendMessage(admin, "Syntax: //addcube <player name>");
            return;
        }

        Player receiver = null;

        receiver = Aion.GameServer.World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[0]));

        if (receiver == null)
        {
            PacketSendUtility.SendMessage(admin, "The player " + Util.ConvertName(paramsArr[0]) + " is not online.");
            return;
        }

        if (CubeExpandService.CanExpand(receiver))
        {
            CubeExpandService.NpcExpand(receiver);
            PacketSendUtility.SendMessage(admin, "9 cube slots successfully added to player " + receiver.GetName() + "!");
            PacketSendUtility.SendMessage(receiver, "Admin " + admin.GetName() + " gave you a cube expansion!");
        }
        else
        {
            PacketSendUtility.SendMessage(admin, "Cube expansion cannot be added to " + receiver.GetName()
                + "!\nReason: player cube already fully expanded.");
            return;
        }
    }
}
