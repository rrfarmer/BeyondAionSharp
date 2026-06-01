using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmTeleportSelectSignedPaddingTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaTeleportSelectOpcodeAsInGameOnly()
	{
		Assert.IsType<CmTeleportSelect>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(148, buffer =>
				{
					buffer.WriteD(0x01020304);
					buffer.WriteD(0x05060708);
					buffer.WriteH(0xffff);
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(148, buffer =>
			{
				buffer.WriteD(0x01020304);
				buffer.WriteD(0x05060708);
				buffer.WriteH(0xffff);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_HighBitPaddingDoesNotShiftTeleportFields()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteD(0x01020304);
		buffer.WriteD(0x05060708);
		buffer.WriteH(0xffff);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0x01020304, packet.TargetObjectId);
		Assert.Equal(0x05060708, packet.LocationId);
	}

	private static CmTeleportSelect CreatePacket()
	{
		return new CmTeleportSelect(148, new HashSet<GameConnectionState> { GameConnectionState.InGame });
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
