using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmAppearanceSignedPaddingTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaAppearanceOpcodeAsInGameOnly()
	{
		Assert.IsType<CmAppearance>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(197, buffer =>
				{
					buffer.WriteC(2);
					buffer.WriteC(0);
					buffer.WriteH(0xffff);
					buffer.WriteD(9001);
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(197, buffer =>
			{
				buffer.WriteC(2);
				buffer.WriteC(0);
				buffer.WriteH(0xffff);
				buffer.WriteD(9001);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_HighBitPaddingDoesNotShiftRenameFields()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0);
		buffer.WriteC(0x7f);
		buffer.WriteH(0xffff);
		buffer.WriteD(9001);
		buffer.WriteS("Newname");

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0, packet.Type);
		Assert.Equal(9001, packet.ItemObjectId);
		Assert.Equal("Newname", packet.NewName);
	}

	private static CmAppearance CreatePacket()
	{
		return new CmAppearance(197, new HashSet<GameConnectionState> { GameConnectionState.InGame });
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
