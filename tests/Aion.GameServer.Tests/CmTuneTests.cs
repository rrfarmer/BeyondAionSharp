using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmTuneTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaIdentifyItemOpcodeAsInGameOnly()
	{
		var packet = Assert.IsType<CmTune>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(235, buffer =>
				{
					buffer.WriteD(1001);
					buffer.WriteD(2002);
				}),
				GameConnectionState.InGame));

		Assert.Equal(235, packet.OpCode);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(235, buffer =>
			{
				buffer.WriteD(1001);
				buffer.WriteD(2002);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_ReadsTargetItemAndTuningScrollObjectIdsLikeJava()
	{
		var packet = new CmTune(235, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(1001);
		buffer.WriteD(2002);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(1001, packet.ItemObjectId);
		Assert.Equal(2002, packet.TuningScrollObjectId);
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
