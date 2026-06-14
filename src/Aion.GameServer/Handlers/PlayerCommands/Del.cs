using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Del (Source, Neon). Deletes items from the player's inventory.</summary>
public class Del : PlayerCommand
{
    public Del()
        : base("del", "Deletes items from your inventory.")
    {
        SetSyntaxInfo("<item link|ID> [count] - Removes item(s) with the specified name/ID (default: 1, optional: number of items to delete).");
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }

        int itemId = ChatUtil.GetItemId(paramsArr[0]);
        if (itemId == 0)
        {
            SendInfo(player, "Invalid item.");
            return;
        }

        // Java parity: NumberUtils.toInt(params[1]) returns 0 on non-numeric input.
        int itemCount = paramsArr.Length > 1 ? (int.TryParse(paramsArr[1], out int parsed) ? parsed : 0) : 1;
        if (itemCount == 0)
        {
            SendInfo(player, "Invalid item count.");
            return;
        }

        Storage inv = player.GetInventory();
        long invCount = inv.GetItemCountByItemId(itemId);
        if (invCount == 0)
        {
            SendInfo(player, "You don't have that item.");
            return;
        }
        if (itemCount > invCount)
        {
            SendInfo(player, "You only have " + invCount + ".");
            return;
        }

        inv.DecreaseByItemId(itemId, itemCount);
        SendInfo(player, "Deleted " + itemCount + "x " + ChatUtil.Item(itemId) + " from your inventory.");
    }
}
