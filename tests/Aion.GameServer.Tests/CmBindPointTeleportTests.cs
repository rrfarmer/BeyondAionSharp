using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmBindPointTeleportTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaHotspotOpcodeAsInGameOnly()
	{
		var packet = Assert.IsType<CmBindPointTeleport>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(244, buffer => buffer.WriteC(2)), GameConnectionState.InGame));

		Assert.Equal(244, packet.OpCode);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(244, buffer => buffer.WriteC(2)), GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_ActionOneReadsLocIdAndKinahLikeJava()
	{
		var packet = new CmBindPointTeleport(244, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteC(1);
		buffer.WriteD(730001);
		buffer.WriteQ(1234567890123);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(1, packet.Action);
		Assert.Equal(730001, packet.LocId);
		Assert.Equal(1234567890123, packet.Kinah);
	}

	[Fact]
	public void ReadFrom_ActionTwoLeavesLocIdAndKinahAtJavaDefaults()
	{
		var packet = new CmBindPointTeleport(244, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteC(2);
		buffer.WriteD(999999);
		buffer.WriteQ(1234);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(2, packet.Action);
		Assert.Equal(0, packet.LocId);
		Assert.Equal(0, packet.Kinah);
	}

	[Fact]
	public void ReadFrom_UnknownActionReadsOnlyActionLikeJavaRunImplNoop()
	{
		var packet = new CmBindPointTeleport(244, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteC(3);
		buffer.WriteD(888888);
		buffer.WriteQ(5678);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(3, packet.Action);
		Assert.Equal(0, packet.LocId);
		Assert.Equal(0, packet.Kinah);
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
