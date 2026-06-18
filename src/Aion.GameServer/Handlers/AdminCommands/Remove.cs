using System.Text.RegularExpressions;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Remove (Phantom, ATracer). Removes items from a player's inventory.</summary>
public class Remove : AdminCommand
{
    public Remove()
        : base("remove")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 2)
        {
            Info(admin, null);
            return;
        }

        int itemId = 0;
        long itemCount = 1;
        byte itemCountIndex = 2;
        Player target = Aion.GameServer.World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[0]));
        if (target == null)
        {
            Info(admin, "Player isn't online.");
            return;
        }

        string itemString = paramsArr[1];
        if (itemString.Equals("[item:") && paramsArr.Length >= 2)
        {
            // some item links have space before their ID
            itemString += paramsArr[2];
            if (paramsArr.Length > 3)
            {
                itemCountIndex = 3;
            }
        }

        if (paramsArr.Length > 2 && (itemCountIndex < paramsArr.Length))
        {
            // count parameter was passed
            if (!long.TryParse(paramsArr[itemCountIndex], out itemCount))
            {
                Info(admin, "Invalid number parameter passed.");
                return;
            }
        }

        Match result = Regex.Match(itemString, "(?:\\[item:)??(\\d{9})");
        if (result.Success)
        {
            itemId = int.Parse(result.Groups[1].Value);
        }

        if (itemId > 0)
        {
            if (itemCount > 0)
            {
                Storage bag = target.GetInventory();
                long bagItemCount = bag.GetItemCountByItemId(itemId);
                if (bagItemCount >= 1)
                {
                    if (itemCount <= bagItemCount)
                    {
                        bag.DecreaseByItemId(itemId, itemCount);
                        PacketSendUtility.SendMessage(admin, "Successfully removed " + itemCount + "x [item:" + itemId + "] from " + target.GetName()
                            + "'s inventory.");
                        PacketSendUtility.SendMessage(target, "Admin removed " + itemCount + "x [item:" + itemId + "] from your inventory.");
                    }
                    else
                    {
                        Info(admin, "Player only has " + bagItemCount + " of this item.");
                    }
                }
                else
                {
                    Info(admin, "Player doesn't have that item.");
                }
            }
            else
            {
                Info(admin, "Invalid item count.");
            }
        }
        else
        {
            Info(admin, "Invalid item ID.");
        }
    }

    private void Info(Player player, string message)
    {
        if (message != null && message.Length != 0)
        {
            PacketSendUtility.SendMessage(player, message);
        }
        PacketSendUtility.SendMessage(player, "Syntax: //remove <player> <item ID|item @link> [quantity]");
    }
}
