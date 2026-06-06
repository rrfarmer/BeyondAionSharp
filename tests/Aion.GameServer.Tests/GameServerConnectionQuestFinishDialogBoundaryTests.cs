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
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionQuestFinishDialogBoundaryTests
{
	private const int SelectedQuestAutoReward = 108;
	private const int KinahItemId = 182400001;
	private const int RewardItemId = 186000001;

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAppliesXpAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1001, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1001, "REWARD", QuestVars: 0x12, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1001,
			Name = "QuestFinishBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1001,
			extendedRewardIndex: 0);

		var stagedPlan = QuestFinishSocketGuardedOperationCompositionPlanService.CreatePlan(
			player,
			packet,
			fixture.StaticData.QuestFinishRewardProjections,
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 26, 11, 0, 0, TimeSpan.Zero),
			new GameServerOptions());

		Assert.Equal(QuestFinishSocketGuardedOperationCompositionStatus.Composed, stagedPlan.Status);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.Planned, stagedPlan.GuardedInputPlan.GuardPlan.Status);
		Assert.Equal(QuestFinishSocketInputAssemblyStatus.Ready, stagedPlan.GuardedInputPlan.InputPlan?.Status);
		Assert.NotNull(stagedPlan.OperationCompositionPlan?.OperationPlan);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmStatUpdateExp>(packet),
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1370002, message.MessageId);
			},
			packet => Assert.IsType<SmQuestAction>(packet));
		Assert.Equal(300, player.Exp);
		Assert.Equal(1, player.Level);
		Assert.Empty(player.InventoryItems);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAppliesKinahAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1002, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1002, "REWARD", QuestVars: 0x34, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1002,
			Name = "QuestFinishKinahBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			InventoryItems =
			[
				new InventoryItem { ObjectId = 5001, ItemId = KinahItemId, Count = 25, OwnerId = 1002, Location = 0 },
			],
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1002,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet =>
			{
				var inventoryUpdate = Assert.IsType<SmInventoryUpdateItem>(packet);
				Assert.Equal(SmInventoryUpdateItem.IncreaseKinahQuest, inventoryUpdate.UpdateType);
			},
			packet => Assert.IsType<SmQuestAction>(packet));
		var kinahItem = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, kinahItem.ObjectId);
		Assert.Equal(KinahItemId, kinahItem.ItemId);
		Assert.Equal(125, kinahItem.Count);
		Assert.Equal(0, player.Exp);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAddsFixedItemAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1003, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1003, "REWARD", QuestVars: 0x56, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1003,
			Name = "QuestFinishItemBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1003,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmInventoryAddItem>(packet),
			packet => Assert.IsType<SmCubeUpdate>(packet),
			packet => Assert.IsType<SmQuestAction>(packet));
		var rewardItem = Assert.Single(player.InventoryItems);
		Assert.Equal(RewardItemId, rewardItem.ItemId);
		Assert.Equal(2, rewardItem.Count);
		Assert.Equal(player.ObjectId, rewardItem.OwnerId);
		Assert.Equal(0, rewardItem.Location);
		Assert.Equal(0, player.Exp);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	private static CmDialogSelect CreateDialogSelect(
		int targetObjectId,
		int dialogActionId,
		int questId,
		int extendedRewardIndex)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(targetObjectId);
		writer.WriteH(dialogActionId);
		writer.WriteH(extendedRewardIndex);
		writer.WriteH(0);
		writer.WriteD(questId);
		writer.WriteH(0);
		var packet = new CmDialogSelect(56, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private sealed class QuestFinishDialogFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly GameServerConnection _connection;
		private readonly string _tempRoot;

		private QuestFinishDialogFixture(
			TcpClient client,
			GameServerConnection connection,
			StaticData staticData,
			List<GameServerPacket> sentPackets,
			string tempRoot)
		{
			_client = client;
			_connection = connection;
			StaticData = staticData;
			SentPackets = sentPackets;
			_tempRoot = tempRoot;
		}

		public GameServerConnection Connection => _connection;

		public StaticData StaticData { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<QuestFinishDialogFixture> CreateAsync()
		{
			var tempRoot = Path.Combine(Path.GetTempPath(), "aion-quest-finish-dialog-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path.Combine(tempRoot, "game-server", "data", "static_data"));
			await File.WriteAllTextAsync(
				Path.Combine(tempRoot, "game-server", "data", "static_data", "static_data.xml"),
				"""
				<?xml version="1.0" encoding="UTF-8"?>
				<static_data>
					<player_experience_table>
						<exp>0</exp>
						<exp>1000</exp>
						<exp>3000</exp>
					</player_experience_table>
					<item_templates>
						<item_template id="182400001" name="Kinah" desc="1" mask="0" level="1" item_group="NORMAL" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="2147483647" price="1" />
						<item_template id="186000001" name="Quest Reward Item" desc="1" mask="0" level="1" item_group="NORMAL" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100" price="1" />
					</item_templates>
					<quests>
						<quest id="1001" can_report="true" reward_repeat_count="1">
							<rewards exp="300" />
						</quest>
						<quest id="1002" can_report="true" reward_repeat_count="1">
							<rewards gold="100" />
						</quest>
						<quest id="1003" can_report="true" reward_repeat_count="1">
							<rewards>
								<reward_item item_id="186000001" count="2" />
							</rewards>
						</quest>
					</quests>
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
					"quest-finish-dialog-boundary-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					runtimeContext: runtimeContext,
					world: world,
					idFactory: new IDFactory(),
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new QuestFinishDialogFixture(client, connection, dataManager.StaticData, sentPackets, tempRoot);
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
