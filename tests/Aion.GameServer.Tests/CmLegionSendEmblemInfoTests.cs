using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class CmLegionSendEmblemInfoTests
{
	[Fact]
	public void ClientPacketFactory_ParsesLegionSendEmblemInfoAsInGameOnly()
	{
		Assert.IsType<CmLegionSendEmblemInfo>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(16, buffer => buffer.WriteD(77)), GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(16, buffer => buffer.WriteD(77)),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_ParsesLegionSendEmblemAsInGameOnly()
	{
		Assert.IsType<CmLegionSendEmblem>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(47, buffer => buffer.WriteD(77)), GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(47, buffer => buffer.WriteD(77)),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_ParsesLegionModifyEmblemLikeJava()
	{
		var packet = Assert.IsType<CmLegionModifyEmblem>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(59, buffer =>
				{
					buffer.WriteD(77);
					buffer.WriteC(12);
					buffer.WriteC(0x80);
					buffer.WriteC(200);
					buffer.WriteC(21);
					buffer.WriteC(22);
					buffer.WriteC(23);
				}),
				GameConnectionState.InGame));

		Assert.Equal(77, packet.LegionId);
		Assert.Equal(12, packet.EmblemId);
		Assert.Equal(0x80, packet.EmblemType);
		Assert.Equal(200, packet.Alpha);
		Assert.Equal(21, packet.Red);
		Assert.Equal(22, packet.Green);
		Assert.Equal(23, packet.Blue);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(59, buffer => buffer.WriteD(77)),
			GameConnectionState.Authed));
	}

	[Fact]
	public void SmLegionSendEmblem_WritesJavaEmblemInfoPayload()
	{
		var packet = new SmLegionSendEmblem(
			legionId: 77,
			emblemId: 6,
			emblemType: 0x80,
			emblemDataSize: 0,
			colorA: 255,
			colorR: 10,
			colorG: 20,
			colorB: 30,
			legionName: "Hydrated Legion");

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(77, reader.ReadD());
		Assert.Equal(6, reader.ReadC());
		Assert.Equal(0x80, reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(255, reader.ReadC());
		Assert.Equal(10, reader.ReadC());
		Assert.Equal(20, reader.ReadC());
		Assert.Equal(30, reader.ReadC());
		Assert.Equal("Hydrated Legion", reader.ReadS());
		Assert.Equal(0x01, reader.ReadC());
	}

	[Fact]
	public void SmLegionSendEmblemData_WritesJavaChunkPayload()
	{
		var packet = new SmLegionSendEmblemData(3, [0x10, 0x20, 0x30]);

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(3, reader.ReadD());
		Assert.Equal([0x10, 0x20, 0x30], reader.ReadB(3));
	}

	[Fact]
	public void SmLegionUpdateEmblem_WritesJavaPayload()
	{
		var packet = new SmLegionUpdateEmblem(
			legionId: 77,
			emblemId: 12,
			emblemType: 0,
			colorA: 200,
			colorR: 21,
			colorG: 22,
			colorB: 23);

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(77, reader.ReadD());
		Assert.Equal(12, reader.ReadC());
		Assert.Equal(0, reader.ReadC());
		Assert.Equal(200, reader.ReadC());
		Assert.Equal(21, reader.ReadC());
		Assert.Equal(22, reader.ReadC());
		Assert.Equal(23, reader.ReadC());
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionSendEmblemInfoSendsActivePlayerLegionEmblemLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		var player = CreateLegionPlayer();
		SetActivePlayer(pair.Connection, player);

		var packet = CreateInfoPacket(player.LegionId);
		await InvokeHandleInfrastructurePacketAsync(pair.Connection, packet);

		var response = Assert.IsType<SmLegionSendEmblem>(Assert.Single(pair.SentPackets));
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(player.LegionId, reader.ReadD());
		Assert.Equal(player.LegionEmblemId, reader.ReadC());
		Assert.Equal(player.LegionEmblemType, reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(player.LegionEmblemColorA, reader.ReadC());
		Assert.Equal(player.LegionEmblemColorR, reader.ReadC());
		Assert.Equal(player.LegionEmblemColorG, reader.ReadC());
		Assert.Equal(player.LegionEmblemColorB, reader.ReadC());
		Assert.Equal(player.LegionName, reader.ReadS());
		Assert.Equal(0x01, reader.ReadC());
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionSendEmblemInfoUsesRepositoryForOtherLegionLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionEmblem = CreateRepositorySnapshot(88, customData: [0x44]),
		};
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		SetActivePlayer(pair.Connection, CreateLegionPlayer());

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateInfoPacket(88));

		Assert.Equal(1, repository.LoadLegionEmblemCalls);
		Assert.Equal(88, repository.LoadedLegionEmblemRequest);
		var response = Assert.IsType<SmLegionSendEmblem>(Assert.Single(pair.SentPackets));
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(88, reader.ReadD());
		Assert.Equal(9, reader.ReadC());
		Assert.Equal(0x80, reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(200, reader.ReadC());
		Assert.Equal(21, reader.ReadC());
		Assert.Equal(22, reader.ReadC());
		Assert.Equal(23, reader.ReadC());
		Assert.Equal("Repository Legion", reader.ReadS());
		Assert.Equal(0x01, reader.ReadC());
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionSendEmblemSendsCustomDataChunksLikeJava()
	{
		var customData = Enumerable.Range(0, 8000).Select(index => (byte)(index % 251)).ToArray();
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionEmblem = CreateRepositorySnapshot(88, customData),
		};
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		SetActivePlayer(pair.Connection, CreateLegionPlayer());

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateDataPacket(88));

		Assert.Equal(1, repository.LoadLegionEmblemCalls);
		Assert.Collection(
			pair.SentPackets,
			packet =>
			{
				var header = Assert.IsType<SmLegionSendEmblem>(packet);
				using var reader = new PacketBuffer(SerializeUnencryptedPayload(header));
				Assert.Equal(88, reader.ReadD());
				reader.ReadC();
				reader.ReadC();
				Assert.Equal(customData.Length, reader.ReadD());
			},
			packet =>
			{
				var chunk = Assert.IsType<SmLegionSendEmblemData>(packet);
				using var reader = new PacketBuffer(SerializeUnencryptedPayload(chunk));
				Assert.Equal(7993, reader.ReadD());
				Assert.Equal(customData.Take(7993).ToArray(), reader.ReadB(7993));
			},
			packet =>
			{
				var chunk = Assert.IsType<SmLegionSendEmblemData>(packet);
				using var reader = new PacketBuffer(SerializeUnencryptedPayload(chunk));
				Assert.Equal(7, reader.ReadD());
				Assert.Equal(customData.Skip(7993).ToArray(), reader.ReadB(7));
			});
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionModifyEmblemMutatesPersistsAndSendsUpdateLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository, CreateOptions(emblemRequiredKinah: 100));
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.BrigadeGeneral;
		player.LegionLevel = 2;
		player.InventoryItems = [CreateKinahItem(count: 250)];
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateModifyPacket(
			player.LegionId,
			emblemId: 12,
			emblemType: 0x80,
			alpha: 200,
			red: 21,
			green: 22,
			blue: 23));

		Assert.Equal(150, player.InventoryItems.Single().Count);
		Assert.Equal(12, player.LegionEmblemId);
		Assert.Equal(0, player.LegionEmblemType);
		Assert.Equal(200, player.LegionEmblemColorA);
		Assert.Equal(21, player.LegionEmblemColorR);
		Assert.Equal(22, player.LegionEmblemColorG);
		Assert.Equal(23, player.LegionEmblemColorB);
		Assert.Equal(1, repository.SaveLegionEmblemMutationCalls);
		Assert.NotNull(repository.SavedLegionEmblemMutation);
		var saved = repository.SavedLegionEmblemMutation.Value;
		Assert.Equal(player.ObjectId, saved.PlayerObjectId);
		Assert.Equal(player.LegionId, saved.LegionId);
		Assert.Equal(12, saved.Emblem.EmblemId);
		Assert.Equal(0, saved.Emblem.EmblemType);
		Assert.Equal(150, saved.KinahItemUpdate?.Count);
		Assert.Equal(1, repository.InsertLegionHistoryCalls);
		Assert.Equal(LegionHistoryActions.EmblemModified, repository.InsertedLegionHistory?.ActionName);

		Assert.Collection(
			pair.SentPackets,
			packet =>
			{
				var update = Assert.IsType<SmLegionUpdateEmblem>(packet);
				using var reader = new PacketBuffer(SerializeUnencryptedPayload(update));
				Assert.Equal(player.LegionId, reader.ReadD());
				Assert.Equal(12, reader.ReadC());
				Assert.Equal(0, reader.ReadC());
				Assert.Equal(200, reader.ReadC());
				Assert.Equal(21, reader.ReadC());
				Assert.Equal(22, reader.ReadC());
				Assert.Equal(23, reader.ReadC());
			},
			packet => Assert.Equal(1390137, Assert.IsType<SmSystemMessage>(packet).MessageId));
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionModifyEmblemRejectsNonBrigadeGeneralLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository, CreateOptions(emblemRequiredKinah: 100));
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.Legionary;
		player.LegionLevel = 2;
		player.InventoryItems = [CreateKinahItem(count: 250)];
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateModifyPacket(player.LegionId, 12, 0, 200, 21, 22, 23));

		Assert.Equal(250, player.InventoryItems.Single().Count);
		Assert.Equal(0, repository.SaveLegionEmblemMutationCalls);
		var message = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1390136, message.MessageId);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionSendEmblemInfoSkipsUnknownLegionUntilRegistryExists()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		SetActivePlayer(pair.Connection, CreateLegionPlayer());

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateInfoPacket(999));

		Assert.Empty(pair.SentPackets);
	}

	private static Player CreateLegionPlayer()
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "Tester",
			Race = "ELYOS",
			LegionId = 77,
			LegionLevel = 1,
			LegionName = "Hydrated Legion",
			LegionEmblemId = 6,
			LegionEmblemType = 0x80,
			LegionEmblemColorA = 255,
			LegionEmblemColorR = 10,
			LegionEmblemColorG = 20,
			LegionEmblemColorB = 30,
		};
	}

	private static InventoryItem CreateKinahItem(long count)
	{
		return new InventoryItem
		{
			ObjectId = 9001,
			ItemId = 182400001,
			OwnerId = 1001,
			Location = 0,
			Count = count,
		};
	}

	private static GameServerOptions CreateOptions(int emblemRequiredKinah)
	{
		return new GameServerOptions
		{
			Legion = new GameServerLegionOptions
			{
				WarehouseEnabled = true,
				EmblemRequiredKinah = emblemRequiredKinah,
			},
		};
	}

	private static LegionEmblemSnapshot CreateRepositorySnapshot(int legionId, byte[] customData)
	{
		return new LegionEmblemSnapshot(
			legionId,
			"Repository Legion",
			EmblemId: 9,
			EmblemType: 0x80,
			ColorA: 200,
			ColorR: 21,
			ColorG: 22,
			ColorB: 23,
			customData);
	}

	private static CmLegionSendEmblemInfo CreateInfoPacket(int legionId)
	{
		var packet = new CmLegionSendEmblemInfo(16, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(legionId);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static CmLegionSendEmblem CreateDataPacket(int legionId)
	{
		var packet = new CmLegionSendEmblem(47, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(legionId);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static CmLegionModifyEmblem CreateModifyPacket(
		int legionId,
		byte emblemId,
		byte emblemType,
		byte alpha,
		byte red,
		byte green,
		byte blue)
	{
		var packet = new CmLegionModifyEmblem(59, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(legionId);
		buffer.WriteC(emblemId);
		buffer.WriteC(emblemType);
		buffer.WriteC(alpha);
		buffer.WriteC(red);
		buffer.WriteC(green);
		buffer.WriteC(blue);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = EncodeClientPacketOpcode(opcode);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		return buffer.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static async Task InvokeHandleInfrastructurePacketAsync(GameServerConnection connection, GameClientPacket packet)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleInfrastructurePacketAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = (Task)method.Invoke(connection, [packet])!;
		await task;
	}

	private static void SetActivePlayer(GameServerConnection connection, Player player)
	{
		var activePlayerField = typeof(GameServerConnection).GetField("_activePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(activePlayerField);
		activePlayerField.SetValue(connection, player);
	}

	private sealed class TestConnectionPair : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private TestConnectionPair(TcpClient client, GameServerConnection connection, List<GameServerPacket> sentPackets)
		{
			_client = client;
			Connection = connection;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection { get; }
		public List<GameServerPacket> SentPackets { get; }

		public static async Task<TestConnectionPair> CreateAsync(
			IPlayerEnterWorldRepository? playerEnterWorldRepository = null,
			GameServerOptions? options = null)
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try
			{
				var endpoint = (IPEndPoint)listener.LocalEndpoint;
				var client = new TcpClient();
				var acceptTask = listener.AcceptTcpClientAsync();
				await client.ConnectAsync(endpoint.Address, endpoint.Port);
				var serverClient = await acceptTask;
				var crypt = new GameCrypt(() => 0x01020304);
				crypt.EnableKey();
				var sentPackets = new List<GameServerPacket>();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"legion-emblem-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: options,
					playerEnterWorldRepository: playerEnterWorldRepository,
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new TestConnectionPair(client, connection, sentPackets);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await Connection.DisposeAsync();
			_client.Dispose();
		}
	}
}
