using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Add (Phantom, ATracer, Source). Adds Kinah or items to a player's inventory.</summary>
public class Add : AdminCommand
{
    public Add()
        : base("add", "Adds Kinah or items to a player's inventory.")
    {
        SetSyntaxInfo(
            "kinah <amount> - Adds the specified amount of Kinah to your inventory.",
            "<item link|ID> [count] - Adds the specified item(s) to your inventory.",
            "<player> kinah <amount> - Adds the specified amount of Kinah to the player's inventory.",
            "<player> <item link|ID> [count] - Adds the specified item(s) to the player's inventory.");
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            SendInfo(player);
            return;
        }

        int index = 0;
        Player receiver = player;
        int itemId = paramsArr.Length == 2 && "Kinah".Equals(paramsArr[index], System.StringComparison.OrdinalIgnoreCase) ? ItemId.KINAH : ChatUtil.GetItemId(paramsArr[index]);
        if (itemId == 0)
        {
            string playerName = Util.ConvertName(paramsArr[index]);
            receiver = Aion.GameServer.World.World.GetInstance().GetPlayer(playerName);
            if (receiver == null)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(playerName));
                return;
            }
            if (++index < paramsArr.Length)
                itemId = "Kinah".Equals(paramsArr[index], System.StringComparison.OrdinalIgnoreCase) ? ItemId.KINAH : ChatUtil.GetItemId(paramsArr[index]);
        }

        ItemTemplate itemTemplate;
        if (itemId == 0 || (itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(itemId)) == null)
        {
            SendInfo(player, "Invalid item.");
            return;
        }

        long itemCount = paramsArr.Length > ++index ? long.Parse(paramsArr[index]) : 1;
        if (itemCount <= 0
            || (itemId == ItemId.KINAH ? receiver.GetInventory().GetKinah() + itemCount < 0 : itemCount / itemTemplate.GetMaxStackCount() > 126))
        {
            SendInfo(player, "Invalid item count.");
            return;
        }

        if (!AdminService.GetInstance().CanOperate(player, receiver, itemId, "command //add"))
            return;

        long notAddedCount = ItemService.AddItem(receiver, itemId, itemCount, true);
        if (notAddedCount == 0)
        {
            if (player != receiver)
            {
                SendInfo(player, "You gave " + itemCount + " x [item:" + itemId + "] to " + receiver.GetName() + ".");
                SendInfo(receiver, "You received " + itemCount + " x [item:" + itemId + "] from " + player.GetName() + ".");
            }
        }
        else
        {
            SendInfo(player, "Item couldn't be added");
        }
    }
}
