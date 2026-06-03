using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class VortexInvasionRuntime
{
	private readonly Lock _sync = new();
	private readonly Dictionary<int, VortexInvasionState> _activeInvasions = [];

	public VortexInvasionSnapshot StartInvasion(VortexLocationSummary location)
	{
		ArgumentNullException.ThrowIfNull(location);

		lock (_sync)
		{
			var state = GetOrCreateState(location);
			return state.ToSnapshot();
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
				return new VortexInvaderRemovalResult(
					Removed: true,
					PlayerObjectId: player.ObjectId,
					LocationId: state.LocationId,
					RemovedPassedPlayer: removedPassedPlayer,
					WasOnline: player.IsOnline,
					WasInInvasionWorld: player.Position.WorldId == state.InvasionWorldId,
					JavaSource: "services/VortexService.removeInvaderPlayer -> services/vortex/Invasion.kickPlayer");
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
		public HashSet<int> PassedPlayerObjectIds { get; } = [];

		public VortexInvasionSnapshot ToSnapshot()
		{
			return new VortexInvasionSnapshot(
				LocationId,
				HomePoint,
				StartPoint,
				InvaderObjectIds.Order().ToArray(),
				PassedPlayerObjectIds.Order().ToArray());
		}
	}
}

public sealed record VortexInvasionSnapshot(
	int LocationId,
	WorldPosition HomePoint,
	WorldPosition StartPoint,
	IReadOnlyList<int> InvaderObjectIds,
	IReadOnlyList<int> PassedPlayerObjectIds);

public sealed record VortexInvaderRemovalResult(
	bool Removed,
	int PlayerObjectId,
	int LocationId,
	bool RemovedPassedPlayer,
	bool WasOnline,
	bool WasInInvasionWorld,
	string JavaSource);

public sealed record VortexInvaderJoinResult(
	bool Added,
	int PlayerObjectId,
	int LocationId,
	bool HadPassedPortal,
	bool WasAlreadyInvader,
	string JavaSource);
