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

	internal static void WriteItemInfoBlob(PacketBuffer buffer, InventoryItem item, ItemTemplateSummary template)
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

			WriteEnchantInfoBlob(blob, item, template);
			if (template.ConditioningMaxLevel > 0 || item.Charge > 0)
				WriteConditioningInfoBlob(blob, item);
			if (template.CanPolish)
				WritePolishInfoBlob(blob, item);
			WritePremiumOptionBlob(blob, item);
			WriteStatBonusBlobs(blob, template);
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
				WriteStoneSlots(payload, item.FusionStones);
				payload.WriteC(item.OptionalFusionSocket);
				payload.WriteC(item.FusionRandomBonus);
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

	private static void WriteEnchantInfoBlob(PacketBuffer buffer, InventoryItem item, ItemTemplateSummary template)
	{
		WriteBlob(
			buffer,
			0x0b,
			payload => WriteEnchantInfo(payload, item, template));
	}

	internal static void WriteEnchantInfo(PacketBuffer payload, InventoryItem item) =>
		WriteEnchantInfo(payload, item, template: null);

	internal static void WriteEnchantInfo(PacketBuffer payload, InventoryItem item, ItemTemplateSummary? template)
	{
		// Java parity: network/aion/iteminfo/EnchantInfoBlobEntry.writeInfo.
		payload.WriteC(item.IsSoulBound ? 1 : 0);
		payload.WriteC(item.Enchant);
		payload.WriteD(item.ItemSkin == 0 ? item.ItemId : item.ItemSkin);
		payload.WriteC(item.IsIdentified ? item.OptionalSocket : -1);
		payload.WriteC(item.IsIdentified ? item.EnchantBonus : -1);
		WriteStoneSlots(payload, item.ManaStones);
		payload.WriteD(item.Godstone?.ItemId ?? 0);
		var dyeExpiration = GetRemainingSeconds(item.ColorExpires);
		WriteDyeInfo(payload, dyeExpiration < 0 ? null : item.Color);
		payload.WriteC(0);
		payload.WriteD(0);
		payload.WriteD(Math.Max(0, dyeExpiration));
		if (item.IdianStone is { PolishNumber: > 0 } idianStone)
		{
			payload.WriteD(idianStone.ItemId);
			payload.WriteC(idianStone.PolishNumber);
		}
		else
		{
			payload.WriteD(0);
			payload.WriteC(0);
		}
		payload.WriteC(item.Tempering);
		payload.WriteD(0);
		payload.WriteC(0);
		payload.WriteD(0);
		payload.WriteC(0);
		payload.WriteD(0);
		payload.WriteD(0);
		WritePlumeTemperingStatPairs(payload, item, template);
		for (var i = 0; i < 9; i++)
			payload.WriteD(0);
		payload.WriteC(item.IsAmplified ? 1 : 0);
		payload.WriteD(item.BuffSkill);
		payload.WriteD(0);
		payload.WriteD(0);
	}

	private static void WritePlumeTemperingStatPairs(PacketBuffer payload, InventoryItem item, ItemTemplateSummary? template)
	{
		// Java parity: network/aion/iteminfo/EnchantInfoBlobEntry.writeThisBlob + model/stats/container/PlumStatEnum.
		if (item.Tempering <= 0 || template?.IsPlume != true)
		{
			for (var i = 0; i < 4; i++)
				payload.WriteD(0);
			return;
		}

		payload.WriteD(42);
		payload.WriteD(150 * item.Tempering);
		var isPhysicalPlume = string.Equals(template.TemperingName, "TSHIRT_PHYSICAL", StringComparison.Ordinal);
		payload.WriteD(isPhysicalPlume ? 30 : 35);
		payload.WriteD((isPhysicalPlume ? 4 : 20) * item.Tempering + item.RandomPlumeBonus);
	}

	internal static void WriteConditioningInfoBlob(PacketBuffer buffer, InventoryItem item)
	{
		WriteBlob(buffer, 0x0f, payload => payload.WriteD(item.Charge));
	}

	internal static void WritePolishInfoBlob(PacketBuffer buffer, InventoryItem item)
	{
		// Java parity: network/aion/iteminfo/PolishInfoBlobEntry.writeThisBlob.
		WriteBlob(buffer, 0x11, payload => payload.WriteD(item.IdianStone?.PolishCharge ?? 0));
	}

	private static void WriteStoneSlots(PacketBuffer buffer, IReadOnlyList<ItemStoneSocket> stones)
	{
		// Java parity: EnchantInfoBlobEntry.createManastoneMap and CompositeItemBlobEntry.writeFusionStones.
		var stonesBySlot = stones.ToDictionary(stone => stone.Slot);
		for (var i = 0; i < 6; i++)
			buffer.WriteD(stonesBySlot.GetValueOrDefault(i)?.ItemId ?? 0);
	}

	private static void WritePremiumOptionBlob(PacketBuffer buffer, InventoryItem item)
	{
		WriteBlob(
			buffer,
			0x10,
			payload =>
			{
				// Java parity: network/aion/iteminfo/PremiumOptionInfoBlobEntry.writeThisBlob.
				payload.WriteC(item.IsIdentified ? item.RandomBonus : -1);
				payload.WriteC(item.IsIdentified ? item.TuneCount : 0);
				payload.WriteC(0);
			});
	}

	private static void WriteStatBonusBlobs(PacketBuffer buffer, ItemTemplateSummary template)
	{
		foreach (var modifier in template.StatModifiers)
		{
			if (!modifier.Bonus || modifier.ChargeCondition != 0 || !TryGetJavaItemStoneMaskAndSign(modifier.Name, out var mask, out var sign))
				continue;

			WriteBlob(
				buffer,
				0x0a,
				payload =>
				{
					// Java parity: network/aion/iteminfo/BonusInfoBlobEntry.writeThisBlob.
					payload.WriteH(mask);
					payload.WriteD(modifier.Value * sign);
					payload.WriteC(string.Equals(modifier.Operation, "rate", StringComparison.Ordinal) ? 1 : 0);
				});
		}
	}

	private static bool TryGetJavaItemStoneMaskAndSign(string statName, out int mask, out int sign)
	{
		sign = string.Equals(statName, "ATTACK_SPEED", StringComparison.Ordinal) ? -1 : 1;
		mask = statName switch
		{
			// Java parity: model/stats/container/StatEnum itemStoneMask values.
			"ABNORMAL_RESISTANCE_ALL" => 1,
			"ALLRESIST" => 2,
			"STRVIT" => 3,
			"KNOWIL" => 4,
			"AGIDEX" => 5,
			"POWER" => 6,
			"HEALTH" => 7,
			"ACCURACY" => 8,
			"AGILITY" => 9,
			"KNOWLEDGE" => 10,
			"WILL" => 11,
			"WATER_RESISTANCE" => 12,
			"WIND_RESISTANCE" => 13,
			"EARTH_RESISTANCE" => 14,
			"FIRE_RESISTANCE" => 15,
			"LIGHT_RESISTANCE" => 16,
			"DARK_RESISTANCE" => 17,
			"MAXHP" => 18,
			"REGEN_HP" => 19,
			"MAXMP" => 20,
			"REGEN_MP" => 21,
			"MAXDP" => 22,
			"FLY_TIME" => 23,
			"REGEN_FP" => 24,
			"PHYSICAL_ATTACK" => 25,
			"PHYSICAL_DEFENSE" => 26,
			"MAGICAL_ATTACK" => 27,
			"MAGICAL_RESIST" => 28,
			"ATTACK_SPEED" => 29,
			"PHYSICAL_ACCURACY" => 30,
			"EVASION" => 31,
			"PARRY" => 32,
			"BLOCK" => 33,
			"PHYSICAL_CRITICAL" => 34,
			"HIT_COUNT" => 35,
			"SPEED" => 36,
			"FLY_SPEED" => 37,
			"ATTACK_RANGE" => 38,
			"WEIGHT" => 39,
			"MAGICAL_CRITICAL" => 40,
			"CONCENTRATION" => 41,
			"POISON_RESISTANCE" => 43,
			"BLEED_RESISTANCE" => 44,
			"PARALYZE_RESISTANCE" => 45,
			"SLEEP_RESISTANCE" => 46,
			"ROOT_RESISTANCE" => 47,
			"BLIND_RESISTANCE" => 48,
			"CHARM_RESISTANCE" => 49,
			"DISEASE_RESISTANCE" => 50,
			"SILENCE_RESISTANCE" => 51,
			"FEAR_RESISTANCE" => 52,
			"CURSE_RESISTANCE" => 53,
			"CONFUSE_RESISTANCE" => 54,
			"STUN_RESISTANCE" => 55,
			"PERIFICATION_RESISTANCE" => 56,
			"STUMBLE_RESISTANCE" => 57,
			"STAGGER_RESISTANCE" => 58,
			"OPENAERIAL_RESISTANCE" => 59,
			"SNARE_RESISTANCE" => 60,
			"SLOW_RESISTANCE" => 61,
			"SPIN_RESISTANCE" => 62,
			"BIND_RESISTANCE" => 63,
			"DEFORM_RESISTANCE" => 64,
			"PULLED_RESISTANCE" => 65,
			"NOFLY_RESISTANCE" => 66,
			"POISON_RESISTANCE_PENETRATION" => 69,
			"BLEED_RESISTANCE_PENETRATION" => 70,
			"PARALYZE_RESISTANCE_PENETRATION" => 71,
			"SLEEP_RESISTANCE_PENETRATION" => 72,
			"ROOT_RESISTANCE_PENETRATION" => 73,
			"BLIND_RESISTANCE_PENETRATION" => 74,
			"CHARM_RESISTANCE_PENETRATION" => 75,
			"DISEASE_RESISTANCE_PENETRATION" => 76,
			"SILENCE_RESISTANCE_PENETRATION" => 77,
			"FEAR_RESISTANCE_PENETRATION" => 78,
			"CURSE_RESISTANCE_PENETRATION" => 79,
			"CONFUSE_RESISTANCE_PENETRATION" => 80,
			"STUN_RESISTANCE_PENETRATION" => 81,
			"PERIFICATION_RESISTANCE_PENETRATION" => 82,
			"STUMBLE_RESISTANCE_PENETRATION" => 83,
			"STAGGER_RESISTANCE_PENETRATION" => 84,
			"OPENAERIAL_RESISTANCE_PENETRATION" => 85,
			"SNARE_RESISTANCE_PENETRATION" => 86,
			"SLOW_RESISTANCE_PENETRATION" => 87,
			"SPIN_RESISTANCE_PENETRATION" => 88,
			"BIND_RESISTANCE_PENETRATION" => 89,
			"DEFORM_RESISTANCE_PENETRATION" => 90,
			"PULLED_RESISTANCE_PENETRATION" => 91,
			"NOFLY_RESISTANCE_PENETRATION" => 92,
			"BOOST_MAGICAL_SKILL" => 104,
			"MAGICAL_ACCURACY" => 105,
			"PVP_ATTACK_RATIO" => 106,
			"PVP_DEFEND_RATIO" => 107,
			"BOOST_CASTING_TIME" => 108,
			"BOOST_HATE" => 109,
			"HEAL_BOOST" => 110,
			"PVP_PHYSICAL_ATTACK" => 111,
			"PVP_PHYSICAL_DEFEND" => 112,
			"PVP_MAGICAL_ATTACK" => 113,
			"PVP_MAGICAL_DEFEND" => 114,
			"PHYSICAL_CRITICAL_RESIST" => 115,
			"MAGICAL_CRITICAL_RESIST" => 116,
			"PHYSICAL_CRITICAL_DAMAGE_REDUCE" => 117,
			"MAGICAL_CRITICAL_DAMAGE_REDUCE" => 118,
			"MAGICAL_DEFEND" => 125,
			"MAGIC_SKILL_BOOST_RESIST" => 126,
			_ => 0,
		};
		return mask != 0;
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

	internal static void WriteWrapInfoBlob(PacketBuffer buffer, InventoryItem item)
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
