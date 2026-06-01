using Aion.Commons.Network;
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
