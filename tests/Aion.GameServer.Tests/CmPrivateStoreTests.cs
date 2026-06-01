using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmPrivateStoreTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaPrivateStoreOpcodesAsInGameOnly()
	{
		var storePacket = Assert.IsType<CmPrivateStore>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(119, buffer => buffer.WriteH(0)),
				GameConnectionState.InGame));

		var namePacket = Assert.IsType<CmPrivateStoreName>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(120, buffer => buffer.WriteS("For Atreia")),
				GameConnectionState.InGame));

		Assert.Equal(119, storePacket.OpCode);
		Assert.Equal(120, namePacket.OpCode);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(119, buffer => buffer.WriteH(0)),
			GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(120, buffer => buffer.WriteS("For Atreia")),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_PrivateStoreReadsItemsInJavaFieldOrder()
	{
		var packet = CreateStorePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(2);
		buffer.WriteD(3001);
		buffer.WriteD(100000001);
		buffer.WriteH(1);
		buffer.WriteQ(10_000);
		buffer.WriteD(3002);
		buffer.WriteD(182003001);
		buffer.WriteH(0xFFFF);
		buffer.WriteQ(9_999_999_999L);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(2, packet.ItemCount);
		Assert.Equal(
			[
				new CmPrivateStoreEntry(3001, 100000001, 1, 10_000),
				new CmPrivateStoreEntry(3002, 182003001, 65535, 9_999_999_999L)
			],
			packet.Items);
	}

	[Fact]
	public void ReadFrom_PrivateStoreZeroItemsRepresentsJavaCloseStoreBranch()
	{
		var packet = CreateStorePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(0);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0, packet.ItemCount);
		Assert.Empty(packet.Items);
	}

	[Fact]
	public void ReadFrom_PrivateStoreNameReadsJavaString()
	{
		var packet = CreateStoreNamePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteS("For Atreia");

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal("For Atreia", packet.StoreName);
	}

	private static CmPrivateStore CreateStorePacket() =>
		new(119, new HashSet<GameConnectionState> { GameConnectionState.InGame });

	private static CmPrivateStoreName CreateStoreNamePacket() =>
		new(120, new HashSet<GameConnectionState> { GameConnectionState.InGame });

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
