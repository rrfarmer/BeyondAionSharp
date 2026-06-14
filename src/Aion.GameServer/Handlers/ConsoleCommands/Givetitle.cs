using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Givetitle (ginho1). Adds a title to the target player.</summary>
public class Givetitle : ConsoleCommand
{
    public Givetitle()
        : base("givetitle")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            Info(admin, null);
            return;
        }

        if (!ChatProcessor.GetInstance().IsCommandAllowed(admin, "set"))
        {
            PacketSendUtility.SendMessage(admin, "You dont have enough rights to execute this command");
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
            PacketSendUtility.SendMessage(admin, "Select one player.");
            return;
        }

        // Java parity: Integer.parseInt(params[0]) throws NumberFormatException.
        if (!int.TryParse(paramsArr[0], out int titleId))
        {
            PacketSendUtility.SendMessage(admin, "You should enter valid second params!");
            return;
        }
        if (titleId <= 263)
            SetTitle(player, titleId);
        PacketSendUtility.SendMessage(admin, "Added " + titleId + " to" + player.GetCommonData().GetName());
    }

    // Java parity: Givetitle.info(Player, String) — ChatCommand has no info() in the C# port, so this is a private helper.
    private void Info(Player admin, string message)
    {
        PacketSendUtility.SendMessage(admin, "syntax ///addskill <named name>");
    }

    private void SetTitle(Player player, int value)
    {
        if (player.GetTitleList().Contains(value))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_TOOLTIP_LEARNED_TITLE());
            return;
        }
        player.GetTitleList().AddTitle(value, false, 0);
    }
}
