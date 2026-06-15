using System;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Ingameshop;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Network.LoginServer.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Gameshop (xTz).</summary>
public class Gameshop : AdminCommand
{
    public Gameshop()
        : base("gameshop")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            Info(admin, null);
            return;
        }
        int itemId = 0;
        int list;
        sbyte category, subCategory, itemType, gift;
        long count, price, toll;
        Player player;
        string titleDescription;
        if ("delete".StartsWith(paramsArr[0]))
        {
            try
            {
                itemId = int.Parse(paramsArr[1]);
                category = sbyte.Parse(paramsArr[2]);
                subCategory = sbyte.Parse(paramsArr[3]);
                list = int.Parse(paramsArr[4]);
            }
            catch (FormatException)
            {
                PacketSendUtility.SendMessage(admin, "<itemId, category, subCategory, list> values must be int, byte, byte, int.");
                return;
            }
            InGameShopDAO.DeleteIngameShopItem(itemId, category, subCategory, list - 1);
            PacketSendUtility.SendMessage(admin, "You remove [item:" + itemId + "]");
        }
        else if ("add".StartsWith(paramsArr[0]))
        {
            try
            {
                itemId = int.Parse(paramsArr[1]);
                count = long.Parse(paramsArr[2]);
                price = long.Parse(paramsArr[3]);
                category = sbyte.Parse(paramsArr[4]);
                subCategory = sbyte.Parse(paramsArr[5]);
                itemType = sbyte.Parse(paramsArr[6]);
                gift = sbyte.Parse(paramsArr[7]);
                list = int.Parse(paramsArr[8]);
                titleDescription = Util.ConvertName(paramsArr[9]);
            }
            catch (FormatException)
            {
                PacketSendUtility.SendMessage(admin,
                    "<itemId, count, price, category, subCategory, itemType, gift, list, description> values must be int, long, long, byte, byte, byte, byte, int, string, Object... .");
                return;
            }

            ItemTemplate itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(itemId);
            if (itemTemplate == null)
            {
                PacketSendUtility.SendMessage(admin, "Item id is incorrect: " + itemId);
                return;
            }

            string description = "";

            for (int i = 10; i < paramsArr.Length; i++)
            {
                description += Util.ConvertName(paramsArr[i]) + " ";
            }
            description = description.Trim();

            if (list < 1)
            {
                PacketSendUtility.SendMessage(admin, "<list> : minium is 1.");
                return;
            }
            if (gift < 0 || gift > 1)
            {
                PacketSendUtility.SendMessage(admin, "<gift> : minimum is 0, maximum is 1.");
                return;
            }
            if (itemType < 0 || itemType > 2)
            {
                PacketSendUtility.SendMessage(admin, "<itemType> : minimum is 0, maximum is 2.");
                return;
            }
            if (subCategory < 3 || subCategory > 19)
            {
                PacketSendUtility.SendMessage(admin, "<category> : minimum is 3, maximum is 19.");
                return;
            }
            if (titleDescription.Length > 20)
            {
                PacketSendUtility.SendMessage(admin, "<title description> : maximum length is 20.");
                return;
            }
            if (titleDescription.Equals("empty"))
            {
                titleDescription = "";
            }
            InGameShopDAO.SaveIngameShopItem(IDFactory.GetInstance().NextId(), itemId, count, price, category, subCategory,
                list - 1, 1, itemType, gift, titleDescription, description);
            PacketSendUtility.SendMessage(admin, "You add [item:" + itemId + "]");
        }
        else if ("deleteranking".StartsWith(paramsArr[0]))
        {
            try
            {
                itemId = int.Parse(paramsArr[1]);
            }
            catch (FormatException)
            {
                PacketSendUtility.SendMessage(admin, "<itemId> value must be an integer.");
            }
            InGameShopDAO.DeleteIngameShopItem(itemId, (sbyte) -1, (sbyte) -1, -1);
            PacketSendUtility.SendMessage(admin, "You remove from Ranking Sales [item:" + itemId + "]");
        }
        else if ("addranking".StartsWith(paramsArr[0]))
        {
            try
            {
                itemId = int.Parse(paramsArr[1]);
                count = long.Parse(paramsArr[2]);
                price = long.Parse(paramsArr[3]);
                itemType = sbyte.Parse(paramsArr[4]);
                gift = sbyte.Parse(paramsArr[5]);
                titleDescription = Util.ConvertName(paramsArr[6]);
            }
            catch (FormatException)
            {
                PacketSendUtility.SendMessage(admin,
                    "<itemId, count, price, itemType, gift, description> value must be int, long, long, byte, byte, string, Object... .");
                return;
            }
            string description = "";
            for (int i = 7; i < paramsArr.Length; i++)
            {
                description += Util.ConvertName(paramsArr[i]) + " ";
            }
            description = description.Trim();

            ItemTemplate itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(itemId);

            if (itemTemplate == null)
            {
                PacketSendUtility.SendMessage(admin, "Item id is incorrect: " + itemId);
                return;
            }
            if (titleDescription.Equals("empty"))
            {
                titleDescription = "";
            }
            InGameShopDAO.SaveIngameShopItem(IDFactory.GetInstance().NextId(), itemId, count, price, (sbyte) -1, (sbyte) -1, -1, 0,
                itemType, gift, titleDescription, description);
            PacketSendUtility.SendMessage(admin, "You remove from Ranking Sales [item:" + itemId + "]");
        }
        else if ("settoll".StartsWith(paramsArr[0]))
        {
            if (paramsArr.Length == 3)
            {
                try
                {
                    toll = int.Parse(paramsArr[2]);
                }
                catch (FormatException)
                {
                    PacketSendUtility.SendMessage(admin, "<toll> value must be an integer.");
                    return;
                }

                string name = Util.ConvertName(paramsArr[1]);

                player = World.World.GetInstance().GetPlayer(name);
                if (player == null)
                {
                    PacketSendUtility.SendMessage(admin, "The specified player is not online.");
                    return;
                }

                if (LoginServer.GetInstance().SendPacket(new SM_ACCOUNT_TOLL_INFO(toll, player.GetAccount().GetId())))
                {
                    player.GetClientConnection().GetAccount().SetToll(toll);
                    PacketSendUtility.SendPacket(player, new SM_TOLL_INFO(toll));
                    PacketSendUtility.SendMessage(admin, "Tolls setted to " + toll + ".");
                }
                else
                    PacketSendUtility.SendMessage(admin, "ls communication error.");
            }
            if (paramsArr.Length == 2)
            {
                try
                {
                    toll = int.Parse(paramsArr[1]);
                }
                catch (FormatException)
                {
                    PacketSendUtility.SendMessage(admin, "<toll> value must be an integer.");
                    return;
                }

                if (toll < 0)
                {
                    PacketSendUtility.SendMessage(admin, "<toll> must > 0.");
                    return;
                }

                VisibleObject target = admin.GetTarget();
                if (!(target is Player))
                {
                    PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
                    return;
                }

                player = (Player) target;
                if (LoginServer.GetInstance().SendPacket(new SM_ACCOUNT_TOLL_INFO(toll, player.GetAccount().GetId())))
                {
                    player.GetClientConnection().GetAccount().SetToll(toll);
                    PacketSendUtility.SendPacket(player, new SM_TOLL_INFO(toll));
                    PacketSendUtility.SendMessage(admin, "Tolls setted to " + toll + ".");
                }
                else
                    PacketSendUtility.SendMessage(admin, "ls communication error.");
            }
        }
        else if ("addtoll".StartsWith(paramsArr[0]))
        {
            if (paramsArr.Length == 3)
            {
                try
                {
                    toll = int.Parse(paramsArr[2]);
                }
                catch (FormatException)
                {
                    PacketSendUtility.SendMessage(admin, "<toll> value must be an integer.");
                    return;
                }

                if (toll < 0)
                {
                    PacketSendUtility.SendMessage(admin, "<toll> must > 0.");
                    return;
                }

                string name = Util.ConvertName(paramsArr[1]);

                player = World.World.GetInstance().GetPlayer(name);
                if (player == null)
                {
                    PacketSendUtility.SendMessage(admin, "The specified player is not online.");
                    return;
                }

                PacketSendUtility.SendMessage(admin, "You added " + toll + " tolls to Player: " + name);
                InGameShopEn.GetInstance().AddToll(player, toll);
            }
            if (paramsArr.Length == 2)
            {
                try
                {
                    toll = int.Parse(paramsArr[1]);
                }
                catch (FormatException)
                {
                    PacketSendUtility.SendMessage(admin, "<toll> value must be an integer.");
                    return;
                }

                VisibleObject target = admin.GetTarget();
                if (!(target is Player))
                {
                    PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
                    return;
                }

                player = (Player) target;
                PacketSendUtility.SendMessage(admin, "You added " + toll + " tolls to Player: " + player.GetName());
                InGameShopEn.GetInstance().AddToll(player, toll);
            }
        }
        else
        {
            PacketSendUtility.SendMessage(admin, "You can use only, addtoll, settoll, deleteranking, addranking, delete or add.");
        }
    }

    // Java parity: overridden deprecated info(Player, String) — only invoked internally by this command.
    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player,
            "No parameters detected please use:\n"
                + "//gameshop add <itemId> <count> <price> <category> <subCategory> <itemType> <gift> <list> <title description|empty> <item description|null>\n"
                + "//gameshop delete <itemId> <category> <subCategory> <list>\n"
                + "//gameshop addranking <itemId> <count> <price> <itemType> <gift> <title description|empty> <item description|null>\n"
                + "//gameshop deleteranking <itemId>\n" + "//gameshop settoll <target|player> <toll>\n" + "//gameshop addtoll <target|player> <toll>");
    }
}
