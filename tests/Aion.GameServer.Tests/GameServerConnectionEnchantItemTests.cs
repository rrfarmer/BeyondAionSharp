using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionEnchantItemTests
{
	[Fact]
	public async Task CompleteEnchantItemAsync_WritesCleanupSealFlagForRestrictedSupplementSourceAndTargetFullUpdates()
	{
		await using var fixture = await EnchantItemFixture.CreateAsync();
		var supplementUpdate = CreateItem(objectId: SupplementObjectId, itemId: SupplementItemId, count: 1);
		var sourceUpdate = CreateItem(objectId: SourceObjectId, itemId: SourceItemId, count: 1);
		var targetUpdate = new InventoryItem
		{
			ObjectId = TargetObjectId,
			ItemId = TargetItemId,
			Count = 1,
			Location = 0,
			Slot = 65535,
			Enchant = 11,
		};
		var planInventory = new[] { supplementUpdate, sourceUpdate, targetUpdate };
		var player = new Player
		{
			ObjectId = 1001,
			InventoryItems =
			[
				CreateItem(objectId: SupplementObjectId, itemId: SupplementItemId, count: 2),
				CreateItem(objectId: SourceObjectId, itemId: SourceItemId, count: 2),
				CreateItem(objectId: TargetObjectId, itemId: TargetItemId, count: 1),
			],
		};
		var plan = new EnchantItemPlan(
			EnchantItemFailure.None,
			ItemName: "Restricted Enchant Target",
			EnchantmentStoneName: "Restricted Enchant Stone",
			InventoryItems: planInventory,
			TargetItemUpdate: targetUpdate,
			DeletedTargetItemObjectId: null,
			SourceItemUpdate: sourceUpdate,
			DeletedSourceItemObjectId: null,
			SupplementItemUpdates: [supplementUpdate],
			DeletedSupplementItemObjectIds: Array.Empty<int>(),
			EnchantSucceeded: true,
			TargetDestroyed: false,
			RefreshStats: false,
			Skills: Array.Empty<PlayerSkill>(),
			AddedBuffSkills: Array.Empty<PlayerSkill>(),
			RemovedBuffSkills: Array.Empty<PlayerSkill>(),
			NewEnchantLevel: 11,
			EnchantBuffSkillId: 0);
		var sourceTemplate = CreateTemplate(SourceItemId, "Restricted Enchant Stone");
		var targetTemplate = CreateTemplate(TargetItemId, "Restricted Enchant Target");
		var supplementTemplate = CreateTemplate(SupplementItemId, "Restricted Enchant Supplement");
		var itemTemplates = new ItemTemplateTable([sourceTemplate, targetTemplate, supplementTemplate]);
		var cleanups = new ItemRestrictionCleanupTable(
		[
			new ItemRestrictionCleanupSummary(SupplementItemId, AccountWarehouse: 0, LegionWarehouse: 1),
			new ItemRestrictionCleanupSummary(SourceItemId, AccountWarehouse: 1, LegionWarehouse: 0),
			new ItemRestrictionCleanupSummary(TargetItemId, AccountWarehouse: 0, LegionWarehouse: 0),
		]);

		await InvokeCompleteEnchantItemAsync(
			fixture.Connection,
			player,
			plan,
			sourceTemplate,
			targetTemplate,
			itemTemplates,
			cleanups);

		Assert.Same(planInventory, player.InventoryItems);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: SupplementObjectId,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: SourceObjectId,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: TargetObjectId,
				expectedUpdateType: 0,
				expectedCleanupSealFlag: 3),
			packet => Assert.IsType<SmItemUsageAnimation>(packet));
	}

	private static async Task InvokeCompleteEnchantItemAsync(
		GameServerConnection connection,
		Player player,
		EnchantItemPlan plan,
		ItemTemplateSummary sourceTemplate,
		ItemTemplateSummary targetTemplate,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable cleanups)
	{
		var method = typeof(GameServerConnection)
			.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
			.Single(method =>
			{
				var parameters = method.GetParameters();
				return method.Name == "CompleteEnchantItemAsync"
					&& parameters.Length == 11
					&& parameters[1].ParameterType == typeof(int);
			});
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(
			connection,
			[
				player,
				SourceObjectId,
				TargetObjectId,
				plan,
				sourceTemplate,
				targetTemplate,
				itemTemplates,
				cleanups,
				null,
				null,
				CancellationToken.None,
			]));
		await task;
	}

	private static void AssertInventoryUpdatePayloadWithCleanupSealFlag(
		SmInventoryUpdateItem packet,
		int expectedObjectId,
		int expectedUpdateType,
		int expectedCleanupSealFlag)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		var blobSize = reader.ReadH();
		Assert.True(blobSize > 0);
		var blob = reader.ReadB(blobSize);
		AssertGeneralInfoCleanupSealFlag(blob, expectedItemMask: 1, expectedFlag: expectedCleanupSealFlag);
		Assert.Equal(expectedUpdateType, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertGeneralInfoCleanupSealFlag(byte[] blob, int expectedItemMask, int expectedFlag)
	{
		using var reader = new PacketBuffer(blob);
		Assert.Equal(0x00, (int)reader.ReadC());
		Assert.Equal(expectedItemMask, reader.ReadH());
		Assert.Equal(1, reader.ReadQ());
		Assert.Equal(string.Empty, reader.ReadS());
		Assert.Equal(0, (int)reader.ReadC());
		reader.ReadD();
		reader.ReadD();
		reader.ReadD();
		Assert.Equal(expectedFlag, reader.ReadH());
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static InventoryItem CreateItem(int objectId, int itemId, long count)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			Location = 0,
			Slot = 65535,
		};
	}

	private static ItemTemplateSummary CreateTemplate(int itemId, string name)
	{
		return new ItemTemplateSummary(
			itemId,
			name,
			0,
			1,
			1,
			"NONE",
			"NORMAL",
			"COMMON",
			"PC_ALL",
			100,
			0,
			0);
	}

	private const int SupplementObjectId = 4001;
	private const int SourceObjectId = 5001;
	private const int TargetObjectId = 6001;
	private const int SupplementItemId = 166100001;
	private const int SourceItemId = 166000001;
	private const int TargetItemId = 100000001;

	private sealed class EnchantItemFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private EnchantItemFixture(TcpClient client, GameServerConnection connection, List<GameServerPacket> sentPackets)
		{
			_client = client;
			Connection = connection;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<EnchantItemFixture> CreateAsync()
		{
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
					"enchant-item-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new EnchantItemFixture(client, connection, sentPackets);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			_client.Dispose();
			await Connection.CloseAsync();
		}
	}
}
