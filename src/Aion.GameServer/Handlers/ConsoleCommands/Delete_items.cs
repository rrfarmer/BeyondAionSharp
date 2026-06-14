using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Delete_items (ginho1). Deletes all of the target's items at or below a given quality.</summary>
public class Delete_items : ConsoleCommand
{
    public Delete_items()
        : base("delete_items")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
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

        // Java parity: Integer.parseInt(params[0]) throws NumberFormatException.
        if (!int.TryParse(paramsArr[0], out int quality))
        {
            PacketSendUtility.SendMessage(admin, "Parameters need to be an integer.");
            return;
        }

        if (quality < 0 || quality >= Enum.GetValues(typeof(ItemQuality)).Length)
        {
            PacketSendUtility.SendMessage(admin, "Invalid QualityId.");
            return;
        }

        // Snapshot the item list before deleting (C# foreach forbids mutating the source during enumeration).
        foreach (Item item in new List<Item>(player.GetInventory().GetItems()))
        {
            if (item.GetItemTemplate().GetItemQuality().GetQualityId() <= quality)
            {
                player.GetInventory().Delete(item, ItemPacketService.ItemDeleteType.DISCARD);
            }
        }
    }

    // Java parity: Delete_items.info(Player, String) — ChatCommand has no info() in the C# port, so this is a private helper.
    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "syntax ///delete_items <item quality>");
    }
}
