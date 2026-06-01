using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmRemoveAlteredStateTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaRemoveAlteredStateOpcodeAsInGameOnly()
	{
		Assert.IsType<CmRemoveAlteredState>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(35, buffer =>
				{
					buffer.WriteH(0xffff);
					buffer.WriteC(0x80);
					buffer.WriteC(0x01);
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(35, buffer =>
			{
				buffer.WriteH(0xffff);
				buffer.WriteC(0x80);
				buffer.WriteC(0x01);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_HighBitSkillIdReadsAsJavaUnsignedShortAndKeepsTrailingBytes()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(0xffff);
		buffer.WriteC(0x80);
		buffer.WriteC(0x01);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(65535, packet.SkillId);
		Assert.Equal(0x80, packet.Unknown1);
		Assert.Equal(0x01, packet.Unknown2);
	}

	private static CmRemoveAlteredState CreatePacket()
	{
		return new CmRemoveAlteredState(35, new HashSet<GameConnectionState> { GameConnectionState.InGame });
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
