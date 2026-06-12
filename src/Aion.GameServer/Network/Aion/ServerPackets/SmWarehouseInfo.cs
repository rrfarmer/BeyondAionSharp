using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmWarehouseInfo : GameServerPacket
{
	public const int PacketOpCode = 168;
	private const int RegularWarehouse = 1;
	private const int AccountWarehouse = 2;
	private const int LegionWarehouse = 3;
	private const int KinahItemId = 182400001;
	private const int ItemsPerPacket = 10;

	private readonly int _warehouseType;
	private readonly int _expandLevel;
	private readonly bool _isFirstPacket;
	private readonly IReadOnlyList<WarehousePacketItem> _items;

	private SmWarehouseInfo(int warehouseType, int expandLevel, bool isFirstPacket, IReadOnlyList<WarehousePacketItem> items)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_WAREHOUSE_INFO(Collection<Item>, int, int, boolean, Player).
		_warehouseType = warehouseType;
		_expandLevel = expandLevel;
		_isFirstPacket = isFirstPacket;
		_items = items;
	}

	public static IReadOnlyList<SmWarehouseInfo> CreateLoginPackets(
		Player player,
		ItemTemplateTable itemTemplates,
		bool includeAuxiliaryStoragePlaceholders = true,
		ItemRestrictionCleanupTable? itemRestrictionCleanups = null)
	{
		// Java parity: services/player/PlayerEnterWorldService.sendWarehouseItemInfos.
		var packets = new List<SmWarehouseInfo>();
		AddRegularWarehousePackets(packets, player, itemTemplates, itemRestrictionCleanups);
		AddAccountWarehousePackets(packets, player, itemTemplates, itemRestrictionCleanups);
		if (includeAuxiliaryStoragePlaceholders)
			AddAuxiliaryStoragePlaceholders(packets);
		return packets;
	}

	public static IReadOnlyList<SmWarehouseInfo> CreateRegularWarehouseUpdatePackets(
		Player player,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable? itemRestrictionCleanups = null)
	{
		// Java parity: services/WarehouseService.sendWarehouseInfo(player, false).
		var packets = new List<SmWarehouseInfo>();
		AddRegularWarehousePackets(packets, player, itemTemplates, itemRestrictionCleanups);
		packets.Add(new SmWarehouseInfo(AccountWarehouse, expandLevel: 0, isFirstPacket: false, Array.Empty<WarehousePacketItem>()));
		return packets;
	}

	public static IReadOnlyList<SmWarehouseInfo> CreateLegionWarehouseOpenPackets(
		Player player,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable? itemRestrictionCleanups = null)
	{
		// Java parity: services/LegionService.openLegionWarehouse builds SM_WAREHOUSE_INFO chunks
		// with StorageType.LEGION_WAREHOUSE id and Legion.getWarehouseExpansions().
		var packets = new List<SmWarehouseInfo>();
		var legionItems = player.InventoryItems
			.Where(item => item.Location == LegionWarehouse && item.ItemId != KinahItemId)
			.OrderBy(item => item.Slot)
			.ThenBy(item => item.ObjectId);
		var items = BuildWarehouseItems(legionItems, itemTemplates, itemRestrictionCleanups);
		var expandLevel = player.LegionWarehouseExpansions;
		for (var offset = 0; offset < items.Length; offset += ItemsPerPacket)
		{
			packets.Add(
				new SmWarehouseInfo(
					LegionWarehouse,
					expandLevel,
					offset == 0,
					items.Skip(offset).Take(ItemsPerPacket).ToArray()));
		}

		packets.Add(new SmWarehouseInfo(LegionWarehouse, expandLevel, isFirstPacket: items.Length == 0, Array.Empty<WarehousePacketItem>()));
		return packets;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_WAREHOUSE_INFO.writeImpl.
		buffer.WriteC(_warehouseType);
		buffer.WriteC(_isFirstPacket ? 1 : 0);
		buffer.WriteC(_expandLevel);
		if (_warehouseType == RegularWarehouse && _items.Count > 0)
		{
			buffer.WriteC(1);
			buffer.WriteC(0);
		}
		else
		{
			buffer.WriteH(0);
		}

		buffer.WriteH(_items.Count);
		foreach (var item in _items)
			WriteItemInfo(buffer, item);
	}

	private static void AddRegularWarehousePackets(
		List<SmWarehouseInfo> packets,
		Player player,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		// Java parity: services/WarehouseService.sendWarehouseInfo regular warehouse branch.
		var regularItems = player.WarehouseItems
			.Where(item => item.ItemId != KinahItemId)
			.OrderBy(item => item.Slot)
			.ThenBy(item => item.ObjectId);
		var items = BuildWarehouseItems(regularItems, itemTemplates, itemRestrictionCleanups);
		var expandLevel = (player.GetCommonData().GetWhNpcExpands()) + (player.GetCommonData().GetWhBonusExpands());
		for (var offset = 0; offset < items.Length; offset += ItemsPerPacket)
		{
			packets.Add(
				new SmWarehouseInfo(
					RegularWarehouse,
					expandLevel,
					offset == 0,
					items.Skip(offset).Take(ItemsPerPacket).ToArray()));
		}

		packets.Add(new SmWarehouseInfo(RegularWarehouse, expandLevel, isFirstPacket: false, Array.Empty<WarehousePacketItem>()));
	}

	private static void AddAccountWarehousePackets(
		List<SmWarehouseInfo> packets,
		Player player,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		// Java parity: services/WarehouseService.sendWarehouseInfo account warehouse branch.
		var accountItems = player.AccountWarehouseItems
			.Where(item => item.ItemId != KinahItemId)
			.OrderBy(item => item.Slot)
			.ThenBy(item => item.ObjectId)
			.Concat(player.AccountWarehouseItems.Where(item => item.ItemId == KinahItemId).OrderBy(item => item.ObjectId));
		packets.Add(new SmWarehouseInfo(AccountWarehouse, expandLevel: 0, isFirstPacket: true, BuildWarehouseItems(accountItems, itemTemplates, itemRestrictionCleanups)));
		packets.Add(new SmWarehouseInfo(AccountWarehouse, expandLevel: 0, isFirstPacket: false, Array.Empty<WarehousePacketItem>()));
	}

	private static void AddAuxiliaryStoragePlaceholders(List<SmWarehouseInfo> packets)
	{
		// Java parity: PlayerEnterWorldService sends empty pet-bag and housing-warehouse placeholders for absent storages.
		for (var storageType = 30; storageType <= 79; storageType++)
		{
			if (storageType >= 50 && storageType < 60)
				continue;
			packets.Add(new SmWarehouseInfo(storageType, expandLevel: 0, isFirstPacket: true, Array.Empty<WarehousePacketItem>()));
		}
	}

	private static WarehousePacketItem[] BuildWarehouseItems(
		IEnumerable<InventoryItem> items,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		var packetItems = new List<WarehousePacketItem>();
		foreach (var item in items)
		{
			var template = itemTemplates.GetItemTemplate(item.ItemId);
			if (template != null)
			{
				var warehouseRestrictionFlag = itemRestrictionCleanups?.HasAccountOrLegionWarehouseStorabilityDisabled(item.ItemId) == true ? 3 : 0;
				packetItems.Add(new WarehousePacketItem(item, template, warehouseRestrictionFlag));
			}
		}

		return packetItems.ToArray();
	}

	private static void WriteItemInfo(PacketBuffer buffer, WarehousePacketItem packetItem)
	{
		var item = packetItem.Item;
		var template = packetItem.Template;
		buffer.WriteD(item.ObjectId);
		buffer.WriteD(template.TemplateId);
		buffer.WriteC(0);
		buffer.WriteS(template.GetClientName());
		SmInventoryInfo.WriteItemInfoBlob(buffer, item, template, packetItem.GeneralInfoWarehouseRestrictionFlag);
		buffer.WriteH((int)(item.Slot & 0xffff));
	}

	private sealed record WarehousePacketItem(InventoryItem Item, ItemTemplateSummary Template, int GeneralInfoWarehouseRestrictionFlag);
}
