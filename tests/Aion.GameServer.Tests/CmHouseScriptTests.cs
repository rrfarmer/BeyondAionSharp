using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmHouseScriptTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaHouseScriptOpcodeAsInGameOnly()
	{
		Assert.IsType<CmHouseScript>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(30, buffer =>
				{
					buffer.WriteD(12345);
					buffer.WriteC(7);
					buffer.WriteH(0);
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(30, buffer =>
			{
				buffer.WriteD(12345);
				buffer.WriteC(7);
				buffer.WriteH(0);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_ValidCompressedScriptReadsPayloadLikeJava()
	{
		var packet = CreatePacket();
		using var writeBuffer = new PacketBuffer();
		writeBuffer.WriteD(12345);
		writeBuffer.WriteC(255);
		writeBuffer.WriteH(11);
		writeBuffer.WriteD(3);
		writeBuffer.WriteD(9);
		writeBuffer.WriteB([0x01, 0x02, 0x03]);

		var readBuffer = new PacketBuffer(writeBuffer.ToArray());
		packet.ReadFrom(readBuffer);

		Assert.Equal(12345, packet.Address);
		Assert.Equal(255, packet.ScriptId);
		Assert.Equal(11, packet.TotalSize);
		Assert.Equal(3, packet.CompressedSize);
		Assert.Equal(9, packet.UncompressedSize);
		Assert.Equal([0x01, 0x02, 0x03], packet.ScriptContent);
		Assert.Equal(0, readBuffer.Remaining);
	}

	[Fact]
	public void ReadFrom_OversizedCompressedScriptStopsBeforeUncompressedSizeLikeJava()
	{
		var packet = CreatePacket();
		using var writeBuffer = new PacketBuffer();
		writeBuffer.WriteD(12345);
		writeBuffer.WriteC(7);
		writeBuffer.WriteH(11);
		writeBuffer.WriteD(CmHouseScript.MaxCompressedScriptSize + 1);
		writeBuffer.WriteD(9);
		writeBuffer.WriteB([0x01, 0x02, 0x03]);

		var readBuffer = new PacketBuffer(writeBuffer.ToArray());
		packet.ReadFrom(readBuffer);

		Assert.Equal(CmHouseScript.MaxCompressedScriptSize + 1, packet.CompressedSize);
		Assert.Equal(0, packet.UncompressedSize);
		Assert.Empty(packet.ScriptContent);
		Assert.Equal(7, readBuffer.Remaining);
	}

	private static CmHouseScript CreatePacket()
	{
		return new CmHouseScript(30, new HashSet<GameConnectionState> { GameConnectionState.InGame });
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
