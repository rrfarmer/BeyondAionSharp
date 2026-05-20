using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmBrokerService : GameServerPacket
{
	public const int PacketOpCode = 146;

	private readonly BrokerPacketType _type;
	private readonly long _settledKinah;
	private readonly int _totalItemCount;
	private readonly int _pageIndex;
	private readonly int _itemId;
	private readonly byte _unknown;
	private readonly long _currentLow;
	private readonly long _currentHigh;
	private readonly IReadOnlyList<PlayerBrokerItem> _brokerItems;

	public SmBrokerService(long settledKinah)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_BROKER_SERVICE(boolean showSettledIcon, long settledKinah).
		_type = BrokerPacketType.ShowSettledIcon;
		_settledKinah = settledKinah;
		_brokerItems = Array.Empty<PlayerBrokerItem>();
	}

	private SmBrokerService(
		BrokerPacketType type,
		long settledKinah = 0,
		int totalItemCount = 0,
		int pageIndex = 0,
		int itemId = 0,
		byte unknown = 0,
		long currentLow = 0,
		long currentHigh = 0,
		IReadOnlyList<PlayerBrokerItem>? brokerItems = null)
		: base(PacketOpCode)
	{
		_type = type;
		_settledKinah = settledKinah;
		_totalItemCount = totalItemCount;
		_pageIndex = pageIndex;
		_itemId = itemId;
		_unknown = unknown;
		_currentLow = currentLow;
		_currentHigh = currentHigh;
		_brokerItems = brokerItems ?? Array.Empty<PlayerBrokerItem>();
	}

	public static SmBrokerService CreateEmptySearchedItems(int totalItemCount, int startPage)
	{
		// Java parity: SM_BROKER_SERVICE(BrokerItem[] brokerItems, int itemsCount, int startPage).
		return new SmBrokerService(BrokerPacketType.SearchedItems, totalItemCount: totalItemCount, pageIndex: startPage);
	}

	public static SmBrokerService CreateSearchedItems(PlayerBrokerItemPage page)
	{
		// Java parity: SM_BROKER_SERVICE(BrokerItem[] brokerItems, int itemsCount, int startPage).
		return new SmBrokerService(BrokerPacketType.SearchedItems, totalItemCount: page.TotalItemCount, pageIndex: page.PageIndex, brokerItems: page.Items);
	}

	public static SmBrokerService CreateEmptyRegisteredItems()
	{
		// Java parity: SM_BROKER_SERVICE(BrokerItem[] brokerItems).
		return new SmBrokerService(BrokerPacketType.RegisteredItems);
	}

	public static SmBrokerService CreateRegisteredItems(IReadOnlyList<PlayerBrokerItem> brokerItems)
	{
		// Java parity: SM_BROKER_SERVICE(BrokerItem[] brokerItems).
		return new SmBrokerService(BrokerPacketType.RegisteredItems, brokerItems: brokerItems.Where(item => item.Item != null).ToArray());
	}

	public static SmBrokerService CreateEmptySettledItems(int totalItemCount, int pageIndex, long settledKinah)
	{
		// Java parity: SM_BROKER_SERVICE(List<BrokerItem>, int totalItemCount, int pageIndex, long settledKinah).
		return new SmBrokerService(BrokerPacketType.SettledItems, settledKinah, totalItemCount, pageIndex);
	}

	public static SmBrokerService CreateSettledItems(PlayerBrokerItemPage page)
	{
		// Java parity: SM_BROKER_SERVICE(List<BrokerItem>, int totalItemCount, int pageIndex, long settledKinah).
		return new SmBrokerService(BrokerPacketType.SettledItems, page.SettledKinah, page.TotalItemCount, page.PageIndex, brokerItems: page.Items);
	}

	public static SmBrokerService CreateRemoveSettledIcon()
	{
		// Java parity: SM_BROKER_SERVICE(false, 0).
		return new SmBrokerService(BrokerPacketType.RemoveSettledIcon);
	}

	public static SmBrokerService CreateCancelRegisteredItem(int itemId, byte unknown = 0)
	{
		// Java parity: SM_BROKER_SERVICE(byte unk, int itemId).
		return new SmBrokerService(BrokerPacketType.CancelRegisteredItem, itemId: itemId, unknown: unknown);
	}

	public static SmBrokerService CreateSellWindow(int itemId, long currentLow = 0, long currentHigh = 0)
	{
		// Java parity: SM_BROKER_SERVICE(byte unk, int itemId, long currentLow, long currentHigh).
		return new SmBrokerService(BrokerPacketType.ShowSellWindow, itemId: itemId, currentLow: currentLow, currentHigh: currentHigh);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_BROKER_SERVICE.writeImpl dispatch.
		switch (_type)
		{
			case BrokerPacketType.SearchedItems:
				WriteSearchedItems(buffer);
				break;
			case BrokerPacketType.RegisteredItems:
				WriteRegisteredItems(buffer);
				break;
			case BrokerPacketType.ShowSettledIcon:
				WriteShowSettledIcon(buffer);
				break;
			case BrokerPacketType.SettledItems:
				WriteShowSettledItems(buffer);
				break;
			case BrokerPacketType.RemoveSettledIcon:
				WriteRemoveSettledIcon(buffer);
				break;
			case BrokerPacketType.CancelRegisteredItem:
				WriteCancelRegisteredItem(buffer);
				break;
			case BrokerPacketType.ShowSellWindow:
				WriteShowSellWindow(buffer);
				break;
		}
	}

	private void WriteSearchedItems(PacketBuffer buffer)
	{
		// Java parity: SM_BROKER_SERVICE.writeSearchedItems.
		buffer.WriteC(0);
		buffer.WriteD(_totalItemCount);
		buffer.WriteC(0);
		buffer.WriteH(_pageIndex);
		var brokerItems = _brokerItems.Where(item => item.Item != null).Take(36).ToArray();
		buffer.WriteH(brokerItems.Length);
		foreach (var brokerItem in brokerItems)
			WriteItemInfo(buffer, brokerItem);
	}

	private void WriteRegisteredItems(PacketBuffer buffer)
	{
		// Java parity: SM_BROKER_SERVICE.writeRegisteredItems.
		buffer.WriteC(1);
		buffer.WriteD(0);
		buffer.WriteH(_brokerItems.Count);
		foreach (var brokerItem in _brokerItems)
			WriteRegisteredItemInfo(buffer, brokerItem);
	}

	private void WriteShowSettledIcon(PacketBuffer buffer)
	{
		// Java parity: SM_BROKER_SERVICE.writeShowSettledIcon.
		buffer.WriteC(5);
		buffer.WriteQ(_settledKinah);
		buffer.WriteD(0);
		buffer.WriteH(0);
		buffer.WriteH(1);
		buffer.WriteC(0);
	}

	private void WriteShowSettledItems(PacketBuffer buffer)
	{
		// Java parity: SM_BROKER_SERVICE.writeShowSettledItems.
		buffer.WriteC(5);
		buffer.WriteQ(_settledKinah);
		buffer.WriteD(_totalItemCount);
		buffer.WriteH(_pageIndex);
		buffer.WriteC(0);
		buffer.WriteH(_brokerItems.Count);
		foreach (var brokerItem in _brokerItems)
			WriteSettledItemInfo(buffer, brokerItem);
	}

	private static void WriteRemoveSettledIcon(PacketBuffer buffer)
	{
		// Java parity: SM_BROKER_SERVICE.writeRemoveSettledIcon.
		buffer.WriteH(6);
	}

	private void WriteCancelRegisteredItem(PacketBuffer buffer)
	{
		// Java parity: SM_BROKER_SERVICE.writeCancelRegisteredItem.
		buffer.WriteC(4);
		buffer.WriteC(_unknown);
		buffer.WriteD(_itemId);
	}

	private void WriteShowSellWindow(PacketBuffer buffer)
	{
		// Java parity: SM_BROKER_SERVICE.writeShowSellWindow.
		buffer.WriteC(7);
		buffer.WriteC(_unknown);
		buffer.WriteD(_itemId);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteC(3);
		buffer.WriteQ(_currentLow);
		buffer.WriteQ(_currentHigh);
	}

	private static void WriteRegisteredItemInfo(PacketBuffer buffer, PlayerBrokerItem brokerItem)
	{
		// Java parity: SM_BROKER_SERVICE.writeRegisteredItemInfo.
		var item = brokerItem.Item;
		if (item == null)
			return;

		buffer.WriteD(brokerItem.ItemObjectId);
		buffer.WriteD(brokerItem.ItemId);
		buffer.WriteQ(brokerItem.Price * brokerItem.ItemCount);
		buffer.WriteQ(item.Count);
		buffer.WriteQ(item.Count);
		buffer.WriteC((int)(brokerItem.ExpireTime - DateTime.Now).TotalDays);
		SmInventoryInfo.WriteEnchantInfo(buffer, item);
		buffer.WriteS(brokerItem.ItemCreator);
		buffer.WriteH(0);
		buffer.WriteC(0);
		SmInventoryInfo.WritePolishInfoBlob(buffer, item);
		SmInventoryInfo.WriteWrapInfoBlob(buffer, item);
		buffer.WriteC(brokerItem.SplittingAvailable ? 1 : 0);
	}

	private static void WriteItemInfo(PacketBuffer buffer, PlayerBrokerItem brokerItem)
	{
		// Java parity: SM_BROKER_SERVICE.writeItemInfo.
		var item = brokerItem.Item;
		if (item == null)
			return;

		buffer.WriteD(item.ObjectId);
		buffer.WriteD(item.ItemId);
		buffer.WriteQ(brokerItem.Price * brokerItem.ItemCount);
		buffer.WriteQ(brokerItem.AveragePrice);
		buffer.WriteQ(item.Count);
		SmInventoryInfo.WriteEnchantInfo(buffer, item);
		buffer.WriteS(brokerItem.SellerName);
		buffer.WriteS(brokerItem.ItemCreator);
		buffer.WriteH(0);
		buffer.WriteC(0);
		SmInventoryInfo.WritePolishInfoBlob(buffer, item);
		SmInventoryInfo.WriteWrapInfoBlob(buffer, item);
		buffer.WriteC(brokerItem.SplittingAvailable ? 1 : 0);
	}

	private static void WriteSettledItemInfo(PacketBuffer buffer, PlayerBrokerItem brokerItem)
	{
		// Java parity: SM_BROKER_SERVICE.writeShowSettledItems item body.
		buffer.WriteD(brokerItem.ItemId);
		buffer.WriteQ(brokerItem.IsSold ? brokerItem.Price * brokerItem.ItemCount : 0);
		buffer.WriteQ(brokerItem.ItemCount);
		buffer.WriteQ(brokerItem.ItemCount);
		buffer.WriteD(GetUnixMinutes(brokerItem.SettleTime));
		if (brokerItem.Item == null)
			buffer.WriteB(new byte[138]);
		else
			SmInventoryInfo.WriteEnchantInfo(buffer, brokerItem.Item);
		buffer.WriteS(brokerItem.ItemCreator);
	}

	private static int GetUnixMinutes(DateTime value)
	{
		var dateTime = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Local) : value;
		return (int)(new DateTimeOffset(dateTime).ToUnixTimeSeconds() / 60);
	}

	private enum BrokerPacketType
	{
		SearchedItems,
		RegisteredItems,
		ShowSettledIcon,
		SettledItems,
		RemoveSettledIcon,
		CancelRegisteredItem,
		ShowSellWindow,
	}
}
