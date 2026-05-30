using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public enum PlayerKnownListMembershipRegistryRefreshAdapterStatus
{
	Disabled,
	MissingRegistry,
	RefreshedOwner,
	RefreshedAll,
}

public sealed record PlayerKnownListMembershipRegistryRefreshAdapterResult(
	PlayerKnownListMembershipRegistryRefreshAdapterStatus Status,
	IReadOnlyList<PlayerKnownListMembershipRefreshResult> RefreshResults,
	int OnlinePlayerSnapshotCount,
	bool DidReadRegistry,
	bool IsJavaRegionKnownListParity,
	string JavaSource,
	bool IsLive
);

public sealed class PlayerKnownListMembershipRegistryRefreshAdapterService
{
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly PlayerKnownListMembershipRefreshService _refreshService;
	private readonly bool _enabled;

	public PlayerKnownListMembershipRegistryRefreshAdapterService(
		PlayerKnownListMembershipRefreshService refreshService,
		IGameClientConnectionRegistry? connectionRegistry = null,
		bool enabled = false
	)
	{
		_refreshService = refreshService;
		_connectionRegistry = connectionRegistry;
		_enabled = enabled;
	}

	public PlayerKnownListMembershipRegistryRefreshAdapterResult RefreshOwner(Player owner)
	{
		// Java parity: KnownList.update walks live world visibility and region membership. This adapter
		// is only a C# seam that snapshots online players from the connection registry and feeds them into
		// the staged membership refresh service.
		if (!_enabled)
			return CreateResult(PlayerKnownListMembershipRegistryRefreshAdapterStatus.Disabled, [], didReadRegistry: false);

		if (_connectionRegistry == null)
			return CreateResult(PlayerKnownListMembershipRegistryRefreshAdapterStatus.MissingRegistry, [], didReadRegistry: false);

		var onlinePlayers = SnapshotOnlinePlayers();
		var refreshResult = _refreshService.RefreshOwnerFromOnlinePlayers(owner, onlinePlayers);
		return CreateResult(
			PlayerKnownListMembershipRegistryRefreshAdapterStatus.RefreshedOwner,
			[refreshResult],
			didReadRegistry: true,
			onlinePlayers.Count
		);
	}

	public PlayerKnownListMembershipRegistryRefreshAdapterResult RefreshAll()
	{
		// Java parity: this bulk path is still an approximation over a registry snapshot, not a direct
		// MapRegion or KnownList-driven refresh like the Java runtime.
		if (!_enabled)
			return CreateResult(PlayerKnownListMembershipRegistryRefreshAdapterStatus.Disabled, [], didReadRegistry: false);

		if (_connectionRegistry == null)
			return CreateResult(PlayerKnownListMembershipRegistryRefreshAdapterStatus.MissingRegistry, [], didReadRegistry: false);

		var onlinePlayers = SnapshotOnlinePlayers();
		var refreshResults = _refreshService.RefreshAllFromOnlinePlayers(onlinePlayers);
		return CreateResult(
			PlayerKnownListMembershipRegistryRefreshAdapterStatus.RefreshedAll,
			refreshResults,
			didReadRegistry: true,
			onlinePlayers.Count
		);
	}

	private List<Player> SnapshotOnlinePlayers()
	{
		var onlinePlayers = new List<Player>();
		_connectionRegistry!.ForEachOnlinePlayer(onlinePlayers.Add);
		return onlinePlayers;
	}

	private static PlayerKnownListMembershipRegistryRefreshAdapterResult CreateResult(
		PlayerKnownListMembershipRegistryRefreshAdapterStatus status,
		IReadOnlyList<PlayerKnownListMembershipRefreshResult> refreshResults,
		bool didReadRegistry,
		int onlinePlayerSnapshotCount = 0
	) =>
		new(
			status,
			refreshResults,
			onlinePlayerSnapshotCount,
			didReadRegistry,
			IsJavaRegionKnownListParity: false,
			"KnownList.update approximated from IGameClientConnectionRegistry.ForEachOnlinePlayer snapshot plus WorldVisibility; not Java MapRegion parity",
			IsLive: didReadRegistry
		);
}
