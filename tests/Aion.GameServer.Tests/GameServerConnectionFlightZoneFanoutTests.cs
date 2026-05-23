using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionFlightZoneFanoutTests
{
	[Fact]
	public async Task RevalidatePlayerFlightZonesAsync_BroadcastsStatsSpeedThenStopFlyWhenFlyingGliderLeavesFlyArea()
	{
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = CreateFlyingPlayer(7201);
		player.SetFlyState(PlayerFlyState.Gliding);
		player.SetCreatureState(PlayerCreatureState.Gliding, enabled: true);
		player.IsFpRestoreActive = true;

		var result = await pair.Connection.RevalidatePlayerFlightZonesAsync(player);

		Assert.True(result.LeftValidFlyArea);
		Assert.False(player.IsInFlyingState());
		Assert.True(player.IsInGlidingState());
		Assert.True(player.IsFpReduceActive);
		Assert.False(player.IsFpRestoreActive);
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => AssertEmotion(packet, player.ObjectId, EmotionType.ChangeSpeed),
			packet => AssertEmotion(packet, player.ObjectId, EmotionType.StopFly));
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.All(registry.Broadcasts, broadcast =>
		{
			Assert.Equal(player.Position, broadcast.SourcePosition);
			Assert.Equal(player.ObjectId, broadcast.SourceObjectId);
			Assert.True(broadcast.IncludeSourcePlayer);
		});
	}

	[Fact]
	public async Task RevalidatePlayerFlightZonesAsync_BroadcastsStatsSpeedThenLandWhenFlyingPlayerLeavesFlyArea()
	{
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = CreateFlyingPlayer(7202);
		player.IsFpReduceActive = true;

		var result = await pair.Connection.RevalidatePlayerFlightZonesAsync(player);

		Assert.True(result.LeftValidFlyArea);
		Assert.False(player.IsInFlyingState());
		Assert.False(player.IsInGlidingState());
		Assert.False(player.IsFpReduceActive);
		Assert.True(player.IsFpRestoreActive);
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => AssertEmotion(packet, player.ObjectId, EmotionType.ChangeSpeed),
			packet => AssertEmotion(packet, player.ObjectId, EmotionType.Land));
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.All(registry.Broadcasts, broadcast =>
		{
			Assert.Equal(player.Position, broadcast.SourcePosition);
			Assert.Equal(player.ObjectId, broadcast.SourceObjectId);
			Assert.True(broadcast.IncludeSourcePlayer);
		});
	}

	[Fact]
	public async Task RevalidatePlayerFlightZonesAsync_FeedsCreaturePvpZoneCountersFromStaticData()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry, runtimeContext, zoneCounterService);
		var player = new Player
		{
			ObjectId = 7301,
			Name = "pvp-zone-player",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 10,
			Position = new WorldPosition(210040000, 2700, 620, 150, 0),
			LifeStats = new PlayerLifeStats(CurrentHp: 111, CurrentMp: 205, CurrentFp: 55),
		};
		Assert.Contains(
			dataManager.StaticData.CreaturePvpZones.GetZonesByMapId(player.Position.WorldId),
			zone => zone.Name == "PVP_87_210040000" && zone.Contains(player.Position));

		await pair.Connection.RevalidatePlayerFlightZonesAsync(player);
		var enteredCounters = zoneCounterService.GetCounters(player.ObjectId);
		player.Position = player.Position with { X = 100, Y = 100, Z = 150 };
		await pair.Connection.RevalidatePlayerFlightZonesAsync(player);

		Assert.Equal(1, enteredCounters.PvpZoneCount);
		Assert.Equal(0, enteredCounters.SiegeZoneCount);
		Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(player.ObjectId));
		Assert.Empty(registry.PacketOrder);
	}

	[Fact]
	public async Task TeleportPlayerToKiskPositionAsync_RevalidatesCreaturePvpZoneCountersAfterTeleport()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry, runtimeContext, zoneCounterService);
		var insidePvpZone = new WorldPosition(210040000, 2700, 620, 150, 0);
		var outsidePvpZone = new WorldPosition(210040000, 100, 100, 150, 0);
		var pvpZones = dataManager.StaticData.CreaturePvpZones.GetZonesByMapId(insidePvpZone.WorldId);
		var player = CreateTeleportingPlayer(7302, insidePvpZone);
		CreaturePvpZoneRevalidationService.Revalidate(
			player.ObjectId,
			player.Position,
			dataManager.StaticData.CreaturePvpZones,
			zoneCounterService);
		Assert.Contains(pvpZones, zone => zone.Name == "PVP_87_210040000" && zone.Contains(insidePvpZone));
		Assert.DoesNotContain(pvpZones, zone => zone.Contains(outsidePvpZone));
		Assert.Equal(1, zoneCounterService.GetCounters(player.ObjectId).PvpZoneCount);

		await pair.Connection.TeleportPlayerToKiskPositionAsync(player, outsidePvpZone, dataManager.StaticData);
		var leftCounters = zoneCounterService.GetCounters(player.ObjectId);
		await pair.Connection.TeleportPlayerToKiskPositionAsync(player, insidePvpZone, dataManager.StaticData);
		var reenteredCounters = zoneCounterService.GetCounters(player.ObjectId);

		Assert.Equal(CreaturePvpZoneCounters.Empty, leftCounters);
		Assert.Equal(1, reenteredCounters.PvpZoneCount);
		Assert.Equal(0, reenteredCounters.SiegeZoneCount);
	}

	[Fact]
	public async Task HandleLevelReadyAsync_RevalidatesCreaturePvpZoneCountersAfterMapLoadTeleport()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry, runtimeContext, zoneCounterService);
		var outsidePvpZone = new WorldPosition(210040000, 100, 100, 150, 0);
		var insidePvpZone = new WorldPosition(210040000, 2700, 620, 150, 0);
		var player = CreateTeleportingPlayer(7308, outsidePvpZone);
		var pvpZones = dataManager.StaticData.CreaturePvpZones.GetZonesByMapId(insidePvpZone.WorldId);
		Assert.Contains(pvpZones, zone => zone.Name == "PVP_87_210040000" && zone.Contains(insidePvpZone));
		Assert.DoesNotContain(pvpZones, zone => zone.Contains(outsidePvpZone));
		CreaturePvpZoneRevalidationService.Revalidate(
			player.ObjectId,
			player.Position,
			dataManager.StaticData.CreaturePvpZones,
			zoneCounterService);
		Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(player.ObjectId));

		player.Position = insidePvpZone;
		player.PortAnimation = ArrivalAnimation.FadeInBeam;
		await pair.Connection.HandleLevelReadyAsync(player);
		var counters = zoneCounterService.GetCounters(player.ObjectId);

		Assert.Equal(1, counters.PvpZoneCount);
		Assert.Equal(0, counters.SiegeZoneCount);
		Assert.Equal(ArrivalAnimation.None, player.PortAnimation);
	}

	[Fact]
	public async Task HandleTeleportAnimationDoneAsync_CompletesPendingTeleportAndRevalidatesCreaturePvpZoneCounters()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry, runtimeContext, zoneCounterService);
		var insidePvpZone = new WorldPosition(210040000, 2700, 620, 150, 0);
		var outsidePvpZone = new WorldPosition(210040000, 100, 100, 150, 0);
		var player = CreateTeleportingPlayer(7309, insidePvpZone);
		var pvpZones = dataManager.StaticData.CreaturePvpZones.GetZonesByMapId(insidePvpZone.WorldId);
		Assert.Contains(pvpZones, zone => zone.Name == "PVP_87_210040000" && zone.Contains(insidePvpZone));
		Assert.DoesNotContain(pvpZones, zone => zone.Contains(outsidePvpZone));
		CreaturePvpZoneRevalidationService.Revalidate(
			player.ObjectId,
			player.Position,
			dataManager.StaticData.CreaturePvpZones,
			zoneCounterService);
		Assert.Equal(1, zoneCounterService.GetCounters(player.ObjectId).PvpZoneCount);

		PlayerTeleportService.QueuePendingTeleport(player, outsidePvpZone);
		var left = await pair.Connection.HandleTeleportAnimationDoneAsync(player);
		var leftCounters = zoneCounterService.GetCounters(player.ObjectId);
		PlayerTeleportService.QueuePendingTeleport(player, insidePvpZone);
		var reentered = await pair.Connection.HandleTeleportAnimationDoneAsync(player);
		var repeated = await pair.Connection.HandleTeleportAnimationDoneAsync(player);

		Assert.NotNull(left);
		Assert.Equal(outsidePvpZone, left.Destination);
		Assert.Equal(CreaturePvpZoneCounters.Empty, leftCounters);
		Assert.NotNull(reentered);
		Assert.Equal(insidePvpZone, reentered.Destination);
		Assert.Equal(insidePvpZone, player.Position);
		Assert.Null(repeated);
		Assert.Null(player.PendingTeleport);
		var counters = zoneCounterService.GetCounters(player.ObjectId);
		Assert.Equal(1, counters.PvpZoneCount);
		Assert.Equal(0, counters.SiegeZoneCount);
	}

	[Fact]
	public async Task HandleTeleportAnimationDoneAsync_CompletesPendingTeleportAndRevalidatesCreatureSiegeZoneCounters()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry, runtimeContext, zoneCounterService);
		var insideSiegeZone = new WorldPosition(210050000, 1750, 2150, 300, 0);
		var outsideSiegeZone = new WorldPosition(210050000, 100, 100, 300, 0);
		var player = CreateTeleportingPlayer(7310, insideSiegeZone);
		var siegeZones = dataManager.StaticData.CreaturePvpZones.GetZonesByMapId(insideSiegeZone.WorldId);
		Assert.Contains(siegeZones, zone => zone.Name == "ABYSS_CASTLE_AREA_2011_210050000"
			&& zone.ZoneType == CreaturePvpZoneType.Siege
			&& zone.Contains(insideSiegeZone));
		Assert.DoesNotContain(siegeZones, zone => zone.Contains(outsideSiegeZone));
		CreaturePvpZoneRevalidationService.Revalidate(
			player.ObjectId,
			player.Position,
			dataManager.StaticData.CreaturePvpZones,
			zoneCounterService);
		Assert.Equal(1, zoneCounterService.GetCounters(player.ObjectId).SiegeZoneCount);

		PlayerTeleportService.QueuePendingTeleport(player, outsideSiegeZone);
		var left = await pair.Connection.HandleTeleportAnimationDoneAsync(player);
		var leftCounters = zoneCounterService.GetCounters(player.ObjectId);
		PlayerTeleportService.QueuePendingTeleport(player, insideSiegeZone);
		var reentered = await pair.Connection.HandleTeleportAnimationDoneAsync(player);
		var counters = zoneCounterService.GetCounters(player.ObjectId);

		Assert.NotNull(left);
		Assert.Equal(outsideSiegeZone, left.Destination);
		Assert.Equal(CreaturePvpZoneCounters.Empty, leftCounters);
		Assert.NotNull(reentered);
		Assert.Equal(insideSiegeZone, reentered.Destination);
		Assert.Equal(insideSiegeZone, player.Position);
		Assert.Null(player.PendingTeleport);
		Assert.Equal(1, counters.SiegeZoneCount);
		Assert.Equal(0, counters.PvpZoneCount);
	}

	[Fact]
	public async Task QueueDelayedTeleportAsync_SendsTeleportLocAndPendingStateCompletesOnAnimationDone()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry, runtimeContext, zoneCounterService);
		var startPosition = new WorldPosition(210040000, 100, 100, 150, 0);
		var destination = new WorldPosition(210040000, 2700, 620, 150, 32);
		var player = CreateTeleportingPlayer(7311, startPosition);
		Assert.Contains(
			dataManager.StaticData.CreaturePvpZones.GetZonesByMapId(destination.WorldId),
			zone => zone.Name == "PVP_87_210040000" && zone.Contains(destination));

		var request = await pair.Connection.QueueDelayedTeleportAsync(
			player,
			destination,
			TeleportAnimation.FadeOutBeam,
			dataManager.StaticData);
		var teleportPacket = Assert.IsType<SmTeleportLoc>(request.Packet);
		using var teleportReader = new PacketBuffer(SerializeUnencryptedPayload(teleportPacket));
		var queuedPosition = player.Position;
		var completed = await pair.Connection.HandleTeleportAnimationDoneAsync(player);

		Assert.Equal(destination, request.PendingTeleport.Destination);
		Assert.Equal(TeleportAnimation.FadeOutBeam, request.PendingTeleport.Animation);
		Assert.Equal(destination, player.Position);
		Assert.Equal(startPosition, queuedPosition);
		Assert.NotNull(completed);
		Assert.Equal(startPosition, completed.PreviousPosition);
		Assert.Equal(destination, completed.Destination);
		Assert.Null(player.PendingTeleport);
		Assert.Equal(ArrivalAnimation.None, player.PortAnimation);
		var despawn = Assert.Single(registry.Broadcasts, broadcast => broadcast.Packet is SmDelete);
		Assert.Equal(startPosition, despawn.SourcePosition);
		using var deleteReader = new PacketBuffer(SerializeUnencryptedPayload(despawn.Packet));
		Assert.Equal(player.ObjectId, deleteReader.ReadD());
		Assert.Equal((byte)ObjectDeleteAnimation.FadeOutBeam, deleteReader.ReadC());
		Assert.Equal(0, deleteReader.Remaining);
		Assert.Equal(1, zoneCounterService.GetCounters(player.ObjectId).PvpZoneCount);
		Assert.Equal(TeleportAnimation.FadeOutBeam.Id, teleportReader.ReadC());
		Assert.Equal(destination.WorldId, teleportReader.ReadD());
		Assert.Equal(destination.WorldId, teleportReader.ReadD());
		Assert.Equal(destination.X, teleportReader.ReadF());
		Assert.Equal(destination.Y, teleportReader.ReadF());
		Assert.Equal(destination.Z, teleportReader.ReadF());
		Assert.Equal(destination.Heading, teleportReader.ReadC());
		Assert.Equal(0, teleportReader.Remaining);
	}

	[Fact]
	public async Task QueueDelayedTeleportAsync_MapInstanceChangeKeepsArrivalAnimationUntilLevelReady()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry, runtimeContext, zoneCounterService);
		var startPosition = new WorldPosition(210040000, 100, 100, 150, 0, InstanceId: 1);
		var destination = new WorldPosition(210040000, 2700, 620, 150, 32, InstanceId: 2);
		var player = CreateTeleportingPlayer(7312, startPosition);

		await pair.Connection.QueueDelayedTeleportAsync(
			player,
			destination,
			TeleportAnimation.FadeOutBeam,
			dataManager.StaticData);
		Assert.IsType<SmTeleportLoc>(Assert.Single(pair.SentPackets));
		var completed = await pair.Connection.HandleTeleportAnimationDoneAsync(player);

		Assert.NotNull(completed);
		Assert.False(completed.UsesSameWorldSpawnPath);
		Assert.Equal(destination, player.Position);
		Assert.Equal(ArrivalAnimation.FadeInBeam, player.PortAnimation);
		Assert.Equal(1, zoneCounterService.GetCounters(player.ObjectId).PvpZoneCount);
		Assert.Collection(
			pair.SentPackets.Skip(1),
			packet => Assert.IsType<SmChannelInfo>(packet),
			packet => Assert.IsType<SmPlayerSpawn>(packet));

		await pair.Connection.HandleLevelReadyAsync(player);

		Assert.Equal(ArrivalAnimation.None, player.PortAnimation);
		Assert.Equal(1, zoneCounterService.GetCounters(player.ObjectId).PvpZoneCount);
		Assert.Collection(
			pair.SentPackets.Skip(3),
			packet => Assert.IsType<SmPlayerInfo>(packet),
			packet => Assert.IsType<SmAccountProperties>(packet),
			packet => Assert.IsType<SmMotion>(packet),
			packet => Assert.IsType<SmCubeUpdate>(packet));
	}

	[Fact]
	public async Task CompleteToyPetSpawnUseItemAsync_RevalidatesCreaturePvpZoneCountersForSpawnedKisk()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var registry = new CapturingConnectionRegistry();
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var idFactory = new IDFactory();
		await using var pair = await TestConnectionPair.CreateAsync(
			registry,
			runtimeContext,
			zoneCounterService,
			idFactory,
			world);
		var spawnPosition = new WorldPosition(210040000, 2700, 620, 150, 0);
		var player = CreateTeleportingPlayer(7304, spawnPosition);
		var sourceItem = new InventoryItem
		{
			ObjectId = 9101,
			ItemId = 184000011,
			Count = 1,
			OwnerId = player.ObjectId,
			Location = 0,
			Slot = 4,
		};
		player.InventoryItems = [sourceItem];
		var sourceTemplate = CreateToyPetSpawnItemTemplate(sourceItem.ItemId, toyPetSpawnNpcId: 700273);
		var kiskTemplate = CreateKiskTemplate(700273);
		Assert.Contains(
			dataManager.StaticData.CreaturePvpZones.GetZonesByMapId(spawnPosition.WorldId),
			zone => zone.Name == "PVP_87_210040000" && zone.Contains(spawnPosition));

		await pair.Connection.CompleteToyPetSpawnUseItemAsync(
			player,
			sourceItem.ObjectId,
			sourceTemplate,
			kiskTemplate,
			CancellationToken.None);

		Assert.True(world.TryGetObject(1, out var kiskObject));
		var kiskNpc = Assert.IsType<WorldNpc>(kiskObject);
		Assert.Equal(spawnPosition.WorldId, kiskNpc.Position.WorldId);
		Assert.Equal(spawnPosition.X, kiskNpc.Position.X);
		Assert.Equal(spawnPosition.Y, kiskNpc.Position.Y);
		Assert.Equal(spawnPosition.Z, kiskNpc.Position.Z);
		var counters = zoneCounterService.GetCounters(kiskNpc.ObjectId);
		Assert.Equal(1, counters.PvpZoneCount);
		Assert.Equal(0, counters.SiegeZoneCount);
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == sourceItem.ObjectId);
		Assert.NotNull(runtimeContext.Kisks.GetKiskState(kiskNpc.ObjectId));
	}

	[Fact]
	public async Task CompleteToyPetSpawnUseItemAsync_ClearsCreaturePvpZoneCountersWhenSourceMutationFailsAfterKiskSpawn()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var registry = new CapturingConnectionRegistry();
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var idFactory = new IDFactory();
		var playerEnterWorldService = new PlayerEnterWorldService(
			new GameServerOptions(),
			new EmptyPlayerEnterWorldRepository { SaveItemUseSourceMutationResult = false },
			world,
			NullLogger<PlayerEnterWorldService>.Instance);
		await using var pair = await TestConnectionPair.CreateAsync(
			registry,
			runtimeContext,
			zoneCounterService,
			idFactory,
			world,
			playerEnterWorldService);
		var spawnPosition = new WorldPosition(210040000, 2700, 620, 150, 0);
		var player = CreateTeleportingPlayer(7306, spawnPosition);
		var sourceItem = new InventoryItem
		{
			ObjectId = 9102,
			ItemId = 184000011,
			Count = 1,
			OwnerId = player.ObjectId,
			Location = 0,
			Slot = 4,
		};
		player.InventoryItems = [sourceItem];
		var sourceTemplate = CreateToyPetSpawnItemTemplate(sourceItem.ItemId, toyPetSpawnNpcId: 700273);
		var kiskTemplate = CreateKiskTemplate(700273);
		Assert.Contains(
			dataManager.StaticData.CreaturePvpZones.GetZonesByMapId(spawnPosition.WorldId),
			zone => zone.Name == "PVP_87_210040000" && zone.Contains(spawnPosition));

		await pair.Connection.CompleteToyPetSpawnUseItemAsync(
			player,
			sourceItem.ObjectId,
			sourceTemplate,
			kiskTemplate,
			CancellationToken.None);
		var reusedId = idFactory.NextId();

		Assert.False(world.TryGetObject(1, out _));
		Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(1));
		Assert.NotNull(player.InventoryItems.SingleOrDefault(item => item.ObjectId == sourceItem.ObjectId));
		Assert.Null(runtimeContext.Kisks.GetKiskState(1));
		Assert.Empty(registry.RefreshedNpcs);
		Assert.Equal(1, reusedId);
	}

	[Fact]
	public async Task CompleteToyPetSpawnUseItemAsync_ClearsCreatureSiegeZoneCountersWhenSourceMutationFailsAfterKiskSpawn()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var registry = new CapturingConnectionRegistry();
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var idFactory = new IDFactory();
		var playerEnterWorldService = new PlayerEnterWorldService(
			new GameServerOptions(),
			new EmptyPlayerEnterWorldRepository { SaveItemUseSourceMutationResult = false },
			world,
			NullLogger<PlayerEnterWorldService>.Instance);
		await using var pair = await TestConnectionPair.CreateAsync(
			registry,
			runtimeContext,
			zoneCounterService,
			idFactory,
			world,
			playerEnterWorldService);
		var spawnPosition = new WorldPosition(210050000, 1750, 2150, 300, 0);
		var player = CreateTeleportingPlayer(7307, spawnPosition);
		var sourceItem = new InventoryItem
		{
			ObjectId = 9103,
			ItemId = 184000011,
			Count = 1,
			OwnerId = player.ObjectId,
			Location = 0,
			Slot = 4,
		};
		player.InventoryItems = [sourceItem];
		var sourceTemplate = CreateToyPetSpawnItemTemplate(sourceItem.ItemId, toyPetSpawnNpcId: 700273);
		var kiskTemplate = CreateKiskTemplate(700273);
		Assert.Contains(
			dataManager.StaticData.CreaturePvpZones.GetZonesByMapId(spawnPosition.WorldId),
			zone => zone.Name == "ABYSS_CASTLE_AREA_2011_210050000"
				&& zone.ZoneType == CreaturePvpZoneType.Siege
				&& zone.Contains(spawnPosition));

		await pair.Connection.CompleteToyPetSpawnUseItemAsync(
			player,
			sourceItem.ObjectId,
			sourceTemplate,
			kiskTemplate,
			CancellationToken.None);

		Assert.False(world.TryGetObject(1, out _));
		Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(1));
		Assert.NotNull(player.InventoryItems.SingleOrDefault(item => item.ObjectId == sourceItem.ObjectId));
		Assert.Null(runtimeContext.Kisks.GetKiskState(1));
		Assert.Empty(registry.RefreshedNpcs);
	}

	[Fact]
	public async Task RemoveRuntimeKiskAsync_ClearsCreaturePvpZoneCountersWithoutConnectionRegistry()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var idFactory = new IDFactory([1]);
		await using var pair = await TestConnectionPair.CreateAsync(
			registry: null,
			runtimeContext,
			zoneCounterService,
			idFactory,
			world);
		var kiskState = new PlayerKiskRuntimeState(
			objectId: 1,
			ownerObjectId: 7308,
			npcId: 700273);
		runtimeContext.Kisks.RegisterKisk(kiskState);
		var spawnPosition = new WorldPosition(210040000, 2700, 620, 150, 0);
		var kiskTemplate = CreateKiskTemplate(700273);
		var kiskNpc = new WorldNpc(1, 700273, kiskTemplate, spawnPosition);
		Assert.True(world.TryAddObject(kiskNpc.ObjectId, kiskNpc));
		CreaturePvpZoneRevalidationService.Revalidate(
			kiskNpc.ObjectId,
			kiskNpc.Position,
			dataManager.StaticData.CreaturePvpZones,
			zoneCounterService);
		Assert.Equal(1, zoneCounterService.GetCounters(kiskNpc.ObjectId).PvpZoneCount);

		await pair.Connection.RemoveRuntimeKiskAsync(kiskNpc.ObjectId);

		Assert.False(world.TryGetObject(kiskNpc.ObjectId, out _));
		Assert.False(runtimeContext.Kisks.HaveKisk(kiskState.OwnerObjectId));
		Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(kiskNpc.ObjectId));
		Assert.Equal(kiskNpc.ObjectId, idFactory.NextId());
	}

	[Fact]
	public async Task SpawnAndDismissPostmanAsync_RevalidatesAndClearsCreaturePvpZoneCounters()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var registry = new CapturingConnectionRegistry();
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var idFactory = new IDFactory();
		await using var pair = await TestConnectionPair.CreateAsync(
			registry,
			runtimeContext,
			zoneCounterService,
			idFactory,
			world);
		var postmanPosition = new WorldPosition(210040000, 2700, 620, 150, 0);
		var player = CreateTeleportingPlayer(7305, postmanPosition with { X = postmanPosition.X - 7 });
		Assert.Contains(
			dataManager.StaticData.CreaturePvpZones.GetZonesByMapId(postmanPosition.WorldId),
			zone => zone.Name == "PVP_87_210040000" && zone.Contains(postmanPosition));

		await pair.Connection.SpawnPostmanAsync(player);

		var postman = Assert.IsType<PostmanNpc>(player.Postman);
		Assert.True(world.TryGetObject(postman.ObjectId, out var worldObject));
		Assert.Same(postman, worldObject);
		Assert.Equal(postmanPosition.WorldId, postman.Position.WorldId);
		Assert.Equal(postmanPosition.X, postman.Position.X);
		Assert.Equal(postmanPosition.Y, postman.Position.Y);
		Assert.Equal(postmanPosition.Z, postman.Position.Z);
		var counters = zoneCounterService.GetCounters(postman.ObjectId);
		Assert.Equal(1, counters.PvpZoneCount);
		Assert.Equal(0, counters.SiegeZoneCount);

		await pair.Connection.DismissPostmanAsync(player, notifyClient: false);
		var staleLeave = zoneCounterService.ApplyZoneLeave(postman.ObjectId, "PVP_87_210040000", CreaturePvpZoneCounterType.Pvp);

		Assert.Null(player.Postman);
		Assert.False(player.HasSummonedPostman);
		Assert.False(world.TryGetObject(postman.ObjectId, out _));
		Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(postman.ObjectId));
		Assert.Equal(CreaturePvpZoneMembershipTransitionStatus.NotInside, staleLeave.Status);
	}

	[Fact]
	public async Task LeavePlayerWorldAsync_ClearsCreaturePvpZoneCounters()
	{
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry, creaturePvpZoneCounterService: zoneCounterService);
		var player = CreateTeleportingPlayer(7303, new WorldPosition(210040000, 2700, 620, 150, 0));
		zoneCounterService.ApplyZoneEnter(player.ObjectId, "PVP_87_210040000", CreaturePvpZoneCounterType.Pvp);
		zoneCounterService.ApplyZoneEnter(player.ObjectId, "FORT_210040000", CreaturePvpZoneCounterType.Siege);
		Assert.Equal(new CreaturePvpZoneCounters(SiegeZoneCount: 1, PvpZoneCount: 1), zoneCounterService.GetCounters(player.ObjectId));

		await pair.Connection.LeavePlayerWorldAsync(player, notifyPostmanClient: false);
		var staleLeave = zoneCounterService.ApplyZoneLeave(player.ObjectId, "PVP_87_210040000", CreaturePvpZoneCounterType.Pvp);

		Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(player.ObjectId));
		Assert.Equal(CreaturePvpZoneMembershipTransitionStatus.NotInside, staleLeave.Status);
		Assert.Contains(registry.Broadcasts, broadcast => broadcast.SourceObjectId == player.ObjectId && broadcast.Packet is SmDelete);
	}

	private static Player CreateFlyingPlayer(int objectId)
	{
		var player = new Player
		{
			ObjectId = objectId,
			Name = $"flight-{objectId}",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 10,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(CurrentHp: 111, CurrentMp: 205, CurrentFp: 55),
			IsInsideFlyZone = true,
		};
		player.SetFlyState(PlayerFlyState.Flying);
		player.SetCreatureState(PlayerCreatureState.Flying, enabled: true);
		return player;
	}

	private static Player CreateTeleportingPlayer(int objectId, WorldPosition position)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = $"teleport-zone-{objectId}",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 10,
			Position = position,
			LifeStats = new PlayerLifeStats(CurrentHp: 111, CurrentMp: 205, CurrentFp: 55),
		};
	}

	private static ItemTemplateSummary CreateToyPetSpawnItemTemplate(int itemId, int toyPetSpawnNpcId)
	{
		return new ItemTemplateSummary(
			itemId,
			"kisk item",
			DescriptionId: 0,
			Mask: 0,
			Level: 1,
			ItemGroup: "NONE",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: 0,
			ValidEquipmentSlots: 0,
			ToyPetSpawnNpcId: toyPetSpawnNpcId);
	}

	private static NpcTemplateSummary CreateKiskTemplate(int npcId)
	{
		return new NpcTemplateSummary(
			npcId,
			"test_kisk",
			NameId: npcId + 100,
			Level: 10,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "PC_LIGHT_CASTLE_DOOR",
			Tribe: "KISK",
			Type: "NPC",
			MaxHp: 1000,
			Height: 2.5f,
			BoundRadius: 1.2f,
			State: WorldNpcState.DefaultSpawnState,
			KiskStats: new KiskStatsSummary(UseMask: 0, MaxMembers: 6, MaxResurrects: 18));
	}

	private static void AssertEmotion(GameServerPacket packet, int expectedObjectId, EmotionType expectedEmotion)
	{
		var emotion = Assert.IsType<SmEmotion>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(emotion));
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal((int)expectedEmotion, (int)reader.ReadC());
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "game-server", "data", "static_data", "static_data.xml")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<GameServerPacket> PacketOrder { get; } = [];

		public List<BroadcastRecord> Broadcasts { get; } = [];

		public List<IWorldNpcObject> RefreshedNpcs { get; } = [];

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
			PacketOrder.Add(packet);
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
			Broadcasts.Add(new BroadcastRecord(sourcePosition, sourceObjectId, packet, includeSourcePlayer));
			PacketOrder.Add(packet);
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
			RefreshedNpcs.AddRange(npcs);
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

	private sealed record BroadcastRecord(
		WorldPosition SourcePosition,
		int SourceObjectId,
		GameServerPacket Packet,
		bool IncludeSourcePlayer);

	private sealed class TestConnectionPair : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private TestConnectionPair(
			TcpClient client,
			GameServerConnection connection,
			List<GameServerPacket> sentPackets)
		{
			_client = client;
			Connection = connection;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<TestConnectionPair> CreateAsync(
			IGameClientConnectionRegistry? registry,
			GameServerRuntimeContext? runtimeContext = null,
			CreaturePvpZoneCounterService? creaturePvpZoneCounterService = null,
			IDFactory? idFactory = null,
			GameWorld? world = null,
			PlayerEnterWorldService? playerEnterWorldService = null)
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
					"flight-zone-fanout-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					runtimeContext: runtimeContext,
					connectionRegistry: registry,
					idFactory: idFactory,
					world: world,
					playerEnterWorldService: playerEnterWorldService,
					creaturePvpZoneCounterService: creaturePvpZoneCounterService,
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
