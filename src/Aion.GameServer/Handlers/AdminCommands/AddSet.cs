using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Itemset;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/AddSet (Antivirus). Adds an item set to a player's inventory.</summary>
public class AddSet : AdminCommand
{
    public AddSet()
        : base("addset")
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0 || paramsArr.Length > 2)
        {
            Info(player, null);
            return;
        }

        int itemSetId = 0;
        Player receiver = null;

        if (int.TryParse(paramsArr[0], out int parsedId))
        {
            itemSetId = parsedId;
            receiver = player;
        }
        else
        {
            receiver = Aion.GameServer.World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[0]));

            if (receiver == null)
            {
                PacketSendUtility.SendMessage(player, "Could not find a player by that name.");
                return;
            }

            if (int.TryParse(paramsArr[1], out int parsedId2))
            {
                itemSetId = parsedId2;
            }
            else
            {
                PacketSendUtility.SendMessage(player, "You must give number to itemset ID.");
                return;
            }
        }

        ItemSetTemplate itemSet = DataManager.ITEM_SET_DATA.GetItemSetTemplate(itemSetId);
        if (itemSet == null)
        {
            PacketSendUtility.SendMessage(player, "ItemSet does not exist with id " + itemSetId);
            return;
        }

        if (receiver.GetInventory().GetFreeSlots() < itemSet.GetItempart().Count)
        {
            PacketSendUtility.SendMessage(player, "Inventory needs at least " + itemSet.GetItempart().Count + " free slots.");
            return;
        }

        foreach (ItemPart setPart in itemSet.GetItempart())
        {
            long count = ItemService.AddItem(receiver, setPart.GetItemId(), 1);

            if (count != 0)
            {
                PacketSendUtility.SendMessage(player, "Item " + setPart.GetItemId() + " couldn't be added");
                return;
            }
        }

        PacketSendUtility.SendMessage(player, "Item Set added successfully");
        PacketSendUtility.SendMessage(receiver, "admin gives you an item set");
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "syntax //addset <player> <itemset ID>");
        PacketSendUtility.SendMessage(player, "syntax //addset <itemset ID>");
    }
}
