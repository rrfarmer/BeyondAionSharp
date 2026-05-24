using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionInventoryExpansionUseItemTests
{
	[Fact]
	public async Task HandleUseItemAsync_CubeExpansionTicketConsumesItemAndRefreshesCubeSize()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 169630000);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(1, player.ItemExpands);
		Assert.Equal(36, InventoryCapacity.GetCubeLimit(player));
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(1, sourceItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => Assert.IsType<SmItemUsageAnimation>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmCubeUpdate>(packet));
	}

	[Fact]
	public async Task HandleUseItemAsync_WarehouseExpansionTicketConsumesItemAndRefreshesWarehouseInfo()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 169640000);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(1, player.WarehouseBonusExpands);
		Assert.Equal(32, InventoryCapacity.GetWarehouseLimit(player));
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(1, sourceItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => Assert.IsType<SmItemUsageAnimation>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmWarehouseInfo>(packet),
			packet => Assert.IsType<SmWarehouseInfo>(packet));
	}

	[Theory]
	[InlineData(169630000)]
	[InlineData(169640000)]
	public async Task HandleUseItemAsync_InventoryExpansionPersistenceFailureDoesNotMutateRuntimeState(int itemId)
	{
		var repository = new EmptyPlayerEnterWorldRepository { SaveInventoryExpansionMutationResult = false };
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(1, repository.SaveInventoryExpansionMutationCalls);
		Assert.Equal(0, player.ItemExpands);
		Assert.Equal(0, player.WarehouseBonusExpands);
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(2, sourceItem.Count);
		Assert.Empty(fixture.SentPackets);
	}

	private static Player CreatePlayer(int itemId)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "TicketUser",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 5001,
					ItemId = itemId,
					Count = 2,
					Location = 0,
				},
			],
		};
	}

	private static CmUseItem CreateUseItem(int sourceItemObjectId)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(sourceItemObjectId);
		writer.WriteC(0);
		var packet = new CmUseItem(37, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private sealed class InventoryExpansionUseItemFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly GameServerConnection _connection;
		private readonly string _tempRoot;

		private InventoryExpansionUseItemFixture(
			TcpClient client,
			GameServerConnection connection,
			List<GameServerPacket> sentPackets,
			string tempRoot)
		{
			_client = client;
			_connection = connection;
			SentPackets = sentPackets;
			_tempRoot = tempRoot;
		}

		public GameServerConnection Connection => _connection;

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<InventoryExpansionUseItemFixture> CreateAsync(
			EmptyPlayerEnterWorldRepository? repository = null)
		{
			var tempRoot = Path.Combine(Path.GetTempPath(), "aion-inventory-expansion-use-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path.Combine(tempRoot, "game-server", "data", "static_data"));
			await File.WriteAllTextAsync(
				Path.Combine(tempRoot, "game-server", "data", "static_data", "static_data.xml"),
				"""
				<?xml version="1.0" encoding="UTF-8"?>
				<static_data>
					<item_templates>
						<item_template id="169630000" name="[Expand Card] Expand Cube Ticket (lvl 1)" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<expandinventory level="1" storage="CUBE" />
							</actions>
						</item_template>
						<item_template id="169640000" name="[Expand Card] Expand Warehouse Ticket (lvl 1)" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<expandinventory level="1" storage="WAREHOUSE" />
							</actions>
						</item_template>
					</item_templates>
				</static_data>
				""");
			var dataManager = await DataManager.LoadAsync(
				tempRoot,
				cacheDirectory: Path.Combine(tempRoot, "cache"),
				validateWhenCacheChanges: false);
			var runtimeContext = new GameServerRuntimeContext();
			runtimeContext.SetDataManager(dataManager);
			var sentPackets = new List<GameServerPacket>();
			var world = new Aion.GameServer.World.World(NullLogger<Aion.GameServer.World.World>.Instance);
			world.Initialize();
			var playerEnterWorldService = repository == null
				? null
				: new PlayerEnterWorldService(
					new GameServerOptions(),
					repository,
					world,
					NullLogger<PlayerEnterWorldService>.Instance);

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
					"inventory-expansion-use-item-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					runtimeContext: runtimeContext,
					playerEnterWorldService: playerEnterWorldService,
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new InventoryExpansionUseItemFixture(client, connection, sentPackets, tempRoot);
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
