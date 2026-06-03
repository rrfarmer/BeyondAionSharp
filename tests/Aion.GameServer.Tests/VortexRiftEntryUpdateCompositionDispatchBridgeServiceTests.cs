using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class VortexRiftEntryUpdateCompositionDispatchBridgeServiceTests
{
	[Fact]
	public async Task DispatchAsync_DisabledBridgeDoesNotCallDispatchAdapter()
	{
		var composition = CreateReadyCompositionPlan();
		var registry = new RecordingConnectionRegistry();
		var service = new VortexRiftEntryUpdateCompositionDispatchBridgeService(registry, enabled: false);

		var result = await service.DispatchAsync(composition);

		Assert.Equal(VortexRiftEntryUpdateCompositionDispatchBridgeStatus.DisabledNoDispatch, result.Status);
		Assert.False(result.IsEnabled);
		Assert.False(result.DidCallDispatch);
		Assert.False(result.SendsPackets);
		Assert.Null(result.DispatchResult);
		Assert.Equal([100, 101], result.TargetPlayerObjectIds);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task DispatchAsync_EnabledBridgeDelegatesReadyCompositionToExistingDispatchAdapter()
	{
		var composition = CreateReadyCompositionPlan();
		var registry = new RecordingConnectionRegistry();
		var service = new VortexRiftEntryUpdateCompositionDispatchBridgeService(registry, enabled: true);

		var result = await service.DispatchAsync(composition);

		Assert.Equal(VortexRiftEntryUpdateCompositionDispatchBridgeStatus.Delegated, result.Status);
		Assert.True(result.IsEnabled);
		Assert.True(result.DidCallDispatch);
		Assert.True(result.SendsPackets);
		Assert.NotNull(result.DispatchResult);
		var dispatch = result.DispatchResult;
		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.Completed, dispatch.Status);
		Assert.Equal(2, dispatch.SentCount);
		Assert.Equal([100, 101], registry.SentPackets.Select(packet => packet.PlayerObjectId).ToArray());
		Assert.All(registry.SentPackets, packet => Assert.Same(composition.Packet, packet.Packet));
	}

	[Fact]
	public async Task DispatchAsync_EnabledBridgeSurfacesMissingRegistryFromDispatchAdapter()
	{
		var composition = CreateReadyCompositionPlan();
		var service = new VortexRiftEntryUpdateCompositionDispatchBridgeService(connectionRegistry: null, enabled: true);

		var result = await service.DispatchAsync(composition);

		Assert.Equal(VortexRiftEntryUpdateCompositionDispatchBridgeStatus.Delegated, result.Status);
		Assert.True(result.DidCallDispatch);
		Assert.False(result.SendsPackets);
		Assert.NotNull(result.DispatchResult);
		var dispatch = result.DispatchResult;
		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.MissingRegistry, dispatch.Status);
		Assert.Equal([100, 101], dispatch.Targets.Select(target => target.PlayerObjectId).ToArray());
		Assert.All(dispatch.Targets, target => Assert.False(target.AttemptedSend));
	}

	[Fact]
	public async Task DispatchAsync_MissingOrNotReadyCompositionDoesNotCallDispatchAdapter()
	{
		var registry = new RecordingConnectionRegistry();
		var service = new VortexRiftEntryUpdateCompositionDispatchBridgeService(registry, enabled: true);

		var missing = await service.DispatchAsync(compositionPlan: null);
		var notReady = await service.DispatchAsync(CreateNoTargetCompositionPlan());

		Assert.Equal(VortexRiftEntryUpdateCompositionDispatchBridgeStatus.MissingComposition, missing.Status);
		Assert.Equal(VortexRiftEntryUpdateCompositionDispatchBridgeStatus.NotReady, notReady.Status);
		Assert.False(missing.DidCallDispatch);
		Assert.False(notReady.DidCallDispatch);
		Assert.Empty(missing.TargetPlayerObjectIds);
		Assert.Empty(notReady.TargetPlayerObjectIds);
		Assert.Empty(registry.SentPackets);
	}

	private static VortexRiftEntryUpdateCompositionPlan CreateReadyCompositionPlan()
	{
		var entryUpdate = CreateEntryUpdate();
		var worldTargetPlan = VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(entryUpdate.Portal, isMasterController: true);
		var playerTargetPlan = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(
			worldTargetPlan,
			[
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000),
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(101, 120080000),
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(102, 400010000),
			]);
		return VortexRiftEntryUpdateCompositionPlanService.CreatePlan(
			entryUpdate,
			worldTargetPlan,
			playerTargetPlan);
	}

	private static VortexRiftEntryUpdateCompositionPlan CreateNoTargetCompositionPlan()
	{
		var entryUpdate = CreateEntryUpdate();
		var worldTargetPlan = VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(entryUpdate.Portal, isMasterController: true);
		var playerTargetPlan = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(
			worldTargetPlan,
			[new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 400010000)]);
		return VortexRiftEntryUpdateCompositionPlanService.CreatePlan(
			entryUpdate,
			worldTargetPlan,
			playerTargetPlan);
	}

	private static VortexPassedPlayerSyncRiftEntryUpdateResult CreateEntryUpdate()
	{
		return VortexPassedPlayerSyncRiftEntryUpdateService.CreatePlan(
			new VortexPassedPlayerSyncPlan(
				LocationId: 0,
				PassedPlayerCount: 2,
				UsePassedPlayerCount: true,
				"controllers/RVController.syncPassed(true)"),
			CreateVortexPortal(),
			() => DateTimeOffset.FromUnixTimeSeconds(2000));
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
