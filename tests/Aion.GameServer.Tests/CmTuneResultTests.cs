using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmTuneResultTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaAnswerReidentifyOpcodeAsInGameOnly()
	{
		var packet = Assert.IsType<CmTuneResult>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(238, buffer =>
				{
					buffer.WriteD(1001);
					buffer.WriteC(1);
				}),
				GameConnectionState.InGame));

		Assert.Equal(238, packet.OpCode);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(238, buffer =>
			{
				buffer.WriteD(1001);
				buffer.WriteC(1);
			}),
			GameConnectionState.Authed));
	}

	[Theory]
	[InlineData(1, true)]
	[InlineData(0, false)]
	[InlineData(2, false)]
	public void ReadFrom_ReadsItemObjectIdAndAcceptFlagLikeJava(int acceptedByte, bool expectedAccepted)
	{
		var packet = new CmTuneResult(238, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(1001);
		buffer.WriteC(acceptedByte);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(1001, packet.ItemObjectId);
		Assert.Equal(expectedAccepted, packet.HasAccepted);
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		return buffer.ToArray();
	}
}
