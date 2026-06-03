using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class VortexInvasionRuntime
{
	private readonly Lock _sync = new();
	private readonly Dictionary<int, VortexInvasionState> _activeInvasions = [];

	public VortexInvasionSnapshot StartInvasion(
		VortexLocationSummary location,
		RiftPortalState? activePortal = null)
	{
		ArgumentNullException.ThrowIfNull(location);

		lock (_sync)
		{
			var state = GetOrCreateState(location);
			state.ActivePortal = activePortal;
			return state.ToSnapshot();
		}
	}

	public bool SetActivePortal(int vortexLocationId, RiftPortalState? activePortal)
	{
		lock (_sync)
		{
			if (!_activeInvasions.TryGetValue(vortexLocationId, out var state))
				return false;

			// Java parity: RiftManager.spawnRift stores the active RVController on
			// VortexLocation after Invasion.startInvasion marks the location active.
			state.ActivePortal = activePortal;
			return true;
		}
	}

	public bool ClearActivePortal(int vortexLocationId)
	{
		lock (_sync)
		{
			if (!_activeInvasions.TryGetValue(vortexLocationId, out var state))
				return false;

			// Java parity: VortexService.despawn unsets VortexLocation.vortexController.
			state.ActivePortal = null;
			return true;
		}
	}

	public VortexStopInvasionResult StopInvasion(int vortexLocationId)
	{
		lock (_sync)
		{
			if (!_activeInvasions.Remove(vortexLocationId, out var state))
			{
				return new VortexStopInvasionResult(
					Stopped: false,
					LocationId: vortexLocationId,
					Status: VortexStopInvasionStatus.MissingInvasion,
					JavaSource: "services/VortexService.stopInvasion");
			}

			var previousSnapshot = state.ToSnapshot();
			var hadActivePortal = state.ActivePortal != null;
			var removedInvaderCount = state.InvaderObjectIds.Count;
			var removedDefenderCount = state.DefenderObjectIds.Count;
			var removedPassedPlayerCount = state.PassedPlayerObjectIds.Count;

			// Java parity: VortexService removes the active invasion entry before
			// DimensionalVortex.stop invokes Invasion.stopInvasion. The full Java method
			// also kills kisks, kicks online invaders, despawns, and respawns PEACE NPCs;
			// this runtime slice only clears metadata for those later side effects.
			state.ActivePortal = null;
			state.InvaderObjectIds.Clear();
			state.DefenderObjectIds.Clear();
			state.PassedPlayerObjectIds.Clear();

			return new VortexStopInvasionResult(
				Stopped: true,
				LocationId: vortexLocationId,
				Status: VortexStopInvasionStatus.Stopped,
				JavaSource: "services/VortexService.stopInvasion -> services/vortex/Invasion.stopInvasion",
				PreviousSnapshot: previousSnapshot,
				StoppedSnapshot: state.ToSnapshot(),
				HadActivePortal: hadActivePortal,
				RemovedInvaderCount: removedInvaderCount,
				RemovedDefenderCount: removedDefenderCount,
				RemovedPassedPlayerCount: removedPassedPlayerCount);
		}
	}

	public bool AddInvader(int vortexLocationId, Player player, bool passedPortal = true)
	{
		ArgumentNullException.ThrowIfNull(player);

		lock (_sync)
		{
			if (!_activeInvasions.TryGetValue(vortexLocationId, out var state))
				return false;

			state.InvaderObjectIds.Add(player.ObjectId);
			if (passedPortal)
				state.PassedPlayerObjectIds.Add(player.ObjectId);
			return true;
		}
	}

	public bool AddDefender(int vortexLocationId, Player player)
	{
		ArgumentNullException.ThrowIfNull(player);

		lock (_sync)
		{
			if (!_activeInvasions.TryGetValue(vortexLocationId, out var state))
				return false;

			return state.DefenderObjectIds.Add(player.ObjectId);
		}
	}

	public bool RecordPortalPass(VortexLocationSummary location, Player player)
	{
		ArgumentNullException.ThrowIfNull(location);
		ArgumentNullException.ThrowIfNull(player);

		lock (_sync)
		{
			if (!_activeInvasions.TryGetValue(location.Id, out var state))
				return false;

			// Java parity: controllers/RVController.acceptRequest records the responder in
			// VortexLocation.vortexController.passedPlayers after teleporting through a vortex rift.
			return state.PassedPlayerObjectIds.Add(player.ObjectId);
		}
	}

	public VortexInvaderJoinResult AddInvaderFromPassedPortal(VortexLocationSummary location, Player player)
	{
		ArgumentNullException.ThrowIfNull(location);
		ArgumentNullException.ThrowIfNull(player);

		lock (_sync)
		{
			if (!_activeInvasions.TryGetValue(location.Id, out var state))
			{
				return new VortexInvaderJoinResult(
					Added: false,
					PlayerObjectId: player.ObjectId,
					LocationId: location.Id,
					HadPassedPortal: false,
					WasAlreadyInvader: false,
					JavaSource: "model/vortex/VortexLocation.onEnterZone");
			}

			var hadPassedPortal = state.PassedPlayerObjectIds.Contains(player.ObjectId);
			var wasAlreadyInvader = state.InvaderObjectIds.Contains(player.ObjectId);
			var added = hadPassedPortal && !wasAlreadyInvader && state.InvaderObjectIds.Add(player.ObjectId);
			return new VortexInvaderJoinResult(
				added,
				player.ObjectId,
				location.Id,
				hadPassedPortal,
				wasAlreadyInvader,
				"model/vortex/VortexLocation.onEnterZone -> services/vortex/Invasion.addPlayer(player, true)");
		}
	}

	public bool IsInvaderPlayer(Player player)
	{
		ArgumentNullException.ThrowIfNull(player);

		lock (_sync)
			return _activeInvasions.Values.Any(state => state.InvaderObjectIds.Contains(player.ObjectId));
	}

	public bool IsDefenderPlayer(Player player)
	{
		ArgumentNullException.ThrowIfNull(player);

		lock (_sync)
			return _activeInvasions.Values.Any(state => state.DefenderObjectIds.Contains(player.ObjectId));
	}

	public VortexInvaderRemovalResult RemoveInvaderPlayer(Player player)
	{
		ArgumentNullException.ThrowIfNull(player);

		lock (_sync)
		{
			foreach (var state in _activeInvasions.Values.OrderBy(state => state.LocationId))
			{
				if (!state.InvaderObjectIds.Remove(player.ObjectId))
					continue;

				// Java parity: services/vortex/Invasion.kickPlayer removes passed-player portal state
				// after VortexService.removeInvaderPlayer finds the active invader.
				var removedPassedPlayer = state.PassedPlayerObjectIds.Remove(player.ObjectId);
				var wasInInvasionWorld = player.Position.WorldId == state.InvasionWorldId;
				var systemMessages = new List<SmSystemMessage>();
				PlayerTeleportResult? teleportResult = null;
				if (player.IsOnline)
				{
					systemMessages.Add(SmSystemMessage.InvasionInvaderKick());
					if (wasInInvasionWorld)
					{
						systemMessages.Add(SmSystemMessage.InvasionDirectPortalOutCompulsion());
						teleportResult = PlayerTeleportService.TeleportToWorldPosition(player, state.HomePoint);
					}
				}

				return new VortexInvaderRemovalResult(
					Removed: true,
					PlayerObjectId: player.ObjectId,
					LocationId: state.LocationId,
					RemovedPassedPlayer: removedPassedPlayer,
					WasOnline: player.IsOnline,
					WasInInvasionWorld: wasInInvasionWorld,
					JavaSource: "services/VortexService.removeInvaderPlayer -> services/vortex/Invasion.kickPlayer",
					SystemMessages: systemMessages,
					TeleportResult: teleportResult,
					PassedPlayerSyncPlan: CreatePassedPlayerSyncPlan(state),
					ActivePortal: state.ActivePortal);
			}
		}

		return new VortexInvaderRemovalResult(
			Removed: false,
			PlayerObjectId: player.ObjectId,
			LocationId: 0,
			RemovedPassedPlayer: false,
			WasOnline: player.IsOnline,
			WasInInvasionWorld: false,
			JavaSource: "services/VortexService.removeInvaderPlayer");
	}

	public VortexDefenderRemovalResult RemoveDefenderPlayer(Player player)
	{
		ArgumentNullException.ThrowIfNull(player);

		lock (_sync)
		{
			foreach (var state in _activeInvasions.Values.OrderBy(state => state.LocationId))
			{
				if (!state.DefenderObjectIds.Remove(player.ObjectId))
					continue;

				// Java parity: services/vortex/Invasion.kickPlayer(player, false) removes the
				// defender participant and sends only the defender kick system message when online.
				var removedPassedPlayer = state.PassedPlayerObjectIds.Remove(player.ObjectId);
				var systemMessages = player.IsOnline
					? new[] { SmSystemMessage.InvasionDefenderKick() }
					: Array.Empty<SmSystemMessage>();

				return new VortexDefenderRemovalResult(
					Removed: true,
					PlayerObjectId: player.ObjectId,
					LocationId: state.LocationId,
					RemovedPassedPlayer: removedPassedPlayer,
					WasOnline: player.IsOnline,
					JavaSource: "services/VortexService.removeDefenderPlayer -> services/vortex/Invasion.kickPlayer",
					SystemMessages: systemMessages,
					PassedPlayerSyncPlan: CreatePassedPlayerSyncPlan(state),
					ActivePortal: state.ActivePortal);
			}
		}

		return new VortexDefenderRemovalResult(
			Removed: false,
			PlayerObjectId: player.ObjectId,
			LocationId: 0,
			RemovedPassedPlayer: false,
			WasOnline: player.IsOnline,
			JavaSource: "services/VortexService.removeDefenderPlayer");
	}

	public VortexInvasionSnapshot? GetSnapshot(int vortexLocationId)
	{
		lock (_sync)
			return _activeInvasions.TryGetValue(vortexLocationId, out var state)
				? state.ToSnapshot()
				: null;
	}

	private VortexInvasionState GetOrCreateState(VortexLocationSummary location)
	{
		if (_activeInvasions.TryGetValue(location.Id, out var state))
			return state;

		state = new VortexInvasionState(location.Id, location.HomePoint, location.StartPoint);
		_activeInvasions.Add(location.Id, state);
		return state;
	}

	private static VortexPassedPlayerSyncPlan CreatePassedPlayerSyncPlan(VortexInvasionState state)
	{
		// Java parity: controllers/RVController.syncPassed(true) sets usedEntries to
		// passedPlayers.size(), then RiftInformer sends SM_RIFT_ANNOUNCE entry updates.
		return new VortexPassedPlayerSyncPlan(
			state.LocationId,
			state.PassedPlayerObjectIds.Count,
			UsePassedPlayerCount: true,
			"services/vortex/Invasion.kickPlayer -> controllers/RVController.syncPassed(true)");
	}

	private sealed class VortexInvasionState(
		int locationId,
		WorldPosition homePoint,
		WorldPosition startPoint)
	{
		public int LocationId { get; } = locationId;
		public WorldPosition HomePoint { get; } = homePoint;
		public WorldPosition StartPoint { get; } = startPoint;
		public int InvasionWorldId => StartPoint.WorldId;
		public HashSet<int> InvaderObjectIds { get; } = [];
		public HashSet<int> DefenderObjectIds { get; } = [];
		public HashSet<int> PassedPlayerObjectIds { get; } = [];
		public RiftPortalState? ActivePortal { get; set; }

		public VortexInvasionSnapshot ToSnapshot()
		{
			return new VortexInvasionSnapshot(
				LocationId,
				HomePoint,
				StartPoint,
				InvaderObjectIds.Order().ToArray(),
				DefenderObjectIds.Order().ToArray(),
				PassedPlayerObjectIds.Order().ToArray(),
				ActivePortal);
		}
	}
}

public sealed record VortexInvasionSnapshot(
	int LocationId,
	WorldPosition HomePoint,
	WorldPosition StartPoint,
	IReadOnlyList<int> InvaderObjectIds,
	IReadOnlyList<int> DefenderObjectIds,
	IReadOnlyList<int> PassedPlayerObjectIds,
	RiftPortalState? ActivePortal = null)
{
	public bool HasActivePortal => ActivePortal != null;
}

public sealed record VortexInvaderRemovalResult(
	bool Removed,
	int PlayerObjectId,
	int LocationId,
	bool RemovedPassedPlayer,
	bool WasOnline,
	bool WasInInvasionWorld,
	string JavaSource,
	IReadOnlyList<SmSystemMessage>? SystemMessages = null,
	PlayerTeleportResult? TeleportResult = null,
	VortexPassedPlayerSyncPlan? PassedPlayerSyncPlan = null,
	RiftPortalState? ActivePortal = null)
{
	public bool HasActivePortal => ActivePortal != null;
}

public sealed record VortexInvaderJoinResult(
	bool Added,
	int PlayerObjectId,
	int LocationId,
	bool HadPassedPortal,
	bool WasAlreadyInvader,
	string JavaSource);

public sealed record VortexDefenderRemovalResult(
	bool Removed,
	int PlayerObjectId,
	int LocationId,
	bool RemovedPassedPlayer,
	bool WasOnline,
	string JavaSource,
	IReadOnlyList<SmSystemMessage>? SystemMessages = null,
	VortexPassedPlayerSyncPlan? PassedPlayerSyncPlan = null,
	RiftPortalState? ActivePortal = null)
{
	public bool HasActivePortal => ActivePortal != null;
}

public sealed record VortexPassedPlayerSyncPlan(
	int LocationId,
	int PassedPlayerCount,
	bool UsePassedPlayerCount,
	string JavaSource);

public enum VortexStopInvasionStatus
{
	MissingInvasion,
	Stopped,
}

public sealed record VortexStopInvasionResult(
	bool Stopped,
	int LocationId,
	VortexStopInvasionStatus Status,
	string JavaSource,
	VortexInvasionSnapshot? PreviousSnapshot = null,
	VortexInvasionSnapshot? StoppedSnapshot = null,
	bool HadActivePortal = false,
	int RemovedInvaderCount = 0,
	int RemovedDefenderCount = 0,
	int RemovedPassedPlayerCount = 0);
