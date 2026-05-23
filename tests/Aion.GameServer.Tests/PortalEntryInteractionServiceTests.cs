using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class PortalEntryInteractionServiceTests
{
	private const int PlayerObjectId = 1001;
	private const int NpcObjectId = 7001;
	private const int NpcTemplateId = 730001;
	private const int PortalWorldId = 300030000;
	private const int DialogActionId = 10000;
	private const int RequiredItemId = 185000077;
	private const int KinahItemId = 182400001;

	[Fact]
	public async Task HandleDialogSelect_SendsValidationFailurePacketWithoutConsumptionPackets()
	{
		var service = CreateService();
		var player = CreatePlayer(level: 25);
		var world = CreateWorldWithPortalNpc();
		var sentPackets = new List<GameServerPacket>();
		var path = CreatePortalPath(itemRequirements: [new PortalItemRequirementSummary(RequiredItemId, 1)]);

		var result = await service.HandleDialogSelectAsync(
			player,
			NpcObjectId,
			DialogActionId,
			questId: 0,
			world,
			CreatePortalPaths(path),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 0),
			CreateWorldMaps(isInstance: false),
			CreateItemTemplates(RequiredItemId, KinahItemId),
			(packet, _) =>
			{
				sentPackets.Add(packet);
				return Task.CompletedTask;
			},
			DateTimeOffset.UnixEpoch);

		Assert.True(result.Handled);
		Assert.Equal(PortalDialogEntryStatus.ValidationRejected, result.Status);
		Assert.Equal(PortalEntryPreparationStatus.ValidationRejected, result.Preparation?.Status);
		Assert.Collection(sentPackets, packet => Assert.IsType<SmDialogWindow>(packet));
		Assert.Empty(player.InventoryItems);
	}

	[Fact]
	public async Task HandleDialogSelect_SendsRequirementConsumptionPacketsInJavaOrder()
	{
		var service = CreateService();
		var player = CreatePlayer(
			level: 25,
			items:
			[
				new InventoryItem { ObjectId = 10, ItemId = RequiredItemId, Count = 1, OwnerId = PlayerObjectId },
				new InventoryItem { ObjectId = 11, ItemId = RequiredItemId, Count = 3, OwnerId = PlayerObjectId },
				new InventoryItem { ObjectId = 12, ItemId = KinahItemId, Count = 1000, OwnerId = PlayerObjectId },
			]);
		var world = CreateWorldWithPortalNpc();
		var sentPackets = new List<GameServerPacket>();
		var order = new List<string>();
		var path = CreatePortalPath(
			kinah: 500,
			itemRequirements: [new PortalItemRequirementSummary(RequiredItemId, 3)]);

		var result = await service.HandleDialogSelectAsync(
			player,
			NpcObjectId,
			DialogActionId,
			questId: 0,
			world,
			CreatePortalPaths(path),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 0),
			CreateWorldMaps(isInstance: false),
			CreateItemTemplates(RequiredItemId, KinahItemId),
			(packet, _) =>
			{
				sentPackets.Add(packet);
				order.Add(packet.GetType().Name);
				return Task.CompletedTask;
			},
			DateTimeOffset.UnixEpoch,
			sameInstanceTeleportAsync: (_, loc, _) =>
			{
				order.Add($"teleport:{loc.WorldId}");
				return Task.CompletedTask;
			});

		Assert.True(result.Handled);
		Assert.Equal(PortalDialogEntryStatus.Ready, result.Status);
		Assert.Equal(PortalEntryPlanAction.SameInstanceTeleport, result.Preparation?.EntryPlan.Action);
		Assert.Collection(
			sentPackets,
			packet => Assert.IsType<SmDeleteItem>(packet),
			packet => Assert.IsType<SmCubeUpdate>(packet),
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => Assert.IsType<SmInventoryUpdateItem>(packet));
		Assert.Equal(
			["SmDeleteItem", "SmCubeUpdate", "SmInventoryUpdateItem", "SmInventoryUpdateItem", $"teleport:{PortalWorldId}"],
			order);
		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ObjectId),
			item =>
			{
				Assert.Equal(11, item.ObjectId);
				Assert.Equal(1, item.Count);
			},
			item =>
			{
				Assert.Equal(12, item.ObjectId);
				Assert.Equal(500, item.Count);
			});
	}

	[Fact]
	public void TeleportWithinSameInstance_MutatesPositionAndSetsLandingBeforeSpawnPackets()
	{
		var player = CreatePlayer(level: 25, position: new WorldPosition(PortalWorldId, 10, 20, 30, 0, InstanceId: 7));
		var destination = new WorldPosition(PortalWorldId, 100, 200, 300, 40, InstanceId: 7);

		var result = PlayerTeleportService.TeleportWithinSameInstance(player, destination);

		Assert.Equal(new WorldPosition(PortalWorldId, 10, 20, 30, 0, InstanceId: 7), result.PreviousPosition);
		Assert.Equal(destination, result.Destination);
		Assert.True(result.UsesSameWorldSpawnPath);
		Assert.Equal(destination, player.Position);
		Assert.Equal(ArrivalAnimation.Landing, player.PortAnimation);
		Assert.Equal(destination.X, player.Movement.TargetX);
		Assert.Equal(destination.Y, player.Movement.TargetY);
		Assert.Equal(destination.Z, player.Movement.TargetZ);
	}

	[Fact]
	public async Task HandleDialogSelect_SkipsRequirementConsumptionPacketsForJavaReentry()
	{
		var service = CreateService();
		var player = CreatePlayer(
			level: 25,
			position: new WorldPosition(210010000, 10, 20, 30, 0),
			items:
			[
				new InventoryItem { ObjectId = 10, ItemId = RequiredItemId, Count = 1, OwnerId = PlayerObjectId },
				new InventoryItem { ObjectId = 12, ItemId = KinahItemId, Count = 1000, OwnerId = PlayerObjectId },
			]);
		var world = CreateWorldWithPortalNpc(player.Position);
		var sentPackets = new List<GameServerPacket>();
		var path = CreatePortalPath(
			kinah: 500,
			itemRequirements: [new PortalItemRequirementSummary(RequiredItemId, 1)]);
		var worldMaps = CreateWorldMaps(isInstance: true);
		var registered = worldMaps.AddWorldMapInstance(PortalWorldId, instanceId: 2, ownerId: PlayerObjectId, maxPlayers: 1);
		Assert.NotNull(registered);
		registered.Register(PlayerObjectId);

		var result = await service.HandleDialogSelectAsync(
			player,
			NpcObjectId,
			DialogActionId,
			questId: 0,
			world,
			CreatePortalPaths(path),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 1),
			worldMaps,
			CreateItemTemplates(RequiredItemId, KinahItemId),
			(packet, _) =>
			{
				sentPackets.Add(packet);
				return Task.CompletedTask;
			},
			DateTimeOffset.UnixEpoch);

		Assert.True(result.Handled);
		Assert.Equal(PortalDialogEntryStatus.Ready, result.Status);
		Assert.True(result.Preparation?.EntryPlan.Reenter);
		Assert.Empty(sentPackets);
		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ObjectId),
			item =>
			{
				Assert.Equal(10, item.ObjectId);
				Assert.Equal(1, item.Count);
			},
			item =>
			{
				Assert.Equal(12, item.ObjectId);
				Assert.Equal(1000, item.Count);
			});
	}

	[Fact]
	public async Task HandleDialogSelect_SendsTeamRequirementFailurePacketBeforeUnsupportedFanout()
	{
		var service = CreateService();
		var player = CreatePlayer(level: 25);
		var world = CreateWorldWithPortalNpc();
		var sentPackets = new List<GameServerPacket>();

		var result = await service.HandleDialogSelectAsync(
			player,
			NpcObjectId,
			DialogActionId,
			questId: 0,
			world,
			CreatePortalPaths(CreatePortalPath()),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 3),
			CreateWorldMaps(isInstance: true),
			CreateItemTemplates(KinahItemId),
			(packet, _) =>
			{
				sentPackets.Add(packet);
				return Task.CompletedTask;
			},
			DateTimeOffset.UnixEpoch);

		Assert.True(result.Handled);
		Assert.Equal(PortalDialogEntryStatus.ValidationRejected, result.Status);
		Assert.Equal(PortalEntryValidationStatus.GroupRequired, result.Preparation?.EntryPlan.Status);
		var packet = Assert.Single(sentPackets);
		var message = Assert.IsType<SmSystemMessage>(packet);
		Assert.Equal(1390256, message.MessageId);
	}

	[Fact]
	public async Task HandleDialogSelect_ReturnsUnsupportedTeamPortalForGroupedPlayerWithoutPackets()
	{
		var service = CreateService();
		var player = CreatePlayer(level: 25);
		player.TeamMembership = PlayerTeamMembership.Group;
		player.CurrentTeamId = 88001;
		player.CurrentTeamMemberObjectIds = [PlayerObjectId, 1002];
		var world = CreateWorldWithPortalNpc();
		var sentPackets = new List<GameServerPacket>();
		var worldMaps = CreateWorldMaps(isInstance: true);
		var registered = worldMaps.AddWorldMapInstance(PortalWorldId, instanceId: 7, maxPlayers: 6);
		Assert.NotNull(registered);
		registered.RegisterTeamId(88001);

		var result = await service.HandleDialogSelectAsync(
			player,
			NpcObjectId,
			DialogActionId,
			questId: 0,
			world,
			CreatePortalPaths(CreatePortalPath()),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 6),
			worldMaps,
			CreateItemTemplates(KinahItemId),
			(packet, _) =>
			{
				sentPackets.Add(packet);
				return Task.CompletedTask;
			},
			DateTimeOffset.UnixEpoch);

		Assert.True(result.Handled);
		Assert.Equal(PortalDialogEntryStatus.UnsupportedTeamPortal, result.Status);
		Assert.Equal(PortalEntryPreparationStatus.UnsupportedTeamPortal, result.Preparation?.Status);
		Assert.Equal(PortalEntryValidationStatus.UnsupportedTeamPortal, result.Preparation?.EntryPlan.Status);
		Assert.NotNull(result.Preparation?.EntryPlan.TeamPlan);
		Assert.Equal(PortalTeamEntryDisposition.RegisteredInstanceTransfer, result.Preparation.EntryPlan.TeamPlan.Disposition);
		Assert.Same(registered, result.Preparation.EntryPlan.TeamPlan.RegisteredInstance);
		Assert.Empty(sentPackets);
	}

	private static PortalEntryInteractionService CreateService()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var playerEnterWorld = new PlayerEnterWorldService(
			new GameServerOptions(),
			new EmptyPlayerEnterWorldRepository(),
			world,
			NullLogger<PlayerEnterWorldService>.Instance);
		return new PortalEntryInteractionService(playerEnterWorld);
	}

	private static Player CreatePlayer(
		int level,
		WorldPosition? position = null,
		IReadOnlyList<InventoryItem>? items = null)
	{
		return new Player
		{
			ObjectId = PlayerObjectId,
			Name = "Tester",
			PlayerClass = "RANGER",
			Race = "ELYOS",
			Gender = "MALE",
			Level = level,
			Position = position ?? new WorldPosition(PortalWorldId, 10, 20, 30, 0),
			InventoryItems = items ?? Array.Empty<InventoryItem>(),
		};
	}

	private static GameWorld CreateWorldWithPortalNpc(WorldPosition? npcPosition = null)
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		world.TryAddObject(
			NpcObjectId,
			new WorldNpc(
				NpcObjectId,
				NpcTemplateId,
				new NpcTemplateSummary(
					NpcTemplateId,
					"Portal",
					0,
					25,
					"NORMAL",
					"NORMAL",
					"PC_ALL",
					"GENERAL",
					"NPC",
					TalkDistance: 4,
					HasTalkInfo: true,
					IsDialogNpc: true),
				npcPosition ?? new WorldPosition(PortalWorldId, 11, 20, 30, 0)));
		return world;
	}

	private static PortalPathSummary CreatePortalPath(
		int kinah = 0,
		IReadOnlyList<PortalItemRequirementSummary>? itemRequirements = null)
	{
		return new PortalPathSummary(
			PortalPathSource.Dialog,
			NpcTemplateId,
			string.Empty,
			DialogActionId,
			LocId: 1,
			SiegeId: 0,
			Race: "PC_ALL",
			MinLevel: 25,
			MinRank: 0,
			kinah,
			TitleId: 0,
			ErrGroup: 0,
			ErrLevel: 0)
		{
			ItemRequirements = itemRequirements ?? Array.Empty<PortalItemRequirementSummary>(),
		};
	}

	private static PortalPathTable CreatePortalPaths(PortalPathSummary path)
	{
		return new PortalPathTable([path], new Dictionary<int, int>(), Array.Empty<PortalPathSummary>(), Array.Empty<PortalPathSummary>());
	}

	private static PortalLocTable CreatePortalLocs()
	{
		return new PortalLocTable([new PortalLocSummary(PortalWorldId, LocId: 1, 100, 200, 300, 40)]);
	}

	private static InstanceCooltimeTable CreatePortalCooltimes(int maxPlayers)
	{
		return new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				Id: 1,
				WorldId: PortalWorldId,
				Race: "PC_ALL",
				MaxCount: maxPlayers == 0 ? 0 : 1,
				MaxMemberLight: maxPlayers,
				MaxMemberDark: maxPlayers,
				EnterMinLevelLight: 25,
				EnterMinLevelDark: 25),
		]);
	}

	private static WorldMapRuntimeStateTable CreateWorldMaps(bool isInstance)
	{
		return new WorldMapRuntimeStateTable([new WorldMapSummary(PortalWorldId, isInstance, TwinCount: 1)]);
	}

	private static ItemTemplateTable CreateItemTemplates(params int[] itemIds)
	{
		return new ItemTemplateTable(itemIds.Select(CreateItemTemplate).ToArray());
	}

	private static ItemTemplateSummary CreateItemTemplate(int itemId)
	{
		return new ItemTemplateSummary(
			itemId,
			$"item-{itemId}",
			DescriptionId: itemId,
			Mask: 0,
			Level: 1,
			ItemGroup: itemId == KinahItemId ? "MONEY" : "NORMAL",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: 1000000,
			Price: 0,
			ValidEquipmentSlots: 0);
	}
}
