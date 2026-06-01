using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmLegionTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaLegionOpcodeAsInGameOnly()
	{
		Assert.IsType<CmLegion>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(45, buffer => buffer.WriteC(0x0D)),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(45, buffer => buffer.WriteC(0x0D)),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_EditPermissionsReadsSignedShorts()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x0D);
		buffer.WriteH(0xffff);
		buffer.WriteH(0x8000);
		buffer.WriteH(0x7fff);
		buffer.WriteH(1);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0x0D, packet.ExOpcode);
		Assert.Equal((short)-1, packet.DeputyPermission);
		Assert.Equal(short.MinValue, packet.CenturionPermission);
		Assert.Equal(short.MaxValue, packet.LegionaryPermission);
		Assert.Equal((short)1, packet.VolunteerPermission);
	}

	[Fact]
	public void ReadFrom_RankBranchConsumesRankAndCharacterName()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x06);
		buffer.WriteD(3);
		buffer.WriteS("Lurion");

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0x06, packet.ExOpcode);
		Assert.Equal(3, packet.Rank);
		Assert.Equal("Lurion", packet.CharacterName);
	}

	private static CmLegion CreatePacket()
	{
		return new CmLegion(45, new HashSet<GameConnectionState> { GameConnectionState.InGame });
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
