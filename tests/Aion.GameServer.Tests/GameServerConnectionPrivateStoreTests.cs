using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionPrivateStoreTests
{
	[Fact]
	public async Task ProcessPacketAsync_CmPrivateStoreCloseClearsStoreStateAndSendsCloseEmotion()
	{
		await using var fixture = await PrivateStoreFixture.CreateAsync();
		var player = CreatePlayer();
		player.ReplaceCreatureState(PlayerCreatureState.PrivateShop);
		player.PrivateStoreItems =
		[
			new PrivateStoreListedItemSummary(0, ItemObjectId: 3001, ItemId: 100000001, Count: 1, PricePerItem: 100, ItemName: "Practice Sword"),
		];
		GameServerConnectionBuyItemTests.SetActivePlayerForPacketDispatchForAdapterTests(fixture.Connection, player);

		await GameServerConnectionBuyItemTests.InvokeProcessPacketAsyncForAdapterTests(
			fixture.Connection,
			CreatePrivateStorePayload([]));

		Assert.Empty(player.PrivateStoreItems);
		Assert.False(player.IsInState(PlayerCreatureState.PrivateShop));
		Assert.True(player.IsInState(PlayerCreatureState.Active));
		Assert.Empty(fixture.CreatePlans);
		AssertClosePrivateShopEmotion(Assert.IsType<SmEmotion>(Assert.Single(fixture.SentPackets)), player.ObjectId);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmPrivateStoreNameRecordsDisabledOpenPlanWithoutSendingPackets()
	{
		await using var fixture = await PrivateStoreFixture.CreateAsync();
		var player = CreatePlayer();
		player.ReplaceCreatureState(PlayerCreatureState.PrivateShop);
		GameServerConnectionBuyItemTests.SetActivePlayerForPacketDispatchForAdapterTests(fixture.Connection, player);

		await GameServerConnectionBuyItemTests.InvokeProcessPacketAsyncForAdapterTests(
			fixture.Connection,
			CreatePrivateStoreNamePayload("For Atreia"));

		var plan = Assert.Single(fixture.NameOpenPlans);
		Assert.Equal(PrivateStoreNameOpenCompositionPlanStatus.OpenPlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.WouldSetStoreMessage);
		Assert.True(plan.WouldBroadcastStoreName);
		Assert.Equal("For Atreia", plan.OpenPlan!.StoreMessage);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmPrivateStoreNameMissingStoreRecordsPreconditionWithoutSendingPackets()
	{
		await using var fixture = await PrivateStoreFixture.CreateAsync();
		GameServerConnectionBuyItemTests.SetActivePlayerForPacketDispatchForAdapterTests(fixture.Connection, CreatePlayer());

		await GameServerConnectionBuyItemTests.InvokeProcessPacketAsyncForAdapterTests(
			fixture.Connection,
			CreatePrivateStoreNamePayload(string.Empty));

		var plan = Assert.Single(fixture.NameOpenPlans);
		Assert.Equal(PrivateStoreNameOpenCompositionPlanStatus.MissingStorePrecondition, plan.Status);
		Assert.Null(plan.OpenPlan);
		Assert.False(plan.WouldSetStoreMessage);
		Assert.False(plan.WouldBroadcastStoreName);
		Assert.Empty(fixture.SentPackets);
	}

	private static Player CreatePlayer() =>
		new()
		{
			ObjectId = 1001,
			Name = "PrivateStoreTester",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 1,
			IsOnline = true,
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};

	private static byte[] CreatePrivateStorePayload(IReadOnlyList<(int ItemObjectId, int ItemId, int Count, long Price)> items)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = EncodeClientPacketOpcode(119);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		buffer.WriteH(items.Count);
		foreach (var (itemObjectId, itemId, count, price) in items)
		{
			buffer.WriteD(itemObjectId);
			buffer.WriteD(itemId);
			buffer.WriteH(count);
			buffer.WriteQ(price);
		}

		return buffer.ToArray();
	}

	private static byte[] CreatePrivateStoreNamePayload(string storeName)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = EncodeClientPacketOpcode(120);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		buffer.WriteS(storeName);
		return buffer.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private static void AssertClosePrivateShopEmotion(SmEmotion packet, int expectedPlayerObjectId)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedPlayerObjectId, reader.ReadD());
		Assert.Equal((int)EmotionType.ClosePrivateShop, reader.ReadC());
		Assert.Equal((int)PlayerCreatureState.Active, reader.ReadH());
		Assert.Equal(0f, reader.ReadF());
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed class PrivateStoreFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private PrivateStoreFixture(
			TcpClient client,
			GameServerConnection connection,
			List<PrivateStoreCreatePlan> createPlans,
			List<PrivateStoreNameOpenCompositionPlan> nameOpenPlans,
			List<GameServerPacket> sentPackets)
		{
			_client = client;
			Connection = connection;
			CreatePlans = createPlans;
			NameOpenPlans = nameOpenPlans;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection { get; }

		public List<PrivateStoreCreatePlan> CreatePlans { get; }

		public List<PrivateStoreNameOpenCompositionPlan> NameOpenPlans { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<PrivateStoreFixture> CreateAsync()
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
				var world = new GameWorld(NullLogger<GameWorld>.Instance);
				world.Initialize();
				var createPlans = new List<PrivateStoreCreatePlan>();
				var nameOpenPlans = new List<PrivateStoreNameOpenCompositionPlan>();
				var sentPackets = new List<GameServerPacket>();
				return new PrivateStoreFixture(
					client,
					new GameServerConnection(
						NullLogger.Instance,
						serverClient,
						"private-store-test",
						new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
						world: world,
						crypt: crypt,
						sentPacketObserver: sentPackets.Add,
						privateStoreCreatePlanObserver: createPlans.Add,
						privateStoreNameOpenCompositionPlanObserver: nameOpenPlans.Add),
					createPlans,
					nameOpenPlans,
					sentPackets);
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
