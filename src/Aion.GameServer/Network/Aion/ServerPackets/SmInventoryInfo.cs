using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmInventoryInfo : GameServerPacket
{
	public const int PacketOpCode = 26;
	private const int CubeStorageId = 0;
	private const int KinahItemId = 182400001;
	private const int FirstAvailableSlot = 65535;
	private const int ItemsPerPacket = 10;

	private readonly bool _isFirstPacket;
	private readonly IReadOnlyList<InventoryPacketItem> _items;
	private readonly Player _player;

	private SmInventoryInfo(bool isFirstPacket, IReadOnlyList<InventoryPacketItem> items, Player player)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_INVENTORY_INFO(boolean, List<Item>, Player).
		_isFirstPacket = isFirstPacket;
		_items = items;
		_player = player;
	}

	public static IReadOnlyList<SmInventoryInfo> CreateLoginPackets(
		Player player,
		ItemTemplateTable itemTemplates,
		Func<int>? nextObjectId = null)
	{
		// Java parity: services/player/PlayerEnterWorldService.sendItemInfos.
		var allItems = BuildLoginItemList(player, itemTemplates, nextObjectId);
		var packets = new List<SmInventoryInfo>();
		for (var offset = 0; offset < allItems.Count; offset += ItemsPerPacket)
		{
			var part = allItems.Skip(offset).Take(ItemsPerPacket).ToArray();
			packets.Add(new SmInventoryInfo(offset == 0, part, player));
		}

		packets.Add(new SmInventoryInfo(false, Array.Empty<InventoryPacketItem>(), player));
		return packets;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_INVENTORY_INFO.writeImpl.
		buffer.WriteC(_isFirstPacket ? 1 : 0);
		buffer.WriteC(_player.NpcExpands);
		buffer.WriteC(_player.QuestExpands);
		buffer.WriteC(_player.ItemExpands);
		buffer.WriteH(_items.Count);
		foreach (var item in _items)
			WriteItemInfo(buffer, item);
	}

	private static IReadOnlyList<InventoryPacketItem> BuildLoginItemList(
		Player player,
		ItemTemplateTable itemTemplates,
		Func<int>? nextObjectId)
	{
		var cubeItems = player.InventoryItems
			.Where(item => item.Location == CubeStorageId)
			.ToArray();
		var allItems = new List<InventoryPacketItem>();
		var kinah = cubeItems.FirstOrDefault(item => item.ItemId == KinahItemId)
			?? new InventoryItem
			{
				ObjectId = nextObjectId?.Invoke() ?? 0,
				ItemId = KinahItemId,
				Count = 0,
				Location = CubeStorageId,
				Slot = FirstAvailableSlot,
			};
		AddIfTemplateExists(allItems, kinah, itemTemplates);

		foreach (var item in cubeItems.Where(item => item.ItemId != KinahItemId && item.IsEquipped).OrderBy(item => item.Slot).ThenBy(item => item.ObjectId))
			AddIfTemplateExists(allItems, item, itemTemplates);

		foreach (var item in cubeItems.Where(item => item.ItemId != KinahItemId && !item.IsEquipped).OrderBy(item => item.Slot).ThenBy(item => item.ObjectId))
			AddIfTemplateExists(allItems, item, itemTemplates);

		return allItems;
	}

	private static void AddIfTemplateExists(List<InventoryPacketItem> items, InventoryItem item, ItemTemplateTable itemTemplates)
	{
		var template = itemTemplates.GetItemTemplate(item.ItemId);
		if (template != null)
			items.Add(new InventoryPacketItem(item, template));
	}

	private static void WriteItemInfo(PacketBuffer buffer, InventoryPacketItem packetItem)
	{
		var item = packetItem.Item;
		var template = packetItem.Template;
		buffer.WriteD(item.ObjectId);
		buffer.WriteD(template.TemplateId);
		buffer.WriteS(template.GetClientName());
		WriteItemInfoBlob(buffer, item, template);
		buffer.WriteH((int)(item.Slot & 0xffff));
		buffer.WriteC(template.IsCloth ? 1 : 0);
	}

	private static void WriteItemInfoBlob(PacketBuffer buffer, InventoryItem item, ItemTemplateSummary template)
	{
		// Java parity: network/aion/iteminfo/ItemInfoBlob.getFullBlob.
		using var blob = new PacketBuffer();

		if (item.FusionedItem != 0 || template.IsTwoHandWeapon)
			WriteCompositeItemBlob(blob, item);

		if (template.ValidEquipmentSlots != 0)
		{
			WriteEquippedSlotBlob(blob, item);
			if (template.IsWing)
				WriteSlotPairBlob(blob, 0x0d, FirstSlot(template.ValidEquipmentSlots), 0);
			else if (template.IsShield)
				WriteDyeableSlotPairBlob(blob, 0x03, FirstSlot(template.ValidEquipmentSlots), 0, item.Color);
			else if (template.IsPlume)
				WritePlumeInfoBlob(blob, template);
			else if (template.IsArmor)
			{
				if (template.IsAccessory)
					WriteAccessoryInfoBlob(blob, template);
				else
					WriteDyeableSlotPairBlob(blob, 0x02, FirstSlot(template.ValidEquipmentSlots), 0, item.Color);
			}
			else if (template.IsWeapon)
			{
				WriteWeaponInfoBlob(blob, item, template);
			}

			WriteEnchantInfoBlob(blob, item);
			if (item.Charge > 0)
				WriteConditioningInfoBlob(blob, item);
			if (template.CanPolish)
				WritePolishInfoBlob(blob);
			WritePremiumOptionBlob(blob, item);
		}

		if (template.IsStigmaShard)
			WriteStigmaShardBlob(blob);

		WriteGeneralInfoBlob(blob, item, template);
		if (item.PackCount != 0)
			WriteWrapInfoBlob(blob, item);

		var blobBytes = blob.ToArray();
		buffer.WriteH(blobBytes.Length);
		buffer.WriteB(blobBytes);
	}

	private static void WriteBlob(PacketBuffer buffer, int entryId, Action<PacketBuffer> writePayload)
	{
		buffer.WriteC(entryId);
		writePayload(buffer);
	}

	private static void WriteCompositeItemBlob(PacketBuffer buffer, InventoryItem item)
	{
		WriteBlob(
			buffer,
			0x0e,
			payload =>
			{
				// Java parity: network/aion/iteminfo/CompositeItemBlobEntry.writeThisBlob.
				payload.WriteD(item.FusionedItem);
				for (var i = 0; i < 6; i++)
					payload.WriteD(0);
				payload.WriteC(item.OptionalFusionSocket);
				payload.WriteC(0);
			});
	}

	private static void WriteEquippedSlotBlob(PacketBuffer buffer, InventoryItem item)
	{
		WriteBlob(
			buffer,
			0x06,
			payload =>
			{
				// Java parity: network/aion/iteminfo/EquippedSlotBlobEntry.writeThisBlob.
				payload.WriteQ(item.IsEquipped ? item.Slot : 0);
			});
	}

	private static void WriteWeaponInfoBlob(PacketBuffer buffer, InventoryItem item, ItemTemplateSummary template)
	{
		WriteBlob(
			buffer,
			0x01,
			payload =>
			{
				// Java parity: network/aion/iteminfo/WeaponInfoBlobEntry.writeThisBlob.
				var slots = SlotsFor(template.ValidEquipmentSlots);
				if (slots.Length == 1)
				{
					payload.WriteQ(slots[0]);
					payload.WriteQ(item.FusionedItem != 0 ? 0 : 2);
					return;
				}

				if (template.IsTwoHandWeapon)
				{
					payload.WriteQ(slots[0] | slots[1]);
					payload.WriteQ(0);
					return;
				}

				payload.WriteQ(slots[0]);
				payload.WriteQ(slots[1]);
			});
	}

	private static void WriteAccessoryInfoBlob(PacketBuffer buffer, ItemTemplateSummary template)
	{
		WriteBlob(
			buffer,
			0x04,
			payload =>
			{
				// Java parity: network/aion/iteminfo/AccessoryInfoBlobEntry.writeThisBlob.
				var slots = SlotsFor(template.ValidEquipmentSlots);
				payload.WriteQ(slots.Length > 0 ? slots[0] : 0);
				payload.WriteQ(slots.Length > 1 ? slots[1] : 0);
			});
	}

	private static void WriteSlotPairBlob(PacketBuffer buffer, int entryId, long firstSlot, long secondSlot)
	{
		WriteBlob(
			buffer,
			entryId,
			payload =>
			{
				payload.WriteQ(firstSlot);
				payload.WriteQ(secondSlot);
			});
	}

	private static void WriteDyeableSlotPairBlob(PacketBuffer buffer, int entryId, long firstSlot, long secondSlot, int? color)
	{
		WriteBlob(
			buffer,
			entryId,
			payload =>
			{
				payload.WriteQ(firstSlot);
				payload.WriteQ(secondSlot);
				WriteDyeInfo(payload, color);
			});
	}

	private static void WritePlumeInfoBlob(PacketBuffer buffer, ItemTemplateSummary template)
	{
		WriteBlob(
			buffer,
			0x13,
			payload =>
			{
				// Java parity: network/aion/iteminfo/PlumeInfoBlobEntry.writeThisBlob.
				payload.WriteQ(FirstSlot(template.ValidEquipmentSlots));
				payload.WriteQ(0x100000);
				payload.WriteD(0);
				payload.WriteD(0);
				payload.WriteD(0);
				payload.WriteD(0);
			});
	}

	private static void WriteEnchantInfoBlob(PacketBuffer buffer, InventoryItem item)
	{
		WriteBlob(
			buffer,
			0x0b,
			payload =>
			{
				// Java parity: network/aion/iteminfo/EnchantInfoBlobEntry.writeInfo.
				payload.WriteC(item.IsSoulBound ? 1 : 0);
				payload.WriteC(item.Enchant);
				payload.WriteD(item.ItemSkin == 0 ? item.ItemId : item.ItemSkin);
				payload.WriteC(item.OptionalSocket);
				payload.WriteC(item.EnchantBonus);
				for (var i = 0; i < 6; i++)
					payload.WriteD(0);
				payload.WriteD(0);
				var dyeExpiration = GetRemainingSeconds(item.ColorExpires);
				WriteDyeInfo(payload, dyeExpiration < 0 ? null : item.Color);
				payload.WriteC(0);
				payload.WriteD(0);
				payload.WriteD(Math.Max(0, dyeExpiration));
				payload.WriteD(0);
				payload.WriteC(0);
				payload.WriteC(item.Tempering);
				payload.WriteD(0);
				payload.WriteC(0);
				payload.WriteD(0);
				payload.WriteC(0);
				payload.WriteD(0);
				payload.WriteD(0);
				for (var i = 0; i < 13; i++)
					payload.WriteD(0);
				payload.WriteC(item.IsAmplified ? 1 : 0);
				payload.WriteD(item.BuffSkill);
				payload.WriteD(0);
				payload.WriteD(0);
			});
	}

	private static void WriteConditioningInfoBlob(PacketBuffer buffer, InventoryItem item)
	{
		WriteBlob(buffer, 0x0f, payload => payload.WriteD(item.Charge));
	}

	private static void WritePolishInfoBlob(PacketBuffer buffer)
	{
		WriteBlob(buffer, 0x11, payload => payload.WriteD(0));
	}

	private static void WritePremiumOptionBlob(PacketBuffer buffer, InventoryItem item)
	{
		WriteBlob(
			buffer,
			0x10,
			payload =>
			{
				// Java parity: network/aion/iteminfo/PremiumOptionInfoBlobEntry.writeThisBlob.
				payload.WriteC(item.RandomBonus);
				payload.WriteC(item.TuneCount);
				payload.WriteC(0);
			});
	}

	private static void WriteStigmaShardBlob(PacketBuffer buffer)
	{
		WriteBlob(buffer, 0x08, payload => payload.WriteD(0));
	}

	private static void WriteGeneralInfoBlob(PacketBuffer buffer, InventoryItem item, ItemTemplateSummary template)
	{
		WriteBlob(
			buffer,
			0x00,
			payload =>
			{
				// Java parity: network/aion/iteminfo/GeneralInfoBlobEntry.writeThisBlob.
				payload.WriteH(template.Mask);
				payload.WriteQ(item.Count);
				payload.WriteS(item.Creator ?? string.Empty);
				payload.WriteC(0);
				payload.WriteD(GetRemainingSeconds(item.ExpireTime));
				payload.WriteD(0);
				payload.WriteD(0);
				payload.WriteH(0);
				payload.WriteD(0);
				payload.WriteH(18);
			});
	}

	private static void WriteWrapInfoBlob(PacketBuffer buffer, InventoryItem item)
	{
		WriteBlob(buffer, 0x12, payload => payload.WriteC(item.PackCount));
	}

	private static void WriteDyeInfo(PacketBuffer buffer, int? rgb)
	{
		// Java parity: network/PacketWriteHelper.writeDyeInfo.
		if (!rgb.HasValue)
		{
			buffer.WriteD(0);
			return;
		}

		buffer.WriteC(1);
		buffer.WriteC((rgb.Value & 0xff0000) >> 16);
		buffer.WriteC((rgb.Value & 0xff00) >> 8);
		buffer.WriteC(rgb.Value & 0xff);
	}

	private static int GetRemainingSeconds(int expirationEpochSeconds)
	{
		return expirationEpochSeconds == 0
			? 0
			: expirationEpochSeconds - (int)DateTimeOffset.Now.ToUnixTimeSeconds();
	}

	private static long FirstSlot(long slotMask)
	{
		var slots = SlotsFor(slotMask);
		return slots.Length == 0 ? 0 : slots[0];
	}

	private static long[] SlotsFor(long slotMask)
	{
		ReadOnlySpan<long> slots =
		[
			1L,
			1L << 1,
			1L << 2,
			1L << 3,
			1L << 4,
			1L << 5,
			1L << 6,
			1L << 7,
			1L << 8,
			1L << 9,
			1L << 10,
			1L << 11,
			1L << 12,
			1L << 13,
			1L << 14,
			1L << 15,
			1L << 16,
			1L << 17,
			1L << 18,
			1L << 19,
			1L << 30,
			1L << 31,
			1L << 32,
			1L << 33,
			1L << 34,
			1L << 35,
		];

		var result = new List<long>();
		foreach (var slot in slots)
		{
			if ((slotMask & slot) == slot)
				result.Add(slot);
		}

		return result.ToArray();
	}

	private sealed record InventoryPacketItem(InventoryItem Item, ItemTemplateSummary Template);
}
