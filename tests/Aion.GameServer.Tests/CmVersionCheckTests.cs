using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmVersionCheckTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaVersionCheckOpcodeAsConnectedOnly()
	{
		Assert.IsType<CmVersionCheck>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(0, buffer => WriteVersionCheckPayload(buffer)),
				GameConnectionState.Connected));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(0, buffer => WriteVersionCheckPayload(buffer)),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_ReadsUnsignedVersionsAndLiteInfoLikeJava()
	{
		var packet = CreatePacket();
		using var writeBuffer = new PacketBuffer();
		WriteVersionCheckPayload(writeBuffer);

		var readBuffer = new PacketBuffer(writeBuffer.ToArray());
		packet.ReadFrom(readBuffer);

		Assert.Equal(0xffff, packet.AionClientVersion);
		Assert.Equal(0x8001, packet.NpcScriptInterfaceVersion);
		Assert.Equal(65001, packet.WindowsEncoding);
		Assert.Equal(10, packet.WindowsVersion);
		Assert.Equal(19045, packet.WindowsSubVersion);
		Assert.Equal(2, packet.LiteInfo);
		Assert.Equal(0, readBuffer.Remaining);
	}

	private static CmVersionCheck CreatePacket()
	{
		return new CmVersionCheck(0, new HashSet<GameConnectionState> { GameConnectionState.Connected });
	}

	private static void WriteVersionCheckPayload(PacketBuffer buffer)
	{
		buffer.WriteH(0xffff);
		buffer.WriteH(0x8001);
		buffer.WriteD(65001);
		buffer.WriteD(10);
		buffer.WriteD(19045);
		buffer.WriteC(2);
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
