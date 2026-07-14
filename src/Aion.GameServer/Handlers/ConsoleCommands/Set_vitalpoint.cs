using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Set_vitalpoint (ginho1). Sets the target player's current salvation points.</summary>
public class Set_vitalpoint : ConsoleCommand
{
    public Set_vitalpoint()
        : base("set_vitalpoint")
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

        // Java parity: Integer.parseInt(params[0]) throws NumberFormatException -> info(admin, null).
        if (!TryParseInt(paramsArr[0], out int value))
        {
            Info(admin, null);
            return;
        }

        player.GetCommonData().SetCurrentSalvationPoints(value);
        PacketSendUtility.SendPacket(player, new SM_STATS_INFO(player));
    }

    // Java parity: Set_vitalpoint.info(Player, String) — ChatCommand has no info() in the C# port, so this is a private helper.
    private void Info(Player admin, string message)
    {
        PacketSendUtility.SendMessage(admin, "syntax ///set_vitalpoint <value>");
    }
}
