using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListMembershipRegistryRefreshAdapterServiceTests
{
	[Fact]
	public void RefreshOwner_DisabledAdapterDoesNotReadRegistry()
	{
		var membership = new PlayerKnownListMembershipService();
		var refresh = new PlayerKnownListMembershipRefreshService(membership);
		var registry = new RecordingConnectionRegistry([CreatePlayer(OwnerPlayerObjectId, 0, 0, 0)]);
		var adapter = new PlayerKnownListMembershipRegistryRefreshAdapterService(refresh, registry, enabled: false);

		var result = adapter.RefreshOwner(CreatePlayer(OwnerPlayerObjectId, 0, 0, 0));

		Assert.Equal(PlayerKnownListMembershipRegistryRefreshAdapterStatus.Disabled, result.Status);
		Assert.False(result.DidReadRegistry);
		Assert.False(result.IsLive);
		Assert.Empty(result.RefreshResults);
		Assert.Equal(0, registry.ForEachOnlinePlayerCalls);
	}

	[Fact]
	public void RefreshOwner_EnabledUsesRegistrySnapshotAndWorldVisibilityApproximation()
	{
		var membership = new PlayerKnownListMembershipService();
		var refresh = new PlayerKnownListMembershipRefreshService(membership);
		var owner = CreatePlayer(OwnerPlayerObjectId, 0, 0, 0);
		var near = CreatePlayer(NearPlayerObjectId, 94, 0, 0);
		var far = CreatePlayer(FarPlayerObjectId, 96, 0, 0);
		var registry = new RecordingConnectionRegistry([owner, near, far]);
		var adapter = new PlayerKnownListMembershipRegistryRefreshAdapterService(refresh, registry, enabled: true);

		var result = adapter.RefreshOwner(owner);

		Assert.Equal(PlayerKnownListMembershipRegistryRefreshAdapterStatus.RefreshedOwner, result.Status);
		Assert.True(result.DidReadRegistry);
		Assert.True(result.IsLive);
		Assert.False(result.IsJavaRegionKnownListParity);
		Assert.Equal(3, result.OnlinePlayerSnapshotCount);
		Assert.Equal(1, registry.ForEachOnlinePlayerCalls);
		Assert.Equal([NearPlayerObjectId], membership.GetKnownPlayerObjectIds(OwnerPlayerObjectId));
	}

	[Fact]
	public void RefreshAll_EnabledRefreshesBidirectionalSnapshotApproximation()
	{
		var membership = new PlayerKnownListMembershipService();
		var refresh = new PlayerKnownListMembershipRefreshService(membership);
		var owner = CreatePlayer(OwnerPlayerObjectId, 0, 0, 0);
		var near = CreatePlayer(NearPlayerObjectId, 94, 0, 0);
		var far = CreatePlayer(FarPlayerObjectId, 200, 0, 0);
		var registry = new RecordingConnectionRegistry([owner, near, far]);
		var adapter = new PlayerKnownListMembershipRegistryRefreshAdapterService(refresh, registry, enabled: true);

		var result = adapter.RefreshAll();

		Assert.Equal(PlayerKnownListMembershipRegistryRefreshAdapterStatus.RefreshedAll, result.Status);
		Assert.Equal(3, result.RefreshResults.Count);
		Assert.Equal([NearPlayerObjectId], membership.GetKnownPlayerObjectIds(OwnerPlayerObjectId));
		Assert.Equal([OwnerPlayerObjectId], membership.GetKnownPlayerObjectIds(NearPlayerObjectId));
		Assert.Empty(membership.GetKnownPlayerObjectIds(FarPlayerObjectId));
	}

	[Fact]
	public void RefreshAll_EnabledWithoutRegistryReturnsMissingRegistry()
	{
		var membership = new PlayerKnownListMembershipService();
		var refresh = new PlayerKnownListMembershipRefreshService(membership);
		var adapter = new PlayerKnownListMembershipRegistryRefreshAdapterService(refresh, connectionRegistry: null, enabled: true);

		var result = adapter.RefreshAll();

		Assert.Equal(PlayerKnownListMembershipRegistryRefreshAdapterStatus.MissingRegistry, result.Status);
		Assert.False(result.DidReadRegistry);
		Assert.True(result.IsJavaRegionKnownListParity is false);
		Assert.Empty(result.RefreshResults);
	}

	private static Player CreatePlayer(
		int objectId,
		float x,
		float y,
		float z,
		int worldId = 210010000) =>
		new()
		{
			ObjectId = objectId,
			Position = new WorldPosition(worldId, x, y, z, Heading: 0),
		};

	private const int OwnerPlayerObjectId = 9101;
	private const int NearPlayerObjectId = 9102;
	private const int FarPlayerObjectId = 9103;

	private sealed class RecordingConnectionRegistry : IGameClientConnectionRegistry
	{
		private readonly IReadOnlyList<Player> _onlinePlayers;

		public RecordingConnectionRegistry(IReadOnlyList<Player> onlinePlayers)
		{
			_onlinePlayers = onlinePlayers;
		}

		public int ForEachOnlinePlayerCalls { get; private set; }

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
			ForEachOnlinePlayerCalls++;
			foreach (var player in _onlinePlayers)
				action(player);
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet) =>
			Task.FromResult(false);

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null) =>
			Task.FromResult(0);

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null) =>
			Task.FromResult(0);

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null) =>
			Task.FromResult(0);

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null) =>
			Task.FromResult(0);

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates) =>
			Task.FromResult(0);

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail) =>
			Task.FromResult(false);

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah) =>
			Task.FromResult(false);
	}
}
