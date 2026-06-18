using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Setinventorygrowth (ginho1). Expands the target player's cube.</summary>
public class Setinventorygrowth : ConsoleCommand
{
    public Setinventorygrowth()
        : base("setinventorygrowth")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            Info(admin, null);
            return;
        }

        VisibleObject target = admin.GetTarget();
        if (target == null)
        {
            PacketSendUtility.SendMessage(admin, "No target selected.");
            return;
        }

        if (target is not Player player)
        {
            PacketSendUtility.SendMessage(admin, "This command can only be used on a player!");
            return;
        }

        if (CubeExpandService.CanExpand(player))
        {
            CubeExpandService.NpcExpand(player);
            PacketSendUtility.SendMessage(admin, "9 cube slots successfully added to player " + player.GetName() + "!");
            PacketSendUtility.SendMessage(player, "Admin " + admin.GetName() + " gave you a cube expansion!");
        }
        else
        {
            PacketSendUtility.SendMessage(admin, "Cube expansion cannot be added to " + player.GetName() + "!\nReason: player cube already fully expanded.");
            return;
        }
    }

    // Java parity: Setinventorygrowth.info(Player, String) — ChatCommand has no info() in the C# port, so this is a private helper.
    private void Info(Player admin, string message)
    {
        PacketSendUtility.SendMessage(admin, "syntax ///setinventorygrowth <?>");
    }
}
