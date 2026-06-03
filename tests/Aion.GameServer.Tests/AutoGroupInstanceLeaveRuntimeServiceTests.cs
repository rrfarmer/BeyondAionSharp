using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class AutoGroupInstanceLeaveRuntimeServiceTests
{
	[Fact]
	public void PressEnter_RemovesGroupAndKeepsRegisteredPlayerLikeJavaAutoGroupService()
	{
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var player = CreatePlayer(1001);
		var teammate = CreatePlayer(1002);
		groups.CreateOrUpdateGroup(9001, [player, teammate], PlayerGroupType.AutoGroup);
		var service = new AutoGroupInstanceLeaveRuntimeService(groups, alliances);
		var readyEnterStartTime = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
		var startInstanceTime = readyEnterStartTime.AddMilliseconds(-5);
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.PvpRaceInstance,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [player.ObjectId, teammate.ObjectId],
			InstanceMaskId: 107,
			ReadyEnterStartTime: readyEnterStartTime,
			StartInstanceTime: startInstanceTime));

		var result = service.PressEnter(player, 107);

		Assert.Equal(AutoGroupInstancePressEnterStatus.ReadyToEnter, result.Status);
		Assert.Equal(300110000, result.WorldId);
		Assert.Equal(2, result.InstanceId);
		Assert.True(result.RemovedGroup);
		Assert.False(result.RemovedAlliance);
		Assert.Equal(PlayerTeamMembership.None, player.TeamMembership);
		Assert.False(groups.HasMember(9001, player.ObjectId));
		Assert.True(groups.HasMember(9001, teammate.ObjectId));
		Assert.NotNull(result.Snapshot);
		Assert.Equal(107, result.Snapshot.InstanceMaskId);
		Assert.Equal(readyEnterStartTime, result.Snapshot.ReadyEnterStartTime);
		Assert.Equal(startInstanceTime, result.Snapshot.StartInstanceTime);
		Assert.Contains(player.ObjectId, result.Snapshot.RegisteredPlayerObjectIds);
		Assert.Contains(teammate.ObjectId, result.Snapshot.RegisteredPlayerObjectIds);
	}

	[Fact]
	public void PressEnter_MissingMaskOrUnregisteredPlayerIsNoOpLikeJavaGetAutoInstanceNull()
	{
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var player = CreatePlayer(1001);
		var teammate = CreatePlayer(1002);
		groups.CreateOrUpdateGroup(9001, [player, teammate], PlayerGroupType.AutoGroup);
		var service = new AutoGroupInstanceLeaveRuntimeService(groups, alliances);
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.PvpRaceInstance,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [teammate.ObjectId],
			InstanceMaskId: 107));

		var missingMask = service.PressEnter(player, 108);
		var unregisteredPlayer = service.PressEnter(player, 107);

		Assert.Equal(AutoGroupInstancePressEnterStatus.NoAutoInstance, missingMask.Status);
		Assert.Equal(AutoGroupInstancePressEnterStatus.NoAutoInstance, unregisteredPlayer.Status);
		Assert.Equal(PlayerTeamMembership.Group, player.TeamMembership);
		Assert.True(groups.HasMember(9001, player.ObjectId));
		Assert.True(groups.HasMember(9001, teammate.ObjectId));
		Assert.Contains(teammate.ObjectId, service.GetSnapshot(300110000, 2)!.RegisteredPlayerObjectIds);
	}

	[Fact]
	public void CancelEnter_UnregistersPlayerLikeJavaAutoGroupService()
	{
		var service = new AutoGroupInstanceLeaveRuntimeService(
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime());
		var player = CreatePlayer(1001);
		var teammate = CreatePlayer(1002);
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.PvpRaceInstance,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [player.ObjectId, teammate.ObjectId],
			InstanceMaskId: 107));

		var result = service.CancelEnter(player, 107);

		Assert.Equal(AutoGroupInstanceCancelEnterStatus.Unregistered, result.Status);
		Assert.Equal(1, result.RegisteredPlayerCountAfterCancel);
		Assert.NotNull(result.Snapshot);
		Assert.DoesNotContain(player.ObjectId, result.Snapshot.RegisteredPlayerObjectIds);
		Assert.Contains(teammate.ObjectId, result.Snapshot.RegisteredPlayerObjectIds);
		Assert.DoesNotContain(player.ObjectId, service.GetSnapshot(300110000, 2)!.RegisteredPlayerObjectIds);
		var penaltyRefreshIntent = Assert.Single(result.PenaltyRefreshIntents);
		Assert.Equal(player.ObjectId, penaltyRefreshIntent.PlayerObjectId);
		Assert.Equal(TimeSpan.FromMilliseconds(10000), penaltyRefreshIntent.Delay);
	}

	[Fact]
	public void CancelEnter_MissingMaskOrUnregisteredPlayerIsNoOpLikeJavaGetAutoInstanceNull()
	{
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var player = CreatePlayer(1001);
		var teammate = CreatePlayer(1002);
		groups.CreateOrUpdateGroup(9001, [player, teammate], PlayerGroupType.AutoGroup);
		var service = new AutoGroupInstanceLeaveRuntimeService(groups, alliances);
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.PvpRaceInstance,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [teammate.ObjectId],
			InstanceMaskId: 107));

		var missingMask = service.CancelEnter(player, 108);
		var unregisteredPlayer = service.CancelEnter(player, 107);

		Assert.Equal(AutoGroupInstanceCancelEnterStatus.NoAutoInstance, missingMask.Status);
		Assert.Equal(AutoGroupInstanceCancelEnterStatus.NoAutoInstance, unregisteredPlayer.Status);
		Assert.Empty(missingMask.PenaltyRefreshIntents);
		Assert.Empty(unregisteredPlayer.PenaltyRefreshIntents);
		Assert.Equal(PlayerTeamMembership.Group, player.TeamMembership);
		Assert.True(groups.HasMember(9001, player.ObjectId));
		Assert.Contains(teammate.ObjectId, service.GetSnapshot(300110000, 2)!.RegisteredPlayerObjectIds);
	}

	[Fact]
	public void GetActiveInstanceMaskIds_ReturnsRegisteredAutoInstanceMasksLikeJavaValuesLoop()
	{
		var service = new AutoGroupInstanceLeaveRuntimeService(
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime());
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.PvpRaceInstance,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [1001],
			InstanceMaskId: 107));
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300120000,
			3,
			AutoGroupInstanceKind.PvpRaceInstance,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [2001],
			InstanceMaskId: 108));

		var masks = service.GetActiveInstanceMaskIds();

		Assert.Equal([107, 108], masks.Order());
	}

	[Fact]
	public void TryAddOpenQuickEntry_AddsQuickPlayerWithinMaximumJoinWindowLikeJavaCheckInstances()
	{
		var service = new AutoGroupInstanceLeaveRuntimeService(
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime());
		var startInstanceTime = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.PvpRaceInstance,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [1001, 2001],
			InstanceMaskId: 107,
			StartInstanceTime: startInstanceTime,
			MaximumJoinTimeMilliseconds: 230000,
			MaxPlayers: 4,
			RegisteredPlayerRacesByObjectId: new Dictionary<int, string>
			{
				[1001] = "ELYOS",
				[2001] = "ASMODIANS",
			}));
		var request = new AutoGroupOpenQuickEntryRequest(
			107,
			LeaderObjectId: 1002,
			MemberObjectIds: [1002],
			Race: "ELYOS",
			AutoGroupEntryRequestType.QuickGroupEntry,
			MaxPlayersForRace: 2);

		var result = service.TryAddOpenQuickEntry(request, startInstanceTime.AddMilliseconds(230000));

		Assert.Equal(AutoGroupOpenQuickEntryStatus.Added, result.Status);
		Assert.Equal(300110000, result.WorldId);
		Assert.Equal(2, result.InstanceId);
		Assert.NotNull(result.Snapshot);
		Assert.Equal(230000, result.Snapshot.MaximumJoinTimeMilliseconds);
		Assert.Equal(4, result.Snapshot.MaxPlayers);
		Assert.Contains(1002, result.Snapshot.RegisteredPlayerObjectIds);
		Assert.Equal("ELYOS", result.Snapshot.RegisteredPlayerRacesByObjectId[1002]);
		Assert.Equal(3, service.GetSnapshot(300110000, 2)!.RegisteredPlayerObjectIds.Count);
	}

	[Fact]
	public void TryAddOpenQuickEntry_RejectsAfterMaximumJoinWindowLikeJavaIsRegistrationDisabled()
	{
		var service = new AutoGroupInstanceLeaveRuntimeService(
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime());
		var startInstanceTime = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.PvpRaceInstance,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [1001],
			InstanceMaskId: 107,
			StartInstanceTime: startInstanceTime,
			MaximumJoinTimeMilliseconds: 230000,
			MaxPlayers: 4,
			RegisteredPlayerRacesByObjectId: new Dictionary<int, string>
			{
				[1001] = "ELYOS",
			}));
		var request = new AutoGroupOpenQuickEntryRequest(
			107,
			LeaderObjectId: 1002,
			MemberObjectIds: [1002],
			Race: "ELYOS",
			AutoGroupEntryRequestType.QuickGroupEntry,
			MaxPlayersForRace: 2);

		var result = service.TryAddOpenQuickEntry(request, startInstanceTime.AddMilliseconds(230001));

		Assert.Equal(AutoGroupOpenQuickEntryStatus.RegistrationDisabledByMaximumJoinTime, result.Status);
		Assert.NotNull(result.Snapshot);
		Assert.DoesNotContain(1002, result.Snapshot.RegisteredPlayerObjectIds);
		Assert.Single(service.GetSnapshot(300110000, 2)!.RegisteredPlayerObjectIds);
	}

	[Fact]
	public void TryAddOpenQuickEntry_RejectsReadyEnterTaskAndRaceOverCapacityLikeJava()
	{
		var service = new AutoGroupInstanceLeaveRuntimeService(
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime());
		var startInstanceTime = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.PvpRaceInstance,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [1001],
			InstanceMaskId: 107,
			StartInstanceTime: startInstanceTime,
			MaximumJoinTimeMilliseconds: 230000,
			MaxPlayers: 4,
			RegisteredPlayerRacesByObjectId: new Dictionary<int, string>
			{
				[1001] = "ELYOS",
			}));
		var onStartEnterTask = new AutoGroupOpenQuickEntryRequest(
			107,
			LeaderObjectId: 1002,
			MemberObjectIds: [1002],
			Race: "ELYOS",
			AutoGroupEntryRequestType.QuickGroupEntry,
			MaxPlayersForRace: 2,
			ReadyEnterStartTime: startInstanceTime.AddSeconds(1));
		var raceFull = onStartEnterTask with { ReadyEnterStartTime = null, MaxPlayersForRace = 1 };

		var onStartResult = service.TryAddOpenQuickEntry(onStartEnterTask, startInstanceTime.AddSeconds(2));
		var raceFullResult = service.TryAddOpenQuickEntry(raceFull, startInstanceTime.AddSeconds(2));

		Assert.Equal(AutoGroupOpenQuickEntryStatus.OnStartEnterTask, onStartResult.Status);
		Assert.Null(onStartResult.Snapshot);
		Assert.Equal(AutoGroupOpenQuickEntryStatus.RaceFull, raceFullResult.Status);
		Assert.NotNull(raceFullResult.Snapshot);
		Assert.DoesNotContain(1002, service.GetSnapshot(300110000, 2)!.RegisteredPlayerObjectIds);
	}

	[Fact]
	public void OnLeaveInstance_UnregistersAndRemovesGroupForJavaAutoPvpInstance()
	{
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var player = CreatePlayer(1001);
		var teammate = CreatePlayer(1002);
		groups.CreateOrUpdateGroup(9001, [player, teammate], PlayerGroupType.AutoGroup);
		var service = new AutoGroupInstanceLeaveRuntimeService(groups, alliances);
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.PvpRaceInstance,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [player.ObjectId, teammate.ObjectId]));

		var result = service.OnLeaveInstance(player, 300110000, 2, onlinePlayersInsideAfterLeave: 1);

		Assert.Equal(AutoGroupInstanceLeaveStatus.RegisteredPlayerLeft, result.Plan.Status);
		Assert.True(result.Plan.WouldRemoveGroup);
		Assert.False(result.RemovedFromRegistry);
		Assert.Equal(PlayerTeamMembership.None, player.TeamMembership);
		Assert.False(groups.HasMember(9001, player.ObjectId));
		Assert.True(groups.HasMember(9001, teammate.ObjectId));
		Assert.NotNull(result.SnapshotAfterLeave);
		Assert.DoesNotContain(player.ObjectId, result.SnapshotAfterLeave.RegisteredPlayerObjectIds);
		Assert.Contains(teammate.ObjectId, result.SnapshotAfterLeave.RegisteredPlayerObjectIds);
	}

	[Fact]
	public void OnLeaveInstance_UnregistersAndRemovesAllianceForJavaAutoPvpInstance()
	{
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var player = CreatePlayer(1001);
		var teammate = CreatePlayer(1002);
		alliances.CreateAlliance(9101, player, PlayerAllianceTeamType.AutoAlliance);
		alliances.AddMember(9101, teammate);
		var service = new AutoGroupInstanceLeaveRuntimeService(groups, alliances);
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.PvpRaceInstance,
			QuickRegistrationAllowed: false,
			RegisteredPlayerObjectIds: [player.ObjectId, teammate.ObjectId]));

		var result = service.OnLeaveInstance(player, 300110000, 2, onlinePlayersInsideAfterLeave: 1);

		Assert.True(result.Plan.WouldRemoveAlliance);
		Assert.Equal(PlayerTeamMembership.None, player.TeamMembership);
		Assert.False(alliances.HasMember(9101, player.ObjectId));
		Assert.True(alliances.HasMember(9101, teammate.ObjectId));
		Assert.NotNull(service.GetSnapshot(300110000, 2));
	}

	[Fact]
	public void OnLeaveInstance_RemovesRegistryWhenJavaDestroyIfPossibleWouldDestroy()
	{
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var player = CreatePlayer(1001);
		var service = new AutoGroupInstanceLeaveRuntimeService(groups, alliances);
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.FreeForAllArena,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [player.ObjectId]));

		var result = service.OnLeaveInstance(player, 300110000, 2, onlinePlayersInsideAfterLeave: 0);

		Assert.True(result.Plan.WouldDestroyInstance);
		Assert.True(result.RemovedFromRegistry);
		Assert.Null(service.GetSnapshot(300110000, 2));
		Assert.NotNull(result.SnapshotAfterLeave);
		Assert.Empty(result.SnapshotAfterLeave.RegisteredPlayerObjectIds);
	}

	[Fact]
	public void OnLeaveInstance_InvokesDestroyWorkflowAfterRemovingAutoGroupRegistryLikeJavaDestroyIfPossible()
	{
		var player = CreatePlayer(1001);
		AutoGroupInstanceLeaveRuntimeService? service = null;
		var destroyCalls = new List<(int WorldId, int InstanceId, bool RegistryAlreadyRemoved)>();
		service = new AutoGroupInstanceLeaveRuntimeService(
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime(),
			(worldId, instanceId) =>
			{
				destroyCalls.Add((worldId, instanceId, service!.GetSnapshot(worldId, instanceId) == null));
				return new InstanceDestroyWorkflowResult(
					InstanceDestroyRuntimePlan.Missing(worldId, instanceId),
					UnregisteredTemporarySpawnCount: 0,
					WalkerCleanup: null,
					"test destroy callback");
			});
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.FreeForAllArena,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [player.ObjectId]));

		var result = service.OnLeaveInstance(player, 300110000, 2, onlinePlayersInsideAfterLeave: 0);

		Assert.True(result.Plan.WouldDestroyInstance);
		Assert.True(result.RemovedFromRegistry);
		Assert.NotNull(result.DestroyWorkflowResult);
		var call = Assert.Single(destroyCalls);
		Assert.Equal(300110000, call.WorldId);
		Assert.Equal(2, call.InstanceId);
		Assert.True(call.RegistryAlreadyRemoved);
	}

	[Fact]
	public void DestroyCurrentAutoInstanceIfPossibleOnLogout_ChecksButDoesNotDestroyWhilePlayerStillRegisteredLikeJava()
	{
		var player = CreatePlayer(1001);
		var destroyCalls = new List<(int WorldId, int InstanceId)>();
		var service = new AutoGroupInstanceLeaveRuntimeService(
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime(),
			(worldId, instanceId) =>
			{
				destroyCalls.Add((worldId, instanceId));
				return new InstanceDestroyWorkflowResult(
					InstanceDestroyRuntimePlan.Missing(worldId, instanceId),
					UnregisteredTemporarySpawnCount: 0,
					WalkerCleanup: null,
					"test destroy callback");
			});
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.FreeForAllArena,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [player.ObjectId],
			InstanceMaskId: 107));

		var result = service.DestroyCurrentAutoInstanceIfPossibleOnLogout(
			player,
			300110000,
			2,
			onlinePlayersInsideAtLogout: 0);

		Assert.Equal(AutoGroupLogoutCurrentInstanceDestroyStatus.CheckedNotDestroyed, result.Status);
		Assert.False(result.RemovedFromRegistry);
		Assert.Null(result.DestroyWorkflowResult);
		Assert.Empty(destroyCalls);
		Assert.NotNull(result.Snapshot);
		Assert.Contains(player.ObjectId, result.Snapshot.RegisteredPlayerObjectIds);
		Assert.Contains(player.ObjectId, service.GetSnapshot(300110000, 2)!.RegisteredPlayerObjectIds);
	}

	[Fact]
	public void DestroyCurrentAutoInstanceIfPossibleOnLogout_MissingOrUnregisteredPlayerIsNoOpLikeJavaGuard()
	{
		var player = CreatePlayer(1001);
		var service = new AutoGroupInstanceLeaveRuntimeService(
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime());
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.FreeForAllArena,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [2002],
			InstanceMaskId: 107));

		var missing = service.DestroyCurrentAutoInstanceIfPossibleOnLogout(
			player,
			300110000,
			3,
			onlinePlayersInsideAtLogout: 0);
		var unregistered = service.DestroyCurrentAutoInstanceIfPossibleOnLogout(
			player,
			300110000,
			2,
			onlinePlayersInsideAtLogout: 0);

		Assert.Equal(AutoGroupLogoutCurrentInstanceDestroyStatus.NoAutoInstanceForCurrentMap, missing.Status);
		Assert.Equal(AutoGroupLogoutCurrentInstanceDestroyStatus.PlayerNotRegistered, unregistered.Status);
		Assert.False(missing.RemovedFromRegistry);
		Assert.False(unregistered.RemovedFromRegistry);
		Assert.Contains(2002, service.GetSnapshot(300110000, 2)!.RegisteredPlayerObjectIds);
	}

	[Fact]
	public void OnLeaveInstance_MissingOrUnregisteredInstanceOnlyPlansOpenRegistrationRefresh()
	{
		var service = new AutoGroupInstanceLeaveRuntimeService(
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime(),
			createOpenRegistrationPackets: _ =>
			[
				new Aion.GameServer.Network.Aion.ServerPackets.SmAutoGroup(
					new AutoGroupSummary(
						107,
						300110000,
						NameId: 140107,
						TitleId: 150107,
						MinLevel: 46,
						MaxLevel: 65,
						RegisterQuick: true,
						RegisterGroup: true,
						RegisterNew: true,
						NpcIds: []),
					Aion.GameServer.Network.Aion.ServerPackets.SmAutoGroup.EntryIconWindowId),
			]);
		var player = CreatePlayer(1001);
		service.RegisterInstance(new AutoGroupInstanceRuntimeRegistration(
			300110000,
			2,
			AutoGroupInstanceKind.FreeForAllArena,
			QuickRegistrationAllowed: true,
			RegisteredPlayerObjectIds: [2002]));

		var missing = service.OnLeaveInstance(player, 300110000, 3, onlinePlayersInsideAfterLeave: 0);
		var unregistered = service.OnLeaveInstance(player, 300110000, 2, onlinePlayersInsideAfterLeave: 0);

		Assert.Equal(AutoGroupInstanceLeaveStatus.NoAutoInstanceForMap, missing.Plan.Status);
		Assert.True(missing.Plan.WouldCheckOpenRegistrations);
		Assert.False(missing.RemovedFromRegistry);
		Assert.Equal([107], missing.OpenRegistrationPackets.Select(packet => packet.MaskId));
		Assert.Equal(AutoGroupInstanceLeaveStatus.PlayerNotRegistered, unregistered.Plan.Status);
		Assert.True(unregistered.Plan.WouldCheckOpenRegistrations);
		Assert.Equal([107], unregistered.OpenRegistrationPackets.Select(packet => packet.MaskId));
		Assert.Contains(2002, service.GetSnapshot(300110000, 2)!.RegisteredPlayerObjectIds);
	}

	private static Player CreatePlayer(int objectId)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = $"Player{objectId}",
			Race = "ELYOS",
			Level = 60,
		};
	}
}
