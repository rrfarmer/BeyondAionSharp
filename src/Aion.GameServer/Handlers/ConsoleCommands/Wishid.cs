using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Wishid (ginho1). Gives an item to the target player.</summary>
public class Wishid : ConsoleCommand
{
    public Wishid()
        : base("wishid")
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

        // Java parity: Long.parseLong(params[0]) / Integer.parseInt(params[1]) throw NumberFormatException -> info(admin, null).
        if (!long.TryParse(paramsArr[0], out long itemCount) || !int.TryParse(paramsArr[1], out int itemId))
        {
            Info(admin, null);
            return;
        }

        if (DataManager.ITEM_DATA.GetItemTemplate(itemId) == null)
        {
            PacketSendUtility.SendMessage(admin, "Item id is incorrect: " + itemId);
            return;
        }

        if (!AdminService.GetInstance().CanOperate(admin, player, itemId, "command ///wishid"))
            return;

        long count = ItemService.AddItem(player, itemId, itemCount, true);

        if (count == 0)
        {
            if (admin != player)
            {
                PacketSendUtility.SendMessage(admin, "You successfully gave " + itemCount + " x [item:" + itemId + "] to " + player.GetName() + ".");
                PacketSendUtility.SendMessage(player, "You successfully received " + itemCount + " x [item:" + itemId + "] from " + admin.GetName() + ".");
            }
            else
            {
                PacketSendUtility.SendMessage(admin, "You successfully received " + itemCount + " x [item:" + itemId + "]");
            }
        }
        else
        {
            PacketSendUtility.SendMessage(admin, "Item couldn't be added");
        }
    }

    // Java parity: Wishid.info(Player, String) — ChatCommand has no info() in the C# port, so this is a private helper.
    private void Info(Player admin, string message)
    {
        PacketSendUtility.SendMessage(admin, "syntax ///wishid <Quantity> <item Id>");
    }
}
