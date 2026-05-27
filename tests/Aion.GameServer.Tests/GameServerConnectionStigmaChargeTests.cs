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

public sealed class GameServerConnectionStigmaChargeTests
{
	[Fact]
	public async Task CompleteStigmaChargeAsync_WritesCleanupSealFlagForRestrictedFullUpdates()
	{
		await using var fixture = await StigmaChargeFixture.CreateAsync();
		var player = new Player
		{
			ObjectId = 1001,
			InventoryItems =
			[
				CreateStigmaItem(objectId: 5001, count: 1, enchant: 3),
				CreateStigmaItem(objectId: 5002, count: 2, enchant: 0),
			],
		};
		var targetUpdate = CreateStigmaItem(objectId: 5001, count: 1, enchant: 4);
		var sourceUpdate = CreateStigmaItem(objectId: 5002, count: 1, enchant: 0);
		var plan = new StigmaChargePlan(
			StigmaChargeResult.Success,
			EnchantSucceeded: true,
			ItemName: "Restricted Stigma",
			InventoryItems: [targetUpdate, sourceUpdate],
			TargetItemUpdate: targetUpdate,
			DeletedTargetItemObjectId: null,
			SourceItemUpdate: sourceUpdate,
			DeletedSourceItemObjectId: null,
			Skills: [],
			AddedSkills: [],
			RemovedSkills: [],
			HiddenSkillDeleteMessages: []);
		var template = CreateStigmaTemplate();

		await InvokeCompleteStigmaChargeAsync(
			fixture.Connection,
			player,
			plan,
			template,
			template,
			targetObjectId: 5001,
			targetItemId: StigmaItemId,
			targetWasEquipped: false,
			fixture.StaticData);

		Assert.Same(plan.InventoryItems, player.InventoryItems);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmItemUsageAnimation>(packet),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5002,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseStigmaUse,
				expectedCleanupSealFlag: 3),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3));
	}

	private static async Task InvokeCompleteStigmaChargeAsync(
		GameServerConnection connection,
		Player player,
		StigmaChargePlan plan,
		ItemTemplateSummary sourceTemplate,
		ItemTemplateSummary targetTemplate,
		int targetObjectId,
		int targetItemId,
		bool targetWasEquipped,
		StaticData staticData)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"CompleteStigmaChargeAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(
			connection,
			[player, plan, sourceTemplate, targetTemplate, targetObjectId, targetItemId, targetWasEquipped, staticData, CancellationToken.None]));
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

	private static InventoryItem CreateStigmaItem(int objectId, long count, int enchant)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = StigmaItemId,
			Count = count,
			Location = 0,
			Slot = 65535,
			Enchant = enchant,
		};
	}

	private static ItemTemplateSummary CreateStigmaTemplate()
	{
		return new ItemTemplateSummary(
			StigmaItemId,
			"Restricted Stigma",
			0,
			1,
			1,
			"STIGMA",
			"NORMAL",
			"COMMON",
			"PC_ALL",
			1,
			0,
			0,
			StigmaInfo: new ItemStigmaInfo(["STIGMA_TEST"], Chargeable: true));
	}

	private const int StigmaItemId = 140001001;

	private sealed class StigmaChargeFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly string _tempRoot;

		private StigmaChargeFixture(TcpClient client, GameServerConnection connection, StaticData staticData, List<GameServerPacket> sentPackets, string tempRoot)
		{
			_client = client;
			Connection = connection;
			StaticData = staticData;
			SentPackets = sentPackets;
			_tempRoot = tempRoot;
		}

		public GameServerConnection Connection { get; }

		public StaticData StaticData { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<StigmaChargeFixture> CreateAsync()
		{
			var tempRoot = Path.Combine(Path.GetTempPath(), "aion-stigma-charge-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path.Combine(tempRoot, "game-server", "data", "static_data"));
			await File.WriteAllTextAsync(
				Path.Combine(tempRoot, "game-server", "data", "static_data", "static_data.xml"),
				"""
				<?xml version="1.0" encoding="UTF-8"?>
				<static_data>
					<item_restriction_cleanups>
						<cleanup id="140001001" awh="0" lwh="1"/>
					</item_restriction_cleanups>
				</static_data>
				""");
			var dataManager = await DataManager.LoadAsync(
				tempRoot,
				cacheDirectory: Path.Combine(tempRoot, "cache"),
				validateWhenCacheChanges: false);
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
					"stigma-charge-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new StigmaChargeFixture(client, connection, dataManager.StaticData, sentPackets, tempRoot);
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
			if (Directory.Exists(_tempRoot))
				Directory.Delete(_tempRoot, recursive: true);
		}
	}
}
