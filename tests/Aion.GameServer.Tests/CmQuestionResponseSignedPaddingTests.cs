using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmQuestionResponseSignedPaddingTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaQuestionResponseOpcodeAsInGameOnly()
	{
		Assert.IsType<CmQuestionResponse>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(50, buffer =>
				{
					buffer.WriteD(900001);
					buffer.WriteC(1);
					buffer.WriteC(0);
					buffer.WriteH(0xffff);
					buffer.WriteD(7001);
					buffer.WriteD(0);
					buffer.WriteH(0x8000);
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(50, buffer =>
			{
				buffer.WriteD(900001);
				buffer.WriteC(1);
				buffer.WriteC(0);
				buffer.WriteH(0xffff);
				buffer.WriteD(7001);
				buffer.WriteD(0);
				buffer.WriteH(0x8000);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_HighBitPaddingDoesNotShiftMeaningfulFields()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteD(900001);
		buffer.WriteC(1);
		buffer.WriteC(0x7f);
		buffer.WriteH(0xffff);
		buffer.WriteD(7001);
		buffer.WriteD(8002);
		buffer.WriteH(0x8000);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(900001, packet.QuestionId);
		Assert.Equal(1, packet.Response);
		Assert.Equal(7001, packet.SenderObjectId);
	}

	private static CmQuestionResponse CreatePacket()
	{
		return new CmQuestionResponse(50, new HashSet<GameConnectionState> { GameConnectionState.InGame });
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
