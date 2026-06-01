using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmSplitItemTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaSplitItemOpcodeAsInGameOnly()
	{
		Assert.IsType<CmSplitItem>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(157, buffer =>
				{
					buffer.WriteD(7001);
					buffer.WriteQ(2);
					buffer.WriteC(0);
					buffer.WriteD(0);
					buffer.WriteC(0);
					buffer.WriteH(0);
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(157, buffer =>
			{
				buffer.WriteD(7001);
				buffer.WriteQ(2);
				buffer.WriteC(0);
				buffer.WriteD(0);
				buffer.WriteC(0);
				buffer.WriteH(0);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_HighBitSlotNumberReadsAsSignedShort()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteD(7001);
		buffer.WriteQ(123);
		buffer.WriteC(0);
		buffer.WriteD(8002);
		buffer.WriteC(3);
		buffer.WriteH(0xffff);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(7001, packet.SourceItemObjectId);
		Assert.Equal(123, packet.ItemAmount);
		Assert.Equal(0, packet.SourceStorageType);
		Assert.Equal(8002, packet.DestinationItemObjectId);
		Assert.Equal(3, packet.DestinationStorageType);
		Assert.Equal((short)-1, packet.SlotNumber);
	}

	private static CmSplitItem CreatePacket()
	{
		return new CmSplitItem(157, new HashSet<GameConnectionState> { GameConnectionState.InGame });
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
