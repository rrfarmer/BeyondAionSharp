using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Iteminfo;
using Aion.GameServer.Services.Players;
using ItemBlobType = global::Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;
using SM_ATTACK_STATUS = global::Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_BROKER_SERVICE (IlBuono, kosyachok). Broker search/register/settled/sell packets. CRITICAL: BrokerPacketType has DUPLICATE ids (SHOW_SETTLED_ICON=5, SETTLED_ITEMS=5) and the switch distinguishes by identity -> sealed value-class + reference-equality if-chain (a plain enum gives duplicate case-label errors), like SM_ATTACK_STATUS. Function->Func; toArray->ToArray; TimeUnit.MILLISECONDS.toDays(ms)->ms/86400000; currentTimeMillis()->DateTimeOffset; getBuf()->GetBuf(). BrokerItem/Item/EnchantInfoBlobEntry/ItemInfoBlob red-tolerated.</summary>
public class SM_BROKER_SERVICE : AionServerPacket
{
    public const int SETTLED_ITEMS_STATIC_BODY_SIZE = 18;
    public static readonly Func<BrokerItem, int> SETTLED_ITEMS_DYNAMIC_BODY_PART_SIZE_CALCULATOR =
        item => 32 + EnchantInfoBlobEntry.SIZE + (item.GetItemCreator() == null ? 2 : item.GetItemCreator().Length * 2 + 2);

    private sealed class BrokerPacketType
    {
        public static readonly BrokerPacketType SEARCHED_ITEMS = new BrokerPacketType(0);
        public static readonly BrokerPacketType REGISTERED_ITEMS = new BrokerPacketType(1);
        public static readonly BrokerPacketType BUY_ITEMS = new BrokerPacketType(2);
        public static readonly BrokerPacketType REGISTER_ITEM = new BrokerPacketType(3);
        public static readonly BrokerPacketType CANCEL_REGISTERED_ITEM = new BrokerPacketType(4);
        public static readonly BrokerPacketType SHOW_SETTLED_ICON = new BrokerPacketType(5);
        public static readonly BrokerPacketType SETTLED_ITEMS = new BrokerPacketType(5);
        public static readonly BrokerPacketType REMOVE_SETTLED_ICON = new BrokerPacketType(6);
        public static readonly BrokerPacketType SHOW_SELL_WINDOW = new BrokerPacketType(7);

        private int id;

        private BrokerPacketType(int id)
        {
            this.id = id;
        }

        public int GetId()
        {
            return id;
        }
    }

    private BrokerPacketType type;
    private BrokerItem[] brokerItems;
    private int itemsCount;
    private int startPage;
    private int message;
    private int totalItemCount, pageIndex;
    private long settledKinah, currentLow, currentHigh;
    private int itemId;
    private byte unk;

    public SM_BROKER_SERVICE(BrokerItem brokerItem, int message, int itemsCount)
    {
        this.type = BrokerPacketType.REGISTER_ITEM;
        this.brokerItems = new BrokerItem[] { brokerItem };
        this.message = message;
        this.itemsCount = itemsCount;
    }

    public SM_BROKER_SERVICE(int message)
    {
        this.type = BrokerPacketType.REGISTER_ITEM;
        this.message = message;
    }

    public SM_BROKER_SERVICE(BrokerItem[] brokerItems)
    {
        this.type = BrokerPacketType.REGISTERED_ITEMS;
        this.brokerItems = brokerItems;
    }

    public SM_BROKER_SERVICE(List<BrokerItem> brokerItems, int totalItemCount, int pageIndex, long settledKinah)
    {
        this.type = BrokerPacketType.SETTLED_ITEMS;
        this.totalItemCount = totalItemCount;
        this.pageIndex = pageIndex;
        this.brokerItems = brokerItems.ToArray();
        this.settledKinah = settledKinah;
    }

    public SM_BROKER_SERVICE(BrokerItem[] brokerItems, int itemsCount, int startPage)
    {
        this.type = BrokerPacketType.SEARCHED_ITEMS;
        this.brokerItems = brokerItems;
        this.itemsCount = itemsCount;
        this.startPage = startPage;
    }

    public SM_BROKER_SERVICE(bool showSettledIcon, long settledKinah)
    {
        this.type = showSettledIcon ? BrokerPacketType.SHOW_SETTLED_ICON : BrokerPacketType.REMOVE_SETTLED_ICON;
        this.settledKinah = settledKinah;
    }

    public SM_BROKER_SERVICE(byte unk, int itemId)
    {
        this.type = BrokerPacketType.CANCEL_REGISTERED_ITEM;
        this.itemId = itemId;
        this.unk = unk;
    }

    public SM_BROKER_SERVICE(byte unk, int itemId, long currentLow, long currentHigh)
    {
        this.type = BrokerPacketType.SHOW_SELL_WINDOW;
        this.unk = unk;
        this.itemId = itemId;
        this.currentLow = currentLow;
        this.currentHigh = currentHigh;
    }

    protected override void WriteImpl(AionConnection con)
    {
        if (type == BrokerPacketType.SEARCHED_ITEMS)
            WriteSearchedItems();
        else if (type == BrokerPacketType.REGISTERED_ITEMS)
            WriteRegisteredItems();
        else if (type == BrokerPacketType.REGISTER_ITEM)
            WriteRegisterItem();
        else if (type == BrokerPacketType.CANCEL_REGISTERED_ITEM)
            WriteCancelRegisteredItem();
        else if (type == BrokerPacketType.SHOW_SETTLED_ICON)
            WriteShowSettledIcon();
        else if (type == BrokerPacketType.REMOVE_SETTLED_ICON)
            WriteRemoveSettledIcon();
        else if (type == BrokerPacketType.SETTLED_ITEMS)
            WriteShowSettledItems();
        else if (type == BrokerPacketType.SHOW_SELL_WINDOW)
            WriteShowSellWindow();
    }

    private void WriteCancelRegisteredItem()
    {
        WriteC(type.GetId());
        WriteC(unk);
        WriteD(itemId);
    }

    private void WriteSearchedItems()
    {
        WriteC(type.GetId());
        WriteD(itemsCount);
        WriteC(0);
        WriteH(startPage);
        WriteH(brokerItems.Length > 36 ? 36 : brokerItems.Length);
        int counter = 0;
        foreach (BrokerItem item in brokerItems)
        {
            if (counter < 36)
            {
                WriteItemInfo(item);
                counter++;
            }
        }
    }

    private void WriteRegisteredItems()
    {
        WriteC(type.GetId());
        WriteD(0x00);
        WriteH(brokerItems.Length); // you can register a max of 15 items, so 0x0F
        foreach (BrokerItem brokerItem in brokerItems)
        {
            WriteRegisteredItemInfo(brokerItem);
        }
    }

    private void WriteRegisterItem()
    {
        WriteC(type.GetId());
        WriteC(message);
        if (message == 0)
        {
            WriteC(itemsCount + 1); // item pos in list
            BrokerItem itemForRegistration = brokerItems[0];
            WriteRegisteredItemInfo(itemForRegistration);
        }
        else
        {
            WriteB(new byte[174]);
            WriteH(255); // right after creatorName string
            WriteB(new byte[7]);
        }
    }

    private void WriteShowSettledIcon()
    {
        WriteC(type.GetId());
        WriteQ(settledKinah);
        WriteD(0x00);
        WriteH(0x00);
        WriteH(0x01);
        WriteC(0x00);
    }

    private void WriteRemoveSettledIcon()
    {
        WriteH(type.GetId());
    }

    private void WriteShowSettledItems()
    {
        WriteC(type.GetId());
        WriteQ(settledKinah);
        WriteD(totalItemCount); // total item count to determine total page count
        WriteH(pageIndex); // zero-based index of the currently selected page
        WriteC(0); // 1 clears the list (no items must be sent)
        WriteH(brokerItems.Length); // items sent in this packet (client will request items of unsent pages when needed)
        foreach (BrokerItem settledItem in brokerItems)
        {
            WriteD(settledItem.GetItemId());
            WriteQ(settledItem.IsSold() ? settledItem.GetPrice() * settledItem.GetItemCount() : 0);
            WriteQ(settledItem.GetItemCount());
            WriteQ(settledItem.GetItemCount());
            WriteD((int)(settledItem.GetSettleTime().ToUnixTimeMilliseconds() / 60000));
            if (settledItem.GetItem() == null) // sold items are null because they are not in item_location 126 (broker) anymore
                WriteB(new byte[EnchantInfoBlobEntry.SIZE]);
            else
                EnchantInfoBlobEntry.WriteInfo(GetBuf(), settledItem.GetItem());
            WriteS(settledItem.GetItemCreator());
        }
    }

    private void WriteShowSellWindow()
    {
        WriteC(type.GetId());
        WriteC(unk);
        WriteD(itemId);
        WriteD(0);
        WriteD(0);
        WriteC(3);// 7dayAverage
        WriteQ(currentLow);// currentLow
        WriteQ(currentHigh);// currentHigh
    }

    private void WriteRegisteredItemInfo(BrokerItem brokerItem)
    {
        Item item = brokerItem.GetItem();

        WriteD(brokerItem.GetItemUniqueId());
        WriteD(brokerItem.GetItemId());
        WriteQ(brokerItem.GetPrice() * brokerItem.GetItemCount());
        WriteQ(item.GetItemCount());
        WriteQ(item.GetItemCount());
        int daysLeft = (int)((brokerItem.GetExpireTime().ToUnixTimeMilliseconds() - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 86400000);
        WriteC(daysLeft);

        EnchantInfoBlobEntry.WriteInfo(GetBuf(), item);
        // ItemInfoBlob.newBlobEntry(ItemBlobType.PREMIUM_OPTION, null, item).writeThisBlob(getBuf());

        WriteS(brokerItem.GetItemCreator());
        WriteH(0);
        WriteC(0);
        ItemInfoBlob.NewBlobEntry(ItemBlobType.POLISH_INFO, null, item).WriteThisBlob(GetBuf());
        ItemInfoBlob.NewBlobEntry(ItemBlobType.WRAP_INFO, null, item).WriteThisBlob(GetBuf());
        WriteC(brokerItem.IsSplittingAvailable() ? 1 : 0);
    }

    private void WriteItemInfo(BrokerItem brokerItem)
    {
        Item item = brokerItem.GetItem();

        WriteD(item.GetObjectId());
        WriteD(item.GetItemTemplate().GetTemplateId());
        WriteQ(brokerItem.GetPrice() * brokerItem.GetItemCount());
        WriteQ(brokerItem.GetAveragePrice()); // AWR price
        WriteQ(item.GetItemCount());

        EnchantInfoBlobEntry.WriteInfo(GetBuf(), item);
        // ItemInfoBlob.newBlobEntry(ItemBlobType.PREMIUM_OPTION, null, item).writeThisBlob(getBuf());

        WriteS(PlayerService.GetPlayerName(brokerItem.GetSellerId()));
        WriteS(brokerItem.GetItemCreator()); // creator
        WriteH(0);
        WriteC(0);
        ItemInfoBlob.NewBlobEntry(ItemBlobType.POLISH_INFO, null, item).WriteThisBlob(GetBuf());
        ItemInfoBlob.NewBlobEntry(ItemBlobType.WRAP_INFO, null, item).WriteThisBlob(GetBuf());
        WriteC(brokerItem.IsSplittingAvailable() ? 1 : 0);
    }
}
