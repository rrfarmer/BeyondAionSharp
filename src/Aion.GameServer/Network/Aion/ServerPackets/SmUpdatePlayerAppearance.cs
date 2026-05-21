using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmUpdatePlayerAppearance : GameServerPacket
{
	public const int PacketOpCode = 36;
	private const int CubeStorageId = 0;
	private const long SubHand = 1L << 1;
	private const long MainOrSub = 1L | SubHand;
	private const long MainOffOrSubOff = (1L << 17) | (1L << 18);
	private const long VisibleSlots =
		1L
		| (1L << 1)
		| (1L << 2)
		| (1L << 3)
		| (1L << 4)
		| (1L << 5)
		| (1L << 6)
		| (1L << 7)
		| (1L << 10)
		| (1L << 11)
		| (1L << 12)
		| (1L << 13)
		| (1L << 14)
		| (1L << 15)
		| (1L << 19);

	private readonly Player _player;
	public SmUpdatePlayerAppearance(Player player)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_UPDATE_PLAYER_APPEARANCE.
		_player = player;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_player.ObjectId);
		WriteEquippedItems(buffer);
	}

	private void WriteEquippedItems(PacketBuffer buffer)
	{
		// Java parity: network/aion/serverpackets/AbstractPlayerInfoPacket.writeEquippedItems.
		var items = _player.InventoryItems
			.Where(item => item.Location == CubeStorageId && item.IsEquipped && IsVisible(item.Slot))
			.OrderBy(item => item.Slot)
			.ThenBy(item => item.ObjectId)
			.ToArray();

		var mask = 0;
		foreach (var item in items)
		{
			mask |= unchecked((int)item.Slot);
			if (IsTwoHandedWeaponSlot(item.Slot))
				mask &= unchecked((int)~SubHand);
		}

		buffer.WriteD(mask);
		foreach (var item in items)
		{
			buffer.WriteD(item.ItemSkin == 0 ? item.ItemId : item.ItemSkin);
			buffer.WriteD(item.Godstone?.ItemId ?? 0);
			WriteDyeInfo(buffer, item.Color);
			buffer.WriteH(item.Enchant);
			buffer.WriteH(0);
		}
	}

	private static bool IsVisible(long slot)
	{
		return (VisibleSlots & slot) == slot;
	}

	private static bool IsTwoHandedWeaponSlot(long slot)
	{
		return (slot & MainOrSub) == MainOrSub || (slot & MainOffOrSubOff) == MainOffOrSubOff;
	}

	private static void WriteDyeInfo(PacketBuffer buffer, int? rgb)
	{
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
}
