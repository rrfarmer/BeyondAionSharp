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
using GameWorld = Aion.GameServer.World.World;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionInstanceCooldownTests
{
	[Fact]
	public async Task ApplyInstanceEntranceCooldownAsync_SendsJavaEntryInfoPacketToOwner()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(
			new GameServerOptions
			{
				Membership = new GameServerMembershipOptions { InstancesCooldown = 10 },
				Instance = new GameServerInstanceOptions { CooldownRate = 2 },
			},
			new PlayerEnterWorldService(
				new GameServerOptions(),
				repository,
				new GameWorld(NullLogger<GameWorld>.Instance),
				NullLogger<PlayerEnterWorldService>.Instance));
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			AccountMembership = 10,
		};
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", MaxCount: 5, CoolTimeType: "RELATIVE", EntCoolTime: 30),
		]);

		var result = await pair.Connection.ApplyInstanceEntranceCooldownAsync(
			player,
			300030000,
			reenter: false,
			cooltimes,
			now);

		Assert.True(result.Added);
		var savedCooldowns = repository.SavedPortalCooldowns;
		Assert.NotNull(savedCooldowns);
		var savedCooldown = Assert.Single(savedCooldowns);
		Assert.Equal(300030000, savedCooldown.Key);
		Assert.Equal(result.ReuseTimeMillis, savedCooldown.Value.ReuseTimeMillis);
		Assert.Equal(1, savedCooldown.Value.EntryCount);
		var packet = Assert.IsType<SmInstanceInfo>(Assert.Single(pair.SentPackets));
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(2, (int)reader.ReadC());
		Assert.Equal(8, reader.ReadD());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(8, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(900, reader.ReadD());
		Assert.Equal(5, reader.ReadD());
		Assert.Equal(-1, reader.ReadD());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal("Character", reader.ReadS());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public async Task ApplyInstanceEntranceCooldownAsync_SkipsSendWhenJavaAddGuardIsFalse()
	{
		await using var pair = await TestConnectionPair.CreateAsync(new GameServerOptions());
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
		};
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", MaxCount: 5, CoolTimeType: "RELATIVE", EntCoolTime: 30),
		]);

		var result = await pair.Connection.ApplyInstanceEntranceCooldownAsync(
			player,
			300030000,
			reenter: true,
			cooltimes,
			DateTimeOffset.FromUnixTimeMilliseconds(100_000));

		Assert.False(result.Added);
		Assert.Empty(pair.SentPackets);
		Assert.Empty(player.PortalCooldowns);
	}

	[Fact]
	public async Task QueueInstancePortalTransferAsync_SendsTeleportBeforeCooldownLikeJavaPortalTransfer()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(
			new GameServerOptions
			{
				Membership = new GameServerMembershipOptions { InstancesCooldown = 10 },
				Instance = new GameServerInstanceOptions { CooldownRate = 1 },
			},
			new PlayerEnterWorldService(
				new GameServerOptions(),
				repository,
				new GameWorld(NullLogger<GameWorld>.Instance),
				NullLogger<PlayerEnterWorldService>.Instance));
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			AccountMembership = 10,
			Position = new WorldPosition(110010000, 1, 1, 1, 0, 0),
		};
		var destination = new WorldPosition(300030000, 10, 20, 30, 90, 2);
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", MaxCount: 5, CoolTimeType: "RELATIVE", EntCoolTime: 30),
		]);
		var result = await pair.Connection.QueueInstancePortalTransferAsync(
			player,
			destination,
			reenter: false,
			cooltimes,
			TeleportAnimation.FadeOutBeam,
			staticData: null,
			now);

		Assert.Equal(destination, result.Teleport.PendingTeleport.Destination);
		Assert.True(result.Cooldown.Added);
		Assert.Equal(now.AddMinutes(30).ToUnixTimeMilliseconds(), result.Cooldown.ReuseTimeMillis);
		Assert.Collection(
			pair.SentPackets,
			packet => Assert.IsType<SmTeleportLoc>(packet),
			packet => Assert.IsType<SmInstanceInfo>(packet));
		var savedCooldowns = repository.SavedPortalCooldowns;
		Assert.NotNull(savedCooldowns);
		var savedCooldown = Assert.Single(savedCooldowns);
		Assert.Equal(300030000, savedCooldown.Key);
		Assert.Equal(result.Cooldown.ReuseTimeMillis, savedCooldown.Value.ReuseTimeMillis);
		Assert.Equal(1, savedCooldown.Value.EntryCount);
	}

	[Fact]
	public async Task QueueAllocatedInstancePortalTransferAsync_AllocatesRegistersAndTransfers()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(
			new GameServerOptions
			{
				Membership = new GameServerMembershipOptions { InstancesCooldown = 10 },
				Instance = new GameServerInstanceOptions { CooldownRate = 1 },
			},
			new PlayerEnterWorldService(
				new GameServerOptions(),
				repository,
				new GameWorld(NullLogger<GameWorld>.Instance),
				NullLogger<PlayerEnterWorldService>.Instance));
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			AccountMembership = 10,
			Position = new WorldPosition(110010000, 1, 1, 1, 0, 0),
		};
		var worldMaps = new WorldMapRuntimeStateTable(
		[
			new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1),
		]);
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", MaxCount: 5, CoolTimeType: "RELATIVE", EntCoolTime: 30),
		]);
		var portalLocation = new WorldPosition(300030000, 10, 20, 30, 90, InstanceId: 1);
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);

		var result = await pair.Connection.QueueAllocatedInstancePortalTransferAsync(
			player,
			portalLocation,
			reenter: false,
			worldMaps,
			cooltimes,
			ownerId: player.ObjectId,
			maxPlayers: 6,
			TeleportAnimation.FadeOutBeam,
			staticData: null,
			now);

		Assert.Equal(2, result.RuntimePlan.Instance.InstanceId);
		Assert.Equal(player.ObjectId, result.RuntimePlan.Instance.OwnerId);
		Assert.Equal(6, result.RuntimePlan.Instance.MaxPlayers);
		Assert.True(result.RuntimePlan.Instance.IsRegistered(player.ObjectId));
		Assert.Equal(portalLocation with { InstanceId = 2 }, result.RuntimePlan.Destination);
		Assert.Equal(result.RuntimePlan.Destination, result.Transfer.Teleport.PendingTeleport.Destination);
		Assert.True(result.Transfer.Cooldown.Added);
		Assert.Collection(
			pair.SentPackets,
			packet => Assert.IsType<SmTeleportLoc>(packet),
			packet => Assert.IsType<SmInstanceInfo>(packet));
	}

	[Fact]
	public async Task QueuePortalContinueTransferAsync_OpenWorldQueuesTeleportWithoutCooldown()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(
			new GameServerOptions(),
			new PlayerEnterWorldService(
				new GameServerOptions(),
				repository,
				new GameWorld(NullLogger<GameWorld>.Instance),
				NullLogger<PlayerEnterWorldService>.Instance));
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			Position = new WorldPosition(110010000, 1, 1, 1, 0),
		};
		var worldMaps = new WorldMapRuntimeStateTable([new WorldMapSummary(210010000, IsInstance: false, TwinCount: 1)]);
		var cooltimes = new InstanceCooltimeTable(Array.Empty<InstanceCooltimeSummary>());
		var portalLoc = new PortalLocSummary(210010000, LocId: 1, 10, 20, 30, 90);
		var preparation = PortalEntryPreparationResult.Ready(
			PortalEntryPlanResult.Allowed(portalLoc, registeredInstance: null, reenter: false),
			requirementApplication: null,
			Array.Empty<GameServerPacket>());

		var result = await pair.Connection.QueuePortalContinueTransferAsync(
			player,
			preparation,
			worldMapStates: worldMaps,
			instanceCooltimes: cooltimes,
			now: DateTimeOffset.FromUnixTimeMilliseconds(100_000));

		Assert.NotNull(result);
		Assert.Equal(PortalContinueTransferKind.OpenWorld, result.Kind);
		Assert.Equal(new WorldPosition(210010000, 10, 20, 30, 90), result.Teleport!.PendingTeleport.Destination);
		Assert.Null(result.Cooldown);
		Assert.Null(repository.SavedPortalCooldowns);
		Assert.Collection(pair.SentPackets, packet => Assert.IsType<SmTeleportLoc>(packet));
	}

	[Fact]
	public async Task QueuePortalContinueTransferAsync_SoloInstanceAllocatesRegistersAndAppliesCooldownAfterTeleport()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(
			new GameServerOptions
			{
				Membership = new GameServerMembershipOptions { InstancesCooldown = 10 },
				Instance = new GameServerInstanceOptions { CooldownRate = 1 },
			},
			new PlayerEnterWorldService(
				new GameServerOptions(),
				repository,
				new GameWorld(NullLogger<GameWorld>.Instance),
				NullLogger<PlayerEnterWorldService>.Instance));
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			AccountMembership = 10,
			Position = new WorldPosition(110010000, 1, 1, 1, 0),
		};
		var worldMaps = new WorldMapRuntimeStateTable([new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1)]);
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				Id: 8,
				WorldId: 300030000,
				Race: "PC_ALL",
				MaxCount: 5,
				MaxMemberLight: 1,
				MaxMemberDark: 1,
				CoolTimeType: "RELATIVE",
				EntCoolTime: 30),
		]);
		var portalLoc = new PortalLocSummary(300030000, LocId: 1, 10, 20, 30, 90);
		var preparation = PortalEntryPreparationResult.Ready(
			PortalEntryPlanResult.Allowed(portalLoc, registeredInstance: null, reenter: false),
			requirementApplication: null,
			Array.Empty<GameServerPacket>());
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);

		var result = await pair.Connection.QueuePortalContinueTransferAsync(
			player,
			preparation,
			worldMapStates: worldMaps,
			instanceCooltimes: cooltimes,
			now: now);

		Assert.NotNull(result);
		Assert.Equal(PortalContinueTransferKind.AllocatedInstance, result.Kind);
		Assert.NotNull(result.AllocatedRuntimePlan);
		Assert.Equal(2, result.AllocatedRuntimePlan.Instance.InstanceId);
		Assert.True(result.AllocatedRuntimePlan.Instance.IsRegistered(player.ObjectId));
		Assert.Equal(new WorldPosition(300030000, 10, 20, 30, 90, InstanceId: 2), result.Teleport!.PendingTeleport.Destination);
		Assert.NotNull(result.Cooldown);
		Assert.True(result.Cooldown.Added);
		Assert.Equal(now.AddMinutes(30).ToUnixTimeMilliseconds(), result.Cooldown.ReuseTimeMillis);
		Assert.Collection(
			pair.SentPackets,
			packet => Assert.IsType<SmTeleportLoc>(packet),
			packet => Assert.IsType<SmInstanceInfo>(packet));
	}

	[Fact]
	public async Task QueuePortalContinueTransferAsync_RegisteredReentryTransfersWithoutCooldown()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(
			new GameServerOptions
			{
				Membership = new GameServerMembershipOptions { InstancesCooldown = 10 },
				Instance = new GameServerInstanceOptions { CooldownRate = 1 },
			},
			new PlayerEnterWorldService(
				new GameServerOptions(),
				repository,
				new GameWorld(NullLogger<GameWorld>.Instance),
				NullLogger<PlayerEnterWorldService>.Instance));
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			AccountMembership = 10,
			Position = new WorldPosition(110010000, 1, 1, 1, 0),
		};
		var worldMaps = new WorldMapRuntimeStateTable([new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1)]);
		var registeredInstance = worldMaps.AddWorldMapInstance(300030000, instanceId: 7, ownerId: player.ObjectId, maxPlayers: 1);
		Assert.NotNull(registeredInstance);
		registeredInstance.Register(player.ObjectId);
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				Id: 8,
				WorldId: 300030000,
				Race: "PC_ALL",
				MaxCount: 5,
				MaxMemberLight: 1,
				MaxMemberDark: 1,
				CoolTimeType: "RELATIVE",
				EntCoolTime: 30),
		]);
		var portalLoc = new PortalLocSummary(300030000, LocId: 1, 10, 20, 30, 90);
		var preparation = PortalEntryPreparationResult.Ready(
			PortalEntryPlanResult.Allowed(portalLoc, registeredInstance, reenter: true),
			requirementApplication: null,
			Array.Empty<GameServerPacket>());

		var result = await pair.Connection.QueuePortalContinueTransferAsync(
			player,
			preparation,
			worldMapStates: worldMaps,
			instanceCooltimes: cooltimes,
			now: DateTimeOffset.FromUnixTimeMilliseconds(100_000));

		Assert.NotNull(result);
		Assert.Equal(PortalContinueTransferKind.RegisteredInstance, result.Kind);
		Assert.Same(registeredInstance, result.RegisteredInstance);
		Assert.Equal(new WorldPosition(300030000, 10, 20, 30, 90, InstanceId: 7), result.Teleport!.PendingTeleport.Destination);
		Assert.NotNull(result.Cooldown);
		Assert.False(result.Cooldown.Added);
		Assert.Null(repository.SavedPortalCooldowns);
		Assert.Collection(pair.SentPackets, packet => Assert.IsType<SmTeleportLoc>(packet));
	}

	[Fact]
	public async Task QueuePortalContinueTransferAsync_RegisteredGroupInstanceTransfersAndAppliesCooldown()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(
			new GameServerOptions(),
			new PlayerEnterWorldService(
				new GameServerOptions(),
				repository,
				new GameWorld(NullLogger<GameWorld>.Instance),
				NullLogger<PlayerEnterWorldService>.Instance));
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			Position = new WorldPosition(110010000, 1, 1, 1, 0),
		};
		var worldMaps = new WorldMapRuntimeStateTable([new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1)]);
		var registeredInstance = worldMaps.AddWorldMapInstance(300030000, instanceId: 7, maxPlayers: 6);
		Assert.NotNull(registeredInstance);
		registeredInstance.RegisterTeamId(88001);
		registeredInstance.AddPlayer(5001);
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				Id: 8,
				WorldId: 300030000,
				Race: "PC_ALL",
				MaxCount: 5,
				MaxMemberLight: 6,
				MaxMemberDark: 6,
				CoolTimeType: "RELATIVE",
				EntCoolTime: 30),
		]);
		var portalLoc = new PortalLocSummary(300030000, LocId: 1, 10, 20, 30, 90);
		var teamPlan = new PortalTeamEntryPlan(
			PortalTeamEntryKind.Group,
			TeamId: 88001,
			MemberObjectIds: [1001, 1002],
			MaxPlayers: 6,
			PortalTeamEntryDisposition.RegisteredInstanceTransfer,
			registeredInstance,
			Reenter: false,
			FanoutSupported: false);
		var preparation = PortalEntryPreparationResult.Ready(
			PortalEntryPlanResult.UnsupportedTeamPortal(portalLoc, teamPlan),
			requirementApplication: null,
			Array.Empty<GameServerPacket>());

		var result = await pair.Connection.QueuePortalContinueTransferAsync(
			player,
			preparation,
			worldMapStates: worldMaps,
			instanceCooltimes: cooltimes,
			now: DateTimeOffset.FromUnixTimeMilliseconds(100_000));

		Assert.NotNull(result);
		Assert.Equal(PortalContinueTransferKind.RegisteredInstance, result.Kind);
		Assert.NotNull(result.Teleport);
		Assert.Equal(new WorldPosition(300030000, 10, 20, 30, 90, InstanceId: 7), result.Teleport.PendingTeleport.Destination);
		Assert.NotNull(result.Cooldown);
		Assert.True(result.Cooldown.Added);
		Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(100_000).AddMinutes(30).ToUnixTimeMilliseconds(), result.Cooldown.ReuseTimeMillis);
		Assert.Same(registeredInstance, result.RegisteredInstance);
		Assert.Same(teamPlan, result.TeamPlan);
		var groupPlan = result.GroupTransferPlan;
		Assert.NotNull(groupPlan);
		Assert.Equal(88001, groupPlan.TeamId);
		Assert.Equal([1001, 1002], groupPlan.MemberObjectIds);
		Assert.Equal(6, groupPlan.MaxPlayers);
		Assert.Equal(GroupPortalTransferState.RegisteredInstanceTransfer, groupPlan.State);
		Assert.Same(registeredInstance, groupPlan.RegisteredInstance);
		Assert.Equal(GroupPortalTransferBlockedReason.GroupFanoutNotImplemented, groupPlan.BlockedReason);
		Assert.Empty(groupPlan.MemberInstanceScanPlan.CandidateObjectIds);
		Assert.Equal(
			GroupPortalMemberInstanceScanState.NotNeededRegisteredTeamInstance,
			groupPlan.MemberInstanceScanPlan.State);
		Assert.Equal(
			GroupPortalMemberInstanceScanBlockedReason.RegisteredTeamInstanceAlreadyResolved,
			groupPlan.MemberInstanceScanPlan.BlockedReason);
		Assert.Equal(6, groupPlan.CapacityPlan.MaxPlayers);
		Assert.Equal(1, groupPlan.CapacityPlan.CurrentPlayerCount);
		Assert.Equal(GroupPortalCapacityState.WouldPassCapacityGuard, groupPlan.CapacityPlan.State);
		Assert.Equal(GroupPortalCapacityBlockedReason.GroupFanoutNotImplemented, groupPlan.CapacityPlan.BlockedReason);
		Assert.Equal(300030000, groupPlan.AllocationPlan.TargetWorldId);
		Assert.Null(groupPlan.AllocationPlan.DifficultyId);
		Assert.Equal(6, groupPlan.AllocationPlan.MaxPlayers);
		Assert.Null(groupPlan.AllocationPlan.IntendedRegisteredTeamId);
		Assert.Equal(GroupPortalAllocationState.NotNeededRegisteredTeamInstance, groupPlan.AllocationPlan.State);
		Assert.Equal(
			GroupPortalAllocationBlockedReason.RegisteredTeamInstanceAlreadyResolved,
			groupPlan.AllocationPlan.BlockedReason);
		Assert.Equal(7, groupPlan.ExecutionPlan.TargetInstanceId);
		Assert.Equal(new WorldPosition(300030000, 10, 20, 30, 90, InstanceId: 7), groupPlan.ExecutionPlan.StartPosition);
		Assert.Equal(1001, groupPlan.ExecutionPlan.PlayerObjectIdToRegister);
		Assert.False(groupPlan.ExecutionPlan.Reenter);
		Assert.Equal(TeleportAnimation.FadeOutBeam, groupPlan.ExecutionPlan.TeleportAnimation);
		Assert.Equal(GroupPortalCooldownPreviewState.WouldAddCooldown, groupPlan.ExecutionPlan.CooldownState);
		Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(100_000).AddMinutes(30).ToUnixTimeMilliseconds(), groupPlan.ExecutionPlan.CooldownReuseTimeMillis);
		Assert.Equal(1, groupPlan.ExecutionPlan.InstanceCooldownRate);
		Assert.True(groupPlan.ExecutionPlan.WouldAddCooldown);
		Assert.Equal(GroupPortalExecutionState.WouldTransferToRegisteredInstance, groupPlan.ExecutionPlan.State);
		Assert.Equal(GroupPortalExecutionBlockedReason.GroupFanoutNotImplemented, groupPlan.ExecutionPlan.BlockedReason);
		Assert.Equal(new WorldPosition(300030000, 10, 20, 30, 90, InstanceId: 7), registeredInstance.StartPosition);
		Assert.True(registeredInstance.IsRegistered(1001));
		Assert.Equal(new WorldPosition(300030000, 10, 20, 30, 90, InstanceId: 7), player.PendingTeleport?.Destination);
		Assert.Collection(
			pair.SentPackets,
			packet => Assert.IsType<SmTeleportLoc>(packet),
			packet => Assert.IsType<SmInstanceInfo>(packet));
		Assert.NotNull(repository.SavedPortalCooldowns);
	}

	[Fact]
	public async Task QueuePortalContinueTransferAsync_RegisteredGroupReentryTransfersWithoutCooldown()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(
			new GameServerOptions(),
			new PlayerEnterWorldService(
				new GameServerOptions(),
				repository,
				new GameWorld(NullLogger<GameWorld>.Instance),
				NullLogger<PlayerEnterWorldService>.Instance));
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			Position = new WorldPosition(110010000, 1, 1, 1, 0),
		};
		var worldMaps = new WorldMapRuntimeStateTable([new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1)]);
		var registeredInstance = worldMaps.AddWorldMapInstance(300030000, instanceId: 7, maxPlayers: 6);
		Assert.NotNull(registeredInstance);
		registeredInstance.RegisterTeamId(88001);
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				Id: 8,
				WorldId: 300030000,
				Race: "PC_ALL",
				MaxCount: 5,
				MaxMemberLight: 6,
				MaxMemberDark: 6,
				CoolTimeType: "RELATIVE",
				EntCoolTime: 30),
		]);
		var portalLoc = new PortalLocSummary(300030000, LocId: 1, 10, 20, 30, 90);
		var teamPlan = new PortalTeamEntryPlan(
			PortalTeamEntryKind.Group,
			TeamId: 88001,
			MemberObjectIds: [1001, 1002],
			MaxPlayers: 6,
			PortalTeamEntryDisposition.RegisteredInstanceTransfer,
			registeredInstance,
			Reenter: true,
			FanoutSupported: false);
		var preparation = PortalEntryPreparationResult.Ready(
			PortalEntryPlanResult.UnsupportedTeamPortal(portalLoc, teamPlan),
			requirementApplication: null,
			Array.Empty<GameServerPacket>());

		var result = await pair.Connection.QueuePortalContinueTransferAsync(
			player,
			preparation,
			worldMapStates: worldMaps,
			instanceCooltimes: cooltimes,
			now: DateTimeOffset.FromUnixTimeMilliseconds(100_000));

		Assert.NotNull(result);
		Assert.Equal(PortalContinueTransferKind.RegisteredInstance, result.Kind);
		Assert.Same(registeredInstance, result.RegisteredInstance);
		Assert.Same(teamPlan, result.TeamPlan);
		Assert.NotNull(result.Teleport);
		Assert.Equal(new WorldPosition(300030000, 10, 20, 30, 90, InstanceId: 7), result.Teleport.PendingTeleport.Destination);
		Assert.NotNull(result.Cooldown);
		Assert.False(result.Cooldown.Added);
		var groupPlan = Assert.IsType<GroupPortalTransferPlan>(result.GroupTransferPlan);
		Assert.Equal(GroupPortalExecutionState.WouldTransferToRegisteredInstance, groupPlan.ExecutionPlan.State);
		Assert.True(groupPlan.ExecutionPlan.Reenter);
		Assert.Equal(GroupPortalCooldownPreviewState.SkippedForReentry, groupPlan.ExecutionPlan.CooldownState);
		Assert.Null(groupPlan.ExecutionPlan.CooldownReuseTimeMillis);
		Assert.Null(groupPlan.ExecutionPlan.InstanceCooldownRate);
		Assert.False(groupPlan.ExecutionPlan.WouldAddCooldown);
		Assert.Collection(pair.SentPackets, packet => Assert.IsType<SmTeleportLoc>(packet));
		Assert.Equal(new WorldPosition(300030000, 10, 20, 30, 90, InstanceId: 7), player.PendingTeleport?.Destination);
		Assert.Null(repository.SavedPortalCooldowns);
		Assert.Equal(new WorldPosition(300030000, 10, 20, 30, 90, InstanceId: 7), registeredInstance.StartPosition);
		Assert.True(registeredInstance.IsRegistered(1001));
	}

	[Fact]
	public async Task QueuePortalContinueTransferAsync_GroupPlanWithoutRegisteredInstanceRecordsAllocationNeededWithoutPackets()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(
			new GameServerOptions(),
			new PlayerEnterWorldService(
				new GameServerOptions(),
				repository,
				new GameWorld(NullLogger<GameWorld>.Instance),
				NullLogger<PlayerEnterWorldService>.Instance));
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			Position = new WorldPosition(110010000, 1, 1, 1, 0),
		};
		var worldMaps = new WorldMapRuntimeStateTable([new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1)]);
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				Id: 8,
				WorldId: 300030000,
				Race: "PC_ALL",
				MaxCount: 5,
				MaxMemberLight: 6,
				MaxMemberDark: 6,
				CoolTimeType: "RELATIVE",
				EntCoolTime: 30),
		]);
		var portalLoc = new PortalLocSummary(300030000, LocId: 1, 10, 20, 30, 90);
		var teamPlan = new PortalTeamEntryPlan(
			PortalTeamEntryKind.Group,
			TeamId: 88001,
			MemberObjectIds: [1001, 1002],
			MaxPlayers: 6,
			PortalTeamEntryDisposition.FreshInstanceAllocationNeeded,
			RegisteredInstance: null,
			Reenter: false,
			FanoutSupported: false);
		var preparation = PortalEntryPreparationResult.Ready(
			PortalEntryPlanResult.UnsupportedTeamPortal(portalLoc, teamPlan),
			requirementApplication: null,
			Array.Empty<GameServerPacket>());

		var result = await pair.Connection.QueuePortalContinueTransferAsync(
			player,
			preparation,
			worldMapStates: worldMaps,
			instanceCooltimes: cooltimes,
			now: DateTimeOffset.FromUnixTimeMilliseconds(100_000));

		Assert.NotNull(result);
		Assert.Equal(PortalContinueTransferKind.UnsupportedTeamPortal, result.Kind);
		Assert.Null(result.Teleport);
		Assert.Null(result.Cooldown);
		Assert.Null(result.RegisteredInstance);
		Assert.Same(teamPlan, result.TeamPlan);
		var groupPlan = result.GroupTransferPlan;
		Assert.NotNull(groupPlan);
		Assert.Equal(88001, groupPlan.TeamId);
		Assert.Equal([1001, 1002], groupPlan.MemberObjectIds);
		Assert.Equal(6, groupPlan.MaxPlayers);
		Assert.Equal(GroupPortalTransferState.FreshInstanceAllocationNeeded, groupPlan.State);
		Assert.Null(groupPlan.RegisteredInstance);
		Assert.Equal(GroupPortalTransferBlockedReason.GroupFanoutNotImplemented, groupPlan.BlockedReason);
		Assert.Equal([1001, 1002], groupPlan.MemberInstanceScanPlan.CandidateObjectIds);
		Assert.Equal(
			GroupPortalMemberInstanceScanState.WouldScanMemberObjectIds,
			groupPlan.MemberInstanceScanPlan.State);
		Assert.Equal(
			GroupPortalMemberInstanceScanBlockedReason.LiveGroupAggregateNotPorted,
			groupPlan.MemberInstanceScanPlan.BlockedReason);
		Assert.Equal(6, groupPlan.CapacityPlan.MaxPlayers);
		Assert.Null(groupPlan.CapacityPlan.CurrentPlayerCount);
		Assert.Equal(GroupPortalCapacityState.UnknownUntilInstanceAllocated, groupPlan.CapacityPlan.State);
		Assert.Equal(GroupPortalCapacityBlockedReason.InstanceAllocationNotPorted, groupPlan.CapacityPlan.BlockedReason);
		Assert.Equal(300030000, groupPlan.AllocationPlan.TargetWorldId);
		Assert.Null(groupPlan.AllocationPlan.DifficultyId);
		Assert.Equal(6, groupPlan.AllocationPlan.MaxPlayers);
		Assert.Equal(88001, groupPlan.AllocationPlan.IntendedRegisteredTeamId);
		Assert.Equal(GroupPortalAllocationState.WouldAllocateAndRegisterTeam, groupPlan.AllocationPlan.State);
		Assert.Equal(GroupPortalAllocationBlockedReason.InstanceAllocationNotPorted, groupPlan.AllocationPlan.BlockedReason);
		Assert.False(worldMaps.GetMap(300030000)!.TryGetWorldMapInstance(instanceId: 2, out _));
		Assert.Null(groupPlan.ExecutionPlan.TargetInstanceId);
		Assert.Equal(new WorldPosition(300030000, 10, 20, 30, 90), groupPlan.ExecutionPlan.StartPosition);
		Assert.Equal(1001, groupPlan.ExecutionPlan.PlayerObjectIdToRegister);
		Assert.False(groupPlan.ExecutionPlan.Reenter);
		Assert.Equal(TeleportAnimation.FadeOutBeam, groupPlan.ExecutionPlan.TeleportAnimation);
		Assert.Equal(GroupPortalCooldownPreviewState.UnknownUntilTransfer, groupPlan.ExecutionPlan.CooldownState);
		Assert.Null(groupPlan.ExecutionPlan.CooldownReuseTimeMillis);
		Assert.Null(groupPlan.ExecutionPlan.InstanceCooldownRate);
		Assert.Null(groupPlan.ExecutionPlan.WouldAddCooldown);
		Assert.Equal(GroupPortalExecutionState.BlockedUntilInstanceAllocation, groupPlan.ExecutionPlan.State);
		Assert.Equal(GroupPortalExecutionBlockedReason.InstanceAllocationNotPorted, groupPlan.ExecutionPlan.BlockedReason);
		Assert.Empty(pair.SentPackets);
		Assert.Null(player.PendingTeleport);
		Assert.Null(repository.SavedPortalCooldowns);
	}

	[Fact]
	public async Task QueuePortalContinueTransferAsync_GroupPlanWithoutTeamIdRecordsMissingTeamIdWithoutPackets()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(
			new GameServerOptions(),
			new PlayerEnterWorldService(
				new GameServerOptions(),
				repository,
				new GameWorld(NullLogger<GameWorld>.Instance),
				NullLogger<PlayerEnterWorldService>.Instance));
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			Position = new WorldPosition(110010000, 1, 1, 1, 0),
		};
		var worldMaps = new WorldMapRuntimeStateTable([new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1)]);
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				Id: 8,
				WorldId: 300030000,
				Race: "PC_ALL",
				MaxCount: 5,
				MaxMemberLight: 6,
				MaxMemberDark: 6,
				CoolTimeType: "RELATIVE",
				EntCoolTime: 30),
		]);
		var portalLoc = new PortalLocSummary(300030000, LocId: 1, 10, 20, 30, 90);
		var teamPlan = new PortalTeamEntryPlan(
			PortalTeamEntryKind.Group,
			TeamId: 0,
			MemberObjectIds: [1001],
			MaxPlayers: 6,
			PortalTeamEntryDisposition.FreshInstanceAllocationNeeded,
			RegisteredInstance: null,
			Reenter: false,
			FanoutSupported: false);
		var preparation = PortalEntryPreparationResult.Ready(
			PortalEntryPlanResult.UnsupportedTeamPortal(portalLoc, teamPlan),
			requirementApplication: null,
			Array.Empty<GameServerPacket>());

		var result = await pair.Connection.QueuePortalContinueTransferAsync(
			player,
			preparation,
			worldMapStates: worldMaps,
			instanceCooltimes: cooltimes,
			now: DateTimeOffset.FromUnixTimeMilliseconds(100_000));

		Assert.NotNull(result);
		Assert.Equal(PortalContinueTransferKind.UnsupportedTeamPortal, result.Kind);
		Assert.Null(result.Teleport);
		Assert.Null(result.Cooldown);
		Assert.Null(result.RegisteredInstance);
		Assert.Same(teamPlan, result.TeamPlan);
		var groupPlan = result.GroupTransferPlan;
		Assert.NotNull(groupPlan);
		Assert.Equal(0, groupPlan.TeamId);
		Assert.Equal([1001], groupPlan.MemberObjectIds);
		Assert.Equal(6, groupPlan.MaxPlayers);
		Assert.Equal(GroupPortalTransferState.InvalidTeamId, groupPlan.State);
		Assert.Null(groupPlan.RegisteredInstance);
		Assert.Equal(GroupPortalTransferBlockedReason.MissingTeamId, groupPlan.BlockedReason);
		Assert.Empty(groupPlan.MemberInstanceScanPlan.CandidateObjectIds);
		Assert.Equal(
			GroupPortalMemberInstanceScanState.BlockedInvalidTeamId,
			groupPlan.MemberInstanceScanPlan.State);
		Assert.Equal(
			GroupPortalMemberInstanceScanBlockedReason.MissingTeamId,
			groupPlan.MemberInstanceScanPlan.BlockedReason);
		Assert.Equal(6, groupPlan.CapacityPlan.MaxPlayers);
		Assert.Null(groupPlan.CapacityPlan.CurrentPlayerCount);
		Assert.Equal(GroupPortalCapacityState.BlockedInvalidTeamId, groupPlan.CapacityPlan.State);
		Assert.Equal(GroupPortalCapacityBlockedReason.MissingTeamId, groupPlan.CapacityPlan.BlockedReason);
		Assert.Equal(300030000, groupPlan.AllocationPlan.TargetWorldId);
		Assert.Null(groupPlan.AllocationPlan.DifficultyId);
		Assert.Equal(6, groupPlan.AllocationPlan.MaxPlayers);
		Assert.Null(groupPlan.AllocationPlan.IntendedRegisteredTeamId);
		Assert.Equal(GroupPortalAllocationState.BlockedInvalidTeamId, groupPlan.AllocationPlan.State);
		Assert.Equal(GroupPortalAllocationBlockedReason.MissingTeamId, groupPlan.AllocationPlan.BlockedReason);
		Assert.False(worldMaps.GetMap(300030000)!.TryGetWorldMapInstance(instanceId: 2, out _));
		Assert.Null(groupPlan.ExecutionPlan.TargetInstanceId);
		Assert.Equal(new WorldPosition(300030000, 10, 20, 30, 90), groupPlan.ExecutionPlan.StartPosition);
		Assert.Null(groupPlan.ExecutionPlan.PlayerObjectIdToRegister);
		Assert.False(groupPlan.ExecutionPlan.Reenter);
		Assert.Equal(TeleportAnimation.FadeOutBeam, groupPlan.ExecutionPlan.TeleportAnimation);
		Assert.Equal(GroupPortalCooldownPreviewState.UnknownUntilTransfer, groupPlan.ExecutionPlan.CooldownState);
		Assert.Null(groupPlan.ExecutionPlan.CooldownReuseTimeMillis);
		Assert.Null(groupPlan.ExecutionPlan.InstanceCooldownRate);
		Assert.Null(groupPlan.ExecutionPlan.WouldAddCooldown);
		Assert.Equal(GroupPortalExecutionState.BlockedInvalidTeamId, groupPlan.ExecutionPlan.State);
		Assert.Equal(GroupPortalExecutionBlockedReason.MissingTeamId, groupPlan.ExecutionPlan.BlockedReason);
		Assert.Empty(pair.SentPackets);
		Assert.Null(player.PendingTeleport);
		Assert.Null(repository.SavedPortalCooldowns);
	}

	[Fact]
	public async Task QueuePortalContinueTransferAsync_CompletesPendingTeleportOnAnimationDone()
	{
		await using var pair = await TestConnectionPair.CreateAsync(new GameServerOptions());
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			Position = new WorldPosition(110010000, 1, 1, 1, 0),
		};
		var worldMaps = new WorldMapRuntimeStateTable([new WorldMapSummary(210010000, IsInstance: false, TwinCount: 1)]);
		var cooltimes = new InstanceCooltimeTable(Array.Empty<InstanceCooltimeSummary>());
		var portalLoc = new PortalLocSummary(210010000, LocId: 1, 10, 20, 30, 90);
		var destination = new WorldPosition(210010000, 10, 20, 30, 90);
		var preparation = PortalEntryPreparationResult.Ready(
			PortalEntryPlanResult.Allowed(portalLoc, registeredInstance: null, reenter: false),
			requirementApplication: null,
			Array.Empty<GameServerPacket>());

		var queued = await pair.Connection.QueuePortalContinueTransferAsync(
			player,
			preparation,
			worldMapStates: worldMaps,
			instanceCooltimes: cooltimes,
			now: DateTimeOffset.FromUnixTimeMilliseconds(100_000));
		var completed = await pair.Connection.HandleTeleportAnimationDoneAsync(player);

		Assert.NotNull(queued);
		Assert.NotNull(completed);
		Assert.Equal(destination, queued.Teleport!.PendingTeleport.Destination);
		Assert.Equal(destination, completed.Destination);
		Assert.Equal(destination, player.Position);
		Assert.Null(player.PendingTeleport);
		Assert.False(completed.UsesSameWorldSpawnPath);
		Assert.Collection(
			pair.SentPackets,
			packet => Assert.IsType<SmTeleportLoc>(packet),
			packet => Assert.IsType<SmChannelInfo>(packet),
			packet => Assert.IsType<SmPlayerSpawn>(packet));
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

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

		public static async Task<TestConnectionPair> CreateAsync(GameServerOptions options)
		{
			return await CreateAsync(options, playerEnterWorldService: null);
		}

		public static async Task<TestConnectionPair> CreateAsync(
			GameServerOptions options,
			PlayerEnterWorldService? playerEnterWorldService)
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
					"instance-cooldown-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: options,
					playerEnterWorldService: playerEnterWorldService,
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
