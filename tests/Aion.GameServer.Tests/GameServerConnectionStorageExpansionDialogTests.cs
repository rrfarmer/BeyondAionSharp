using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionStorageExpansionDialogTests
{
	[Theory]
	[InlineData(CmDialogSelect.ExtendInventory, InventoryExpansionStorage.Cube, 1000)]
	[InlineData(CmDialogSelect.ExtendCharWarehouse, InventoryExpansionStorage.Warehouse, 1200)]
	public async Task HandleDialogSelectAsync_StorageExpansionActionsRegisterJavaWarningQuestion(
		int dialogActionId,
		InventoryExpansionStorage expectedStorage,
		int expectedPrice)
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync();
		var player = CreatePlayer(targetObjectId: 9001);
		var npc = CreateExpansionNpc(9001, templateId: expectedStorage == InventoryExpansionStorage.Cube ? 798008 : 203199, dialogActionId);
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(player, CreateDialogSelect(npc.ObjectId, dialogActionId));

		Assert.NotNull(player.PendingStorageExpansionRequest);
		Assert.Equal(expectedStorage, player.PendingStorageExpansionRequest!.Storage);
		Assert.Equal(npc.ObjectId, player.PendingStorageExpansionRequest.NpcObjectId);
		Assert.Equal(expectedPrice, player.PendingStorageExpansionRequest.Price);
		Assert.Equal(SmQuestionWindow.WarehouseExpandWarning, player.PendingStorageExpansionRequest.QuestionId);
		Assert.Equal(1, player.ResponseRequester.Count);
		var question = Assert.IsType<SmQuestionWindow>(Assert.Single(fixture.SentPackets));
		Assert.Equal(SmQuestionWindow.WarehouseExpandWarning, question.Code);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_StorageExpansionRejectsUnsupportedNpcAction()
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync();
		var player = CreatePlayer(targetObjectId: 9001);
		var npc = CreateExpansionNpc(9001, templateId: 798008, CmDialogSelect.CombineTask);
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(
			player,
			CreateDialogSelect(npc.ObjectId, CmDialogSelect.ExtendInventory));

		Assert.Null(player.PendingStorageExpansionRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
		Assert.Empty(fixture.SentPackets);
	}

	private static Player CreatePlayer(int targetObjectId)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "ExpansionTester",
			TargetObjectId = targetObjectId,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
		};
	}

	private static WorldNpc CreateExpansionNpc(int objectId, int templateId, int dialogActionId)
	{
		var template = new NpcTemplateSummary(
			templateId,
			"Expansion Master",
			123456,
			1,
			"NORMAL",
			"NORMAL",
			"PC_ALL",
			string.Empty,
			"NPC",
			FunctionDialogIds: [dialogActionId],
			HasTalkInfo: true,
			IsDialogNpc: true);
		return new WorldNpc(objectId, templateId, template, new WorldPosition(210010000, 1, 2, 3, 0));
	}

	private static CmDialogSelect CreateDialogSelect(int targetObjectId, int dialogActionId)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(targetObjectId);
		writer.WriteH(dialogActionId);
		writer.WriteH(0);
		writer.WriteH(0);
		writer.WriteD(0);
		writer.WriteH(0);
		var packet = new CmDialogSelect(56, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private sealed class StorageExpansionDialogFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly GameServerConnection _connection;
		private readonly string _tempRoot;

		private StorageExpansionDialogFixture(
			TcpClient client,
			GameServerConnection connection,
			GameWorld world,
			List<GameServerPacket> sentPackets,
			string tempRoot)
		{
			_client = client;
			_connection = connection;
			World = world;
			SentPackets = sentPackets;
			_tempRoot = tempRoot;
		}

		public GameServerConnection Connection => _connection;

		public GameWorld World { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<StorageExpansionDialogFixture> CreateAsync()
		{
			var tempRoot = Path.Combine(Path.GetTempPath(), "aion-storage-dialog-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path.Combine(tempRoot, "game-server", "data", "static_data"));
			await File.WriteAllTextAsync(
				Path.Combine(tempRoot, "game-server", "data", "static_data", "static_data.xml"),
				"""
				<?xml version="1.0" encoding="UTF-8"?>
				<static_data>
					<cube_expander>
						<expansion_npc ids="798008">
							<expand level="1" price="1000" />
						</expansion_npc>
					</cube_expander>
					<warehouse_expander>
						<expansion_npc ids="203199">
							<expand level="1" price="1200" />
						</expansion_npc>
					</warehouse_expander>
				</static_data>
				""");
			var dataManager = await DataManager.LoadAsync(
				tempRoot,
				cacheDirectory: Path.Combine(tempRoot, "cache"),
				validateWhenCacheChanges: false);
			var runtimeContext = new GameServerRuntimeContext();
			runtimeContext.SetDataManager(dataManager);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			world.Initialize();
			var sentPackets = new List<GameServerPacket>();

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
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"storage-expansion-dialog-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					runtimeContext: runtimeContext,
					world: world,
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new StorageExpansionDialogFixture(client, connection, world, sentPackets, tempRoot);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await _connection.DisposeAsync();
			_client.Dispose();
			if (Directory.Exists(_tempRoot))
				Directory.Delete(_tempRoot, recursive: true);
		}
	}
}
