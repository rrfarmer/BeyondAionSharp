using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class CmHouseScriptTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaHouseScriptOpcodeAsInGameOnly()
	{
		Assert.IsType<CmHouseScript>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(30, buffer =>
				{
					buffer.WriteD(12345);
					buffer.WriteC(7);
					buffer.WriteH(0);
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(30, buffer =>
			{
				buffer.WriteD(12345);
				buffer.WriteC(7);
				buffer.WriteH(0);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_ValidCompressedScriptReadsPayloadLikeJava()
	{
		var packet = CreatePacket();
		using var writeBuffer = new PacketBuffer();
		writeBuffer.WriteD(12345);
		writeBuffer.WriteC(255);
		writeBuffer.WriteH(11);
		writeBuffer.WriteD(3);
		writeBuffer.WriteD(9);
		writeBuffer.WriteB([0x01, 0x02, 0x03]);

		var readBuffer = new PacketBuffer(writeBuffer.ToArray());
		packet.ReadFrom(readBuffer);

		Assert.Equal(12345, packet.Address);
		Assert.Equal(255, packet.ScriptId);
		Assert.Equal(11, packet.TotalSize);
		Assert.Equal(3, packet.CompressedSize);
		Assert.Equal(9, packet.UncompressedSize);
		Assert.Equal([0x01, 0x02, 0x03], packet.ScriptContent);
		Assert.Equal(0, readBuffer.Remaining);
	}

	[Fact]
	public void ReadFrom_OversizedCompressedScriptStopsBeforeUncompressedSizeLikeJava()
	{
		var packet = CreatePacket();
		using var writeBuffer = new PacketBuffer();
		writeBuffer.WriteD(12345);
		writeBuffer.WriteC(7);
		writeBuffer.WriteH(11);
		writeBuffer.WriteD(CmHouseScript.MaxCompressedScriptSize + 1);
		writeBuffer.WriteD(9);
		writeBuffer.WriteB([0x01, 0x02, 0x03]);

		var readBuffer = new PacketBuffer(writeBuffer.ToArray());
		packet.ReadFrom(readBuffer);

		Assert.Equal(CmHouseScript.MaxCompressedScriptSize + 1, packet.CompressedSize);
		Assert.Equal(0, packet.UncompressedSize);
		Assert.Empty(packet.ScriptContent);
		Assert.Equal(7, readBuffer.Remaining);
	}

	[Fact]
	public async Task ProcessPacketAsync_OversizedScriptSendsJavaOverflowSystemMessage()
	{
		await using var fixture = await GameServerConnectionBuyItemTests.BuyItemFixture.CreateAsync();
		GameServerConnectionBuyItemTests.SetActivePlayerForPacketDispatchForAdapterTests(
			fixture.Connection,
			new Player
			{
				ObjectId = 1001,
				Name = "ScriptTester",
				Race = "ELYOS",
				PlayerClass = "RANGER",
				Position = new WorldPosition(210010000, 0, 0, 0, 0),
			});

		await GameServerConnectionBuyItemTests.InvokeProcessPacketAsyncForAdapterTests(
			fixture.Connection,
			CreateClientPayload(30, buffer =>
			{
				buffer.WriteD(700001);
				buffer.WriteC(7);
				buffer.WriteH(11);
				buffer.WriteD(CmHouseScript.MaxCompressedScriptSize + 1);
				buffer.WriteD(9);
				buffer.WriteB([0x01, 0x02, 0x03]);
			}));

		Assert.Equal(1401399, Assert.IsType<SmSystemMessage>(Assert.Single(fixture.SentPackets)).MessageId);
	}

	private static CmHouseScript CreatePacket()
	{
		return new CmHouseScript(30, new HashSet<GameConnectionState> { GameConnectionState.InGame });
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
