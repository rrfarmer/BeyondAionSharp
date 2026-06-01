using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmToggleSkillDeactivateSignedPaddingTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaToggleSkillDeactivateOpcodeAsInGameOnly()
	{
		Assert.IsType<CmToggleSkillDeactivate>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(34, buffer =>
				{
					buffer.WriteH(0xabcd);
					buffer.WriteH(0xffff);
					buffer.WriteH(0x8000);
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(34, buffer =>
			{
				buffer.WriteH(0xabcd);
				buffer.WriteH(0xffff);
				buffer.WriteH(0x8000);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_HighBitPaddingDoesNotShiftUnsignedSkillId()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(0xabcd);
		buffer.WriteH(0xffff);
		buffer.WriteH(0x8000);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0xabcd, packet.SkillId);
	}

	private static CmToggleSkillDeactivate CreatePacket()
	{
		return new CmToggleSkillDeactivate(34, new HashSet<GameConnectionState> { GameConnectionState.InGame });
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
