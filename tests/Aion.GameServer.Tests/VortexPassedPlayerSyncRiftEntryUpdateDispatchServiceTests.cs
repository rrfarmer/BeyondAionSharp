using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class VortexPassedPlayerSyncRiftEntryUpdateDispatchServiceTests
{
	[Fact]
	public async Task DispatchAsync_DisabledAdapterRecordsTargetsWithoutCallingRegistry()
	{
		var update = CreateUpdatedPlan();
		var registry = new RecordingConnectionRegistry();
		var service = new VortexPassedPlayerSyncRiftEntryUpdateDispatchService(registry, enabled: false);

		var result = await service.DispatchAsync(update, [1002, 1003]);

		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.DisabledNoSend, result.Status);
		Assert.False(result.SendsPackets);
		Assert.False(result.IsLive);
		Assert.Equal([1002, 1003], result.Targets.Select(target => target.PlayerObjectId).ToArray());
		Assert.All(result.Targets, target =>
		{
			Assert.False(target.AttemptedSend);
			Assert.False(target.SentPacket);
			Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetStatus.NotAttemptedDisabled, target.Status);
		});
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task DispatchAsync_EnabledSendsRiftEntryUpdateToExplicitTargetsInJavaOrder()
	{
		var update = CreateUpdatedPlan();
		var registry = new RecordingConnectionRegistry();
		var service = new VortexPassedPlayerSyncRiftEntryUpdateDispatchService(registry, enabled: true);

		var result = await service.DispatchAsync(update, [1002, 1003]);

		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.Completed, result.Status);
		Assert.True(result.SendsPackets);
		Assert.True(result.IsLive);
		Assert.Equal(2, result.SentCount);
		Assert.Equal([1002, 1003], registry.SentPackets.Select(packet => packet.PlayerObjectId));
		Assert.All(registry.SentPackets, packet => Assert.Same(update.Packet, packet.Packet));
		Assert.Equal(
			[
				VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetStatus.Sent,
				VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetStatus.Sent,
			],
			result.Targets.Select(target => target.Status));
	}

	[Fact]
	public async Task DispatchAsync_EnabledRecordsMissingConnectionWithoutStoppingLaterTargets()
	{
		var update = CreateUpdatedPlan();
		var registry = new RecordingConnectionRegistry(missingPlayerObjectIds: new HashSet<int> { 1002 });
		var service = new VortexPassedPlayerSyncRiftEntryUpdateDispatchService(registry, enabled: true);

		var result = await service.DispatchAsync(update, [1002, 1003]);

		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.Completed, result.Status);
		Assert.Equal(1, result.SentCount);
		Assert.Equal([1002, 1003], registry.SentPackets.Select(packet => packet.PlayerObjectId));
		Assert.Equal(
			[
				VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetStatus.MissingConnection,
				VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetStatus.Sent,
			],
			result.Targets.Select(target => target.Status));
	}

	[Fact]
	public async Task DispatchAsync_EnabledStopsAfterFirstSendExceptionLikeSequentialRiftInformer()
	{
		var update = CreateUpdatedPlan();
		var registry = new RecordingConnectionRegistry(
			exceptions: new Dictionary<int, Queue<Exception>>
			{
				[1002] = new Queue<Exception>([new InvalidOperationException("send failed")]),
			});
		var service = new VortexPassedPlayerSyncRiftEntryUpdateDispatchService(registry, enabled: true);

		var result = await service.DispatchAsync(update, [1002, 1003]);

		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.Completed, result.Status);
		Assert.True(result.StopsAfterFirstFailure);
		Assert.Equal([1002], registry.SentPackets.Select(packet => packet.PlayerObjectId));
		var target = Assert.Single(result.Targets);
		Assert.Equal(1002, target.PlayerObjectId);
		Assert.True(target.AttemptedSend);
		Assert.False(target.SentPacket);
		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetStatus.FailedAndStopped, target.Status);
		Assert.Equal("send failed", target.FailureReason);
	}

	[Fact]
	public async Task DispatchAsync_MissingRegistryRecordsUnsentTargets()
	{
		var update = CreateUpdatedPlan();
		var service = new VortexPassedPlayerSyncRiftEntryUpdateDispatchService(connectionRegistry: null, enabled: true);

		var result = await service.DispatchAsync(update, [1002, 1003]);

		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.MissingRegistry, result.Status);
		Assert.True(result.IsLive);
		Assert.False(result.SendsPackets);
		Assert.Equal([1002, 1003], result.Targets.Select(target => target.PlayerObjectId).ToArray());
		Assert.All(result.Targets, target =>
		{
			Assert.False(target.AttemptedSend);
			Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetStatus.MissingConnection, target.Status);
		});
	}

	[Fact]
	public async Task DispatchAsync_NoUpdateNoPacketOrNoTargetsDoesNotCallRegistry()
	{
		var registry = new RecordingConnectionRegistry();
		var service = new VortexPassedPlayerSyncRiftEntryUpdateDispatchService(registry, enabled: true);

		var noUpdate = await service.DispatchAsync(CreateMissingPortalPlan(), [1002]);
		var noPacket = await service.DispatchAsync(CreateUpdatedPlanWithoutPacketIntent(), [1002]);
		var noTargets = await service.DispatchAsync(CreateUpdatedPlan(), []);

		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.NoUpdate, noUpdate.Status);
		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.NoPacketIntent, noPacket.Status);
		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.NoTargets, noTargets.Status);
		Assert.Empty(noUpdate.Targets);
		Assert.Empty(noPacket.Targets);
		Assert.Empty(noTargets.Targets);
		Assert.Empty(registry.SentPackets);
	}

	private static VortexPassedPlayerSyncRiftEntryUpdateResult CreateUpdatedPlan()
	{
		var syncPlan = new VortexPassedPlayerSyncPlan(
			LocationId: 0,
			PassedPlayerCount: 2,
			UsePassedPlayerCount: true,
			"controllers/RVController.syncPassed(true)");
		var portal = CreateVortexPortal();
		return VortexPassedPlayerSyncRiftEntryUpdateService.CreatePlan(
			syncPlan,
			portal,
			() => DateTimeOffset.FromUnixTimeSeconds(2000));
	}

	private static VortexPassedPlayerSyncRiftEntryUpdateResult CreateMissingPortalPlan()
	{
		var syncPlan = new VortexPassedPlayerSyncPlan(
			LocationId: 0,
			PassedPlayerCount: 2,
			UsePassedPlayerCount: true,
			"controllers/RVController.syncPassed(true)");
		return VortexPassedPlayerSyncRiftEntryUpdateService.CreatePlan(syncPlan, portal: null);
	}

	private static VortexPassedPlayerSyncRiftEntryUpdateResult CreateUpdatedPlanWithoutPacketIntent()
	{
		var update = CreateUpdatedPlan();
		return update with
		{
			Packet = null,
			HasPacketIntent = false,
		};
	}

	private static RiftPortalState CreateVortexPortal()
	{
		var definition = new RiftDefinition(
			1170,
			"MARCHUTAN",
			"MARCHUTAN_AM",
			"MARCHUTAN_AS",
			2,
			45,
			65,
			"ASMODIANS",
			IsVortex: true);
		var template = new NpcTemplateSummary(831143, "Vortex", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC");
		var master = new WorldNpc(
			ObjectId: 7101,
			TemplateId: 831143,
			Template: template,
			Position: new WorldPosition(210060000, 10, 20, 30, 0),
			Anchor: definition.MasterAnchor);
		var slave = new WorldNpc(
			ObjectId: 7102,
			TemplateId: 831144,
			Template: template,
			Position: new WorldPosition(120080000, 40, 50, 60, 0),
			Anchor: definition.SlaveAnchor);

		return new RiftPortalState(definition, master, slave, guardsRequested: false, despawnTimeUnixSeconds: 9200);
	}

	private sealed class RecordingConnectionRegistry : IGameClientConnectionRegistry
	{
		private readonly IReadOnlySet<int> _missingPlayerObjectIds;
		private readonly IReadOnlyDictionary<int, Queue<Exception>> _exceptions;

		public RecordingConnectionRegistry(
			IReadOnlySet<int>? missingPlayerObjectIds = null,
			IReadOnlyDictionary<int, Queue<Exception>>? exceptions = null)
		{
			_missingPlayerObjectIds = missingPlayerObjectIds ?? new HashSet<int>();
			_exceptions = exceptions ?? new Dictionary<int, Queue<Exception>>();
		}

		public List<(int PlayerObjectId, GameServerPacket Packet)> SentPackets { get; } = [];

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
			SentPackets.Add((playerObjectId, packet));
			if (_exceptions.TryGetValue(playerObjectId, out var exceptions) && exceptions.TryDequeue(out var exception))
				return Task.FromException<bool>(exception);

			return Task.FromResult(!_missingPlayerObjectIds.Contains(playerObjectId));
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
			return Task.FromResult(0);
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
}
