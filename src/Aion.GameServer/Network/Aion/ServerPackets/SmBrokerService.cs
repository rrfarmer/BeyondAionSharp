using Aion.Commons.Network;

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

	public SmBrokerService(long settledKinah)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_BROKER_SERVICE(boolean showSettledIcon, long settledKinah).
		_type = BrokerPacketType.ShowSettledIcon;
		_settledKinah = settledKinah;
	}

	private SmBrokerService(BrokerPacketType type, long settledKinah = 0, int totalItemCount = 0, int pageIndex = 0, int itemId = 0, byte unknown = 0, long currentLow = 0, long currentHigh = 0)
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
	}

	public static SmBrokerService CreateEmptySearchedItems(int totalItemCount, int startPage)
	{
		// Java parity: SM_BROKER_SERVICE(BrokerItem[] brokerItems, int itemsCount, int startPage).
		return new SmBrokerService(BrokerPacketType.SearchedItems, totalItemCount: totalItemCount, pageIndex: startPage);
	}

	public static SmBrokerService CreateEmptyRegisteredItems()
	{
		// Java parity: SM_BROKER_SERVICE(BrokerItem[] brokerItems).
		return new SmBrokerService(BrokerPacketType.RegisteredItems);
	}

	public static SmBrokerService CreateEmptySettledItems(int totalItemCount, int pageIndex, long settledKinah)
	{
		// Java parity: SM_BROKER_SERVICE(List<BrokerItem>, int totalItemCount, int pageIndex, long settledKinah).
		return new SmBrokerService(BrokerPacketType.SettledItems, settledKinah, totalItemCount, pageIndex);
	}

	public static SmBrokerService CreateRemoveSettledIcon()
	{
		// Java parity: SM_BROKER_SERVICE(false, 0).
		return new SmBrokerService(BrokerPacketType.RemoveSettledIcon);
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
		buffer.WriteH(0);
	}

	private static void WriteRegisteredItems(PacketBuffer buffer)
	{
		// Java parity: SM_BROKER_SERVICE.writeRegisteredItems.
		buffer.WriteC(1);
		buffer.WriteD(0);
		buffer.WriteH(0);
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
		buffer.WriteH(0);
	}

	private static void WriteRemoveSettledIcon(PacketBuffer buffer)
	{
		// Java parity: SM_BROKER_SERVICE.writeRemoveSettledIcon.
		buffer.WriteH(6);
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

	private enum BrokerPacketType
	{
		SearchedItems,
		RegisteredItems,
		ShowSettledIcon,
		SettledItems,
		RemoveSettledIcon,
		ShowSellWindow,
	}
}
