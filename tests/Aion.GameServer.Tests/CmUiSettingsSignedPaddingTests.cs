using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmUiSettingsSignedPaddingTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaUiSettingsOpcodeAsInGameOnly()
	{
		Assert.IsType<CmUiSettings>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(10, buffer =>
				{
					buffer.WriteC(1);
					buffer.WriteH(0xffff);
					buffer.WriteH(0x8000);
					buffer.WriteB([0xaa, 0xbb]);
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(10, buffer =>
			{
				buffer.WriteC(1);
				buffer.WriteH(0xffff);
				buffer.WriteH(0x8000);
				buffer.WriteB([0xaa, 0xbb]);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_HighBitPaddingDoesNotShiftDeclaredSizeOrData()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(2);
		buffer.WriteH(0xffff);
		buffer.WriteH(0x8000);
		buffer.WriteB([0x10, 0x20, 0x30]);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(2, packet.SettingsType);
		Assert.Equal(0x8000, packet.DeclaredSize);
		Assert.Equal([0x10, 0x20, 0x30], packet.Data);
	}

	private static CmUiSettings CreatePacket()
	{
		return new CmUiSettings(10, new HashSet<GameConnectionState> { GameConnectionState.InGame });
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
