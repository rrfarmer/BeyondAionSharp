using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmAtreianPassportTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaAtreianPassportOpcodeAsInGameOnly()
	{
		Assert.IsType<CmAtreianPassport>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(248, buffer => buffer.WriteH(0)),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(248, buffer => buffer.WriteH(0)),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_SentinelCountConsumesCompletePassportPairsUntilTrailingBytes()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(0xffff);
		buffer.WriteD(1001);
		buffer.WriteD(1717200000);
		buffer.WriteD(1001);
		buffer.WriteD(1717286400);
		buffer.WriteD(9999);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(-1, packet.Count);
		var timestamps = Assert.Single(packet.Passports);
		Assert.Equal(1001, timestamps.Key);
		Assert.True(timestamps.Value.SetEquals([1717200000, 1717286400]));
	}

	[Fact]
	public void ReadFrom_PositiveCountConsumesOnlyDeclaredPassportPairs()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(1);
		buffer.WriteD(1001);
		buffer.WriteD(1717200000);
		buffer.WriteD(2002);
		buffer.WriteD(1717286400);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(1, packet.Count);
		var timestamps = Assert.Single(packet.Passports);
		Assert.Equal(1001, timestamps.Key);
		Assert.True(timestamps.Value.SetEquals([1717200000]));
	}

	private static CmAtreianPassport CreatePacket()
	{
		return new CmAtreianPassport(248, new HashSet<GameConnectionState> { GameConnectionState.InGame });
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
