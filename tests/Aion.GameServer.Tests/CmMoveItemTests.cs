using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmMoveItemTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaMoveItemOpcodeAsInGameOnly()
	{
		Assert.IsType<CmMoveItem>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(156, buffer =>
				{
					buffer.WriteD(7001);
					buffer.WriteC(0);
					buffer.WriteC(1);
					buffer.WriteH(0);
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(156, buffer =>
			{
				buffer.WriteD(7001);
				buffer.WriteC(0);
				buffer.WriteC(1);
				buffer.WriteH(0);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_HighBitSlotReadsAsSignedShort()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteD(7001);
		buffer.WriteC(0);
		buffer.WriteC(3);
		buffer.WriteH(0xffff);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(7001, packet.ItemObjectId);
		Assert.Equal(0, packet.Source);
		Assert.Equal(3, packet.Destination);
		Assert.Equal((short)-1, packet.Slot);
	}

	[Fact]
	public void InventoryItem_SlotIsSettable_ModelLevelJavaParity()
	{
		// Java parity: model/gameobjects/Item.setEquipmentSlot mutates slot in place.
		// ItemMoveService.moveInSameStorage calls item.setEquipmentSlot(slot) on the live item object.
		var item = new InventoryItem
		{
			ObjectId = 1001,
			ItemId = 100000,
			Count = 1,
			Location = 0,
			Slot = 5,
		};

		item.Slot = 12;

		Assert.Equal(12, item.Slot);
	}

	[Fact]
	public void CmMoveItem_SameSourceAndDestination_IdentifiesSameStorageMove()
	{
		// Java parity: CM_MOVE_ITEM.runImpl -> ItemMoveService.moveItem checks sourceStorageType == destinationStorageType.
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteD(999);
		buffer.WriteC(0); // source = cube
		buffer.WriteC(0); // destination = cube (same storage)
		buffer.WriteH(7);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0, packet.Source);
		Assert.Equal(0, packet.Destination);
		Assert.True(packet.Source == packet.Destination); // same-storage indicator
		Assert.Equal(7, packet.Slot);
	}

	private static CmMoveItem CreatePacket()
	{
		return new CmMoveItem(156, new HashSet<GameConnectionState> { GameConnectionState.InGame });
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writeBody)
	{
		using var body = new PacketBuffer();
		writeBody(body);
		var bodyBytes = body.ToArray();

		var encodedOpcode = EncodeClientPacketOpcode(opcode);
		using var payload = new PacketBuffer(5 + bodyBytes.Length);
		payload.WriteH(encodedOpcode);
		payload.WriteC(0x65);
		payload.WriteH((ushort)~encodedOpcode);
		payload.WriteB(bodyBytes);
		return payload.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}
}
