using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcDropRegistrationWorkflowServiceTests
{
	[Fact]
	public async Task RegisterCustomDropsAsync_RegistersGeneratedDropsAndStartsFanout()
	{
		var customDropService = new WorldNpcCustomDropService(
			new CustomNpcDropTable(
			[
				new CustomNpcDropSummary(
					203001,
					[
						new CustomDropGroupSummary(
							"custom",
							"PC_ALL",
							UseLevelBasedChanceReduction: false,
							MaxItems: 1,
							[new CustomDropSummary(166020000, 2, 2, 100f, false)]),
					]),
			]),
			chanceRoll: () => 0f);
		var dropRegistration = new WorldNpcDropRegistrationService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var lootService = new WorldNpcLootService(dropRegistration, threadPoolManager: threadPoolManager);
			var registry = new CapturingConnectionRegistry();
			registry.OnlinePlayerObjectIds.Add(1001);
			var broadcastService = new WorldNpcLootBroadcastService(lootService, registry);
			var workflow = new WorldNpcDropRegistrationWorkflowService(customDropService, dropRegistration, broadcastService);
			var npc = CreateNpc(5001, 203001);
			var looter = new Player { ObjectId = 1001, Race = "ELYOS" };

			var result = await workflow.RegisterCustomDropsAsync(
				npc,
				looter,
				freeForAllDelay: TimeSpan.FromMilliseconds(10));

			Assert.Equal(WorldNpcDropRegistrationWorkflowStatus.Registered, result.Status);
			var generatedDrop = Assert.Single(result.Drops);
			Assert.Equal(166020000, generatedDrop.ItemId);
			Assert.Equal(2, generatedDrop.Count);
			Assert.Equal(5001, generatedDrop.NpcObjectId);
			Assert.Equal(result.Drops, dropRegistration.GetCurrentDrops(5001));
			Assert.True(dropRegistration.TryGetRegistration(5001, out var registration));
			Assert.True(registration!.IsAllowedToLoot(1001));
			Assert.NotNull(result.Fanout);
			Assert.True(result.Fanout!.FreeForAllScheduled);
			Assert.Single(registry.SentPackets);
			var initialPacket = Assert.IsType<SmLootStatus>(registry.SentPackets[0].Packet);
			Assert.Equal(SmLootStatusType.LootEnable, initialPacket.Status);
			Assert.Equal(1003, initialPacket.LootEffectId);

			var completed = await Task.WhenAny(result.Fanout.FreeForAllTask!.Completion, Task.Delay(TimeSpan.FromSeconds(1)));
			Assert.Same(result.Fanout.FreeForAllTask.Completion, completed);
			Assert.True(registration.IsFreeForAll);
			var broadcastPacket = Assert.IsType<SmLootStatus>(registry.BroadcastPacket);
			Assert.Equal(SmLootStatusType.LootEnable, broadcastPacket.Status);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task RegisterCustomDropsAsync_SkipsNpcWithoutGeneratedDrops()
	{
		var workflow = new WorldNpcDropRegistrationWorkflowService(
			new WorldNpcCustomDropService(new CustomNpcDropTable([])),
			new WorldNpcDropRegistrationService(),
			new WorldNpcLootBroadcastService(
				new WorldNpcLootService(new WorldNpcDropRegistrationService()),
				new CapturingConnectionRegistry()));

		var result = await workflow.RegisterCustomDropsAsync(
			CreateNpc(5001, 203001),
			new Player { ObjectId = 1001, Race = "ELYOS" });

		Assert.Equal(WorldNpcDropRegistrationWorkflowStatus.NoGeneratedDrops, result.Status);
		Assert.Empty(result.Drops);
		Assert.Null(result.Fanout);
	}

	[Fact]
	public async Task RegisterCustomDropsAsync_RegistersQuestDropsWhenCustomDropsAreEmpty()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var lootService = new WorldNpcLootService(dropRegistration, threadPoolManager: threadPoolManager);
			var registry = new CapturingConnectionRegistry();
			registry.OnlinePlayerObjectIds.Add(1001);
			var workflow = new WorldNpcDropRegistrationWorkflowService(
				new WorldNpcCustomDropService(new CustomNpcDropTable([])),
				dropRegistration,
				new WorldNpcLootBroadcastService(lootService, registry),
				questDropService: new WorldNpcQuestDropService(
					new QuestDropTable(
					[
						new QuestDropSummary(
							9100,
							203001,
							182200001,
							Chance: 100,
							DropEachMember: 0,
							CollectingStep: 0,
							Target: "NONE",
							MentorType: "NONE",
							CollectItems: []),
					]),
					chanceRoll: () => 0f));
			var looter = new Player
			{
				ObjectId = 1001,
				Race = "ELYOS",
				Quests = [new PlayerQuestState(9100, "START", QuestVars: 0, Flags: 0, CompleteCount: 0)],
			};

			var result = await workflow.RegisterCustomDropsAsync(
				CreateNpc(5001, 203001),
				looter,
				freeForAllDelay: TimeSpan.FromMilliseconds(10));

			Assert.Equal(WorldNpcDropRegistrationWorkflowStatus.Registered, result.Status);
			var drop = Assert.Single(result.Drops);
			Assert.Equal(1, drop.Index);
			Assert.Equal(182200001, drop.ItemId);
			Assert.True(drop.CanViewDropItem(1001));
			Assert.Equal(result.Drops, dropRegistration.GetCurrentDrops(5001));
			Assert.True(dropRegistration.TryGetRegistration(5001, out var registration));
			Assert.True(registration!.IsAllowedToLoot(1001));
			Assert.Single(registry.SentPackets);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task RegisterCustomDropsAsync_RegistersGlobalDropsWhenCustomAndQuestDropsAreEmpty()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var lootService = new WorldNpcLootService(dropRegistration, threadPoolManager: threadPoolManager);
			var registry = new CapturingConnectionRegistry();
			registry.OnlinePlayerObjectIds.Add(1001);
			var workflow = new WorldNpcDropRegistrationWorkflowService(
				new WorldNpcCustomDropService(new CustomNpcDropTable([])),
				dropRegistration,
				new WorldNpcLootBroadcastService(lootService, registry),
				questDropService: new WorldNpcQuestDropService(new QuestDropTable([])),
				globalDropService: new WorldNpcGlobalDropService(
					new GlobalDropTable(
					[
						new GlobalDropRuleSummary(
							"default-global",
							Chance: 100,
							DynamicChance: false,
							MinDiff: -99,
							MaxDiff: 99,
							RestrictionRace: "",
							UseLevelBasedChanceReduction: false,
							MemberLimit: 1,
							MaxDropRule: 1,
							Items: [new GlobalDropItemSummary(1001, 4, 4, 100)],
							WorldTypes: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
							Races: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
							Ratings: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
							MapIds: new HashSet<int>(),
							Tribes: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
							NpcIds: new HashSet<int>(),
							NpcNames: [],
							NpcGroups: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
							ExcludedNpcIds: new HashSet<int>(),
							Zones: new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
					]),
					new ItemTemplateTable([CreateItem(1001, level: 10, race: "PC_ALL")]),
					chanceRoll: () => 0f));
			var looter = new Player { ObjectId = 1001, Race = "ELYOS", Level = 10 };

			var result = await workflow.RegisterCustomDropsAsync(
				CreateNpc(5001, 203001),
				looter,
				freeForAllDelay: TimeSpan.FromMilliseconds(10));

			Assert.Equal(WorldNpcDropRegistrationWorkflowStatus.Registered, result.Status);
			var drop = Assert.Single(result.Drops);
			Assert.Equal(1, drop.Index);
			Assert.Equal(1001, drop.ItemId);
			Assert.Equal(4, drop.Count);
			Assert.Equal(result.Drops, dropRegistration.GetCurrentDrops(5001));
			Assert.True(dropRegistration.TryGetRegistration(5001, out var registration));
			Assert.True(registration!.IsAllowedToLoot(1001));
			Assert.Single(registry.SentPackets);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task RegisterCustomDropsAsync_RegistersActiveEventDropsAfterDefaultGlobals()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var lootService = new WorldNpcLootService(dropRegistration, threadPoolManager: threadPoolManager);
			var registry = new CapturingConnectionRegistry();
			registry.OnlinePlayerObjectIds.Add(1001);
			var eventRule = new GlobalDropRuleSummary(
				"active-event",
				Chance: 100,
				DynamicChance: false,
				MinDiff: -99,
				MaxDiff: 99,
				RestrictionRace: "",
				UseLevelBasedChanceReduction: false,
				MemberLimit: 1,
				MaxDropRule: 1,
				Items: [new GlobalDropItemSummary(1002, 5, 5, 100)],
				WorldTypes: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
				Races: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
				Ratings: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
				MapIds: new HashSet<int>(),
				Tribes: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
				NpcIds: new HashSet<int>(),
				NpcNames: [],
				NpcGroups: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
				ExcludedNpcIds: new HashSet<int>(),
				Zones: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
			var workflow = new WorldNpcDropRegistrationWorkflowService(
				new WorldNpcCustomDropService(new CustomNpcDropTable([])),
				dropRegistration,
				new WorldNpcLootBroadcastService(lootService, registry),
				questDropService: new WorldNpcQuestDropService(new QuestDropTable([])),
				globalDropService: new WorldNpcGlobalDropService(
					new GlobalDropTable([]),
					new ItemTemplateTable([CreateItem(1002, level: 10, race: "PC_ALL")]),
					chanceRoll: () => 0f),
				eventDropRuleService: new WorldNpcEventDropRuleService(
					new EventDropTable(
					[
						new EventTemplateSummary(
							"Active Event",
							new DateTime(2026, 5, 1, 0, 0, 0),
							new DateTime(2026, 5, 30, 0, 0, 0),
							"",
							[eventRule]),
					]),
					now: () => new DateTime(2026, 5, 23, 12, 0, 0)));
			var looter = new Player { ObjectId = 1001, Race = "ELYOS", Level = 10 };

			var result = await workflow.RegisterCustomDropsAsync(
				CreateNpc(5001, 203001),
				looter,
				freeForAllDelay: TimeSpan.FromMilliseconds(10));

			Assert.Equal(WorldNpcDropRegistrationWorkflowStatus.Registered, result.Status);
			var drop = Assert.Single(result.Drops);
			Assert.Equal(1, drop.Index);
			Assert.Equal(1002, drop.ItemId);
			Assert.Equal(5, drop.Count);
			Assert.Equal(result.Drops, dropRegistration.GetCurrentDrops(5001));
			Assert.Single(registry.SentPackets);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	private static WorldNpc CreateNpc(int objectId, int templateId)
	{
		return new WorldNpc(
			objectId,
			templateId,
			new NpcTemplateSummary(templateId, "loot_npc", 0, 10, "NORMAL", "NORMAL", "NONE", "NONE", "GENERAL"),
			new WorldPosition(210010000, 1, 2, 3, 0));
	}

	private static ItemTemplateSummary CreateItem(int itemId, int level, string race)
	{
		return new ItemTemplateSummary(
			itemId,
			$"item-{itemId}",
			DescriptionId: 0,
			Mask: 0,
			Level: level,
			ItemGroup: "MISC",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: race,
			MaxStackCount: 100,
			Price: 0,
			ValidEquipmentSlots: 0);
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public HashSet<int> OnlinePlayerObjectIds { get; } = [];

		public List<PacketDelivery> SentPackets { get; } = [];

		public GameServerPacket? BroadcastPacket { get; private set; }

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			if (!OnlinePlayerObjectIds.Contains(playerObjectId))
				return Task.FromResult(false);

			SentPackets.Add(new PacketDelivery(playerObjectId, packet));
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			BroadcastPacket = packet;
			return Task.FromResult(1);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			return Task.FromResult(false);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}

	private sealed record PacketDelivery(int PlayerObjectId, GameServerPacket Packet);
}
