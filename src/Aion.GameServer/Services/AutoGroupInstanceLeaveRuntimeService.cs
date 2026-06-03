using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using System.Threading;

namespace Aion.GameServer.Services;

public sealed class AutoGroupInstanceLeaveRuntimeService
{
	private readonly Lock _sync = new();
	private readonly Dictionary<AutoGroupInstanceRuntimeKey, AutoGroupInstanceRuntimeState> _instancesByKey = [];
	private readonly PlayerGroupRuntime _playerGroups;
	private readonly PlayerAllianceRuntime _playerAlliances;
	private readonly Func<int, int, InstanceDestroyWorkflowResult>? _destroyInstance;
	private readonly Func<Player, IReadOnlyList<SmAutoGroup>>? _createOpenRegistrationPackets;
	private readonly bool _autoGroupEnabled;

	public AutoGroupInstanceLeaveRuntimeService(
		PlayerGroupRuntime playerGroups,
		PlayerAllianceRuntime playerAlliances,
		Func<int, int, InstanceDestroyWorkflowResult>? destroyInstance = null,
		Func<Player, IReadOnlyList<SmAutoGroup>>? createOpenRegistrationPackets = null,
		bool autoGroupEnabled = true)
	{
		_playerGroups = playerGroups;
		_playerAlliances = playerAlliances;
		_destroyInstance = destroyInstance;
		_createOpenRegistrationPackets = createOpenRegistrationPackets;
		_autoGroupEnabled = autoGroupEnabled;
	}

	public AutoGroupInstanceRuntimeSnapshot RegisterInstance(AutoGroupInstanceRuntimeRegistration registration)
	{
		lock (_sync)
		{
			var state = new AutoGroupInstanceRuntimeState(
				registration.WorldId,
				registration.InstanceId,
				registration.InstanceMaskId,
				registration.InstanceKind,
				registration.QuickRegistrationAllowed,
				registration.RegisteredPlayerObjectIds,
				registration.ReadyEnterStartTime,
				registration.StartInstanceTime,
				registration.MaximumJoinTimeMilliseconds,
				registration.MaxPlayers,
				registration.RegisteredPlayerRacesByObjectId);
			_instancesByKey[state.Key] = state;
			return state.CreateSnapshot();
		}
	}

	public AutoGroupOpenQuickEntryResult TryAddOpenQuickEntry(
		AutoGroupOpenQuickEntryRequest request,
		DateTimeOffset? now = null)
	{
		var evaluatedAt = now ?? DateTimeOffset.UtcNow;
		lock (_sync)
		{
			if (request.EntryRequestType != AutoGroupEntryRequestType.QuickGroupEntry)
				return AutoGroupOpenQuickEntryResult.NotAdded(
					request,
					AutoGroupOpenQuickEntryStatus.NotQuickEntry,
					snapshot: null,
					"AutoGroupService.checkInstancesForOpenQuickEntries -> lfp.getEntryRequestType() != QUICK_GROUP_ENTRY -> return false");

			if (request.IsOnStartEnterTask(evaluatedAt))
				return AutoGroupOpenQuickEntryResult.NotAdded(
					request,
					AutoGroupOpenQuickEntryStatus.OnStartEnterTask,
					snapshot: null,
					"AutoGroupService.checkInstancesForOpenQuickEntries -> lfp.isOnStartEnterTask() -> return false");

			AutoGroupOpenQuickEntryResult? firstMatchingFailure = null;
			foreach (var state in _instancesByKey.Values)
			{
				if (state.InstanceMaskId != request.InstanceMaskId)
					continue;

				var result = state.TryAddOpenQuickEntry(request, evaluatedAt);
				if (result.Status == AutoGroupOpenQuickEntryStatus.Added)
					return result;

				firstMatchingFailure ??= result;
			}

			return firstMatchingFailure ?? AutoGroupOpenQuickEntryResult.NotAdded(
				request,
				AutoGroupOpenQuickEntryStatus.NoMatchingInstance,
				snapshot: null,
				"AutoGroupService.checkInstancesForOpenQuickEntries -> no autoInstance for mask accepted the quick-entry party");
		}
	}

	public AutoGroupInstanceRuntimeResult OnLeaveInstance(
		Player player,
		int worldId,
		int instanceId,
		int onlinePlayersInsideAfterLeave)
	{
		AutoGroupInstanceRuntimeResult result;
		AutoGroupInstanceRuntimeKey? destroyKey = null;
		lock (_sync)
		{
			var key = new AutoGroupInstanceRuntimeKey(worldId, instanceId);
			_instancesByKey.TryGetValue(key, out var state);
			var facts = state == null
				? CreateMissingInstanceFacts(player)
				: state.CreateLeaveFacts(player, _autoGroupEnabled, onlinePlayersInsideAfterLeave);
			var plan = AutoGroupInstanceLeavePlanService.CreatePlan(facts);
			var removedFromRegistry = false;

			if (state != null && plan.WouldUnregisterPlayer)
				state.Unregister(player.ObjectId);

			if (plan.WouldRemoveGroup)
				_playerGroups.RemoveMember(player);
			else if (plan.WouldRemoveAlliance)
				_playerAlliances.RemoveMember(player);

			if (state != null && plan.WouldDestroyInstance)
			{
				removedFromRegistry = _instancesByKey.Remove(key);
				if (removedFromRegistry)
					destroyKey = key;
			}

			result = new AutoGroupInstanceRuntimeResult(
				plan,
				state?.CreateSnapshot(),
				removedFromRegistry,
				DestroyWorkflowResult: null,
				OpenRegistrationPackets: Array.Empty<SmAutoGroup>(),
				"AutoGroupService.onLeaveInstance live adapter slice -> planner, unregister, team cleanup, registry removal when destroyIfPossible is true");
		}

		if (destroyKey is { } keyToDestroy && _destroyInstance != null)
		{
			var destroyResult = _destroyInstance(keyToDestroy.WorldId, keyToDestroy.InstanceId);
			result = result with { DestroyWorkflowResult = destroyResult };
		}

		if (result.Plan.WouldCheckOpenRegistrations && _createOpenRegistrationPackets != null)
			result = result with { OpenRegistrationPackets = _createOpenRegistrationPackets(player) };

		return result;
	}

	public AutoGroupInstancePressEnterResult PressEnter(Player player, int instanceMaskId)
	{
		lock (_sync)
		{
			var state = _instancesByKey.Values.FirstOrDefault(candidate =>
				candidate.InstanceMaskId == instanceMaskId && candidate.IsRegistered(player.ObjectId));
			if (state == null)
				return AutoGroupInstancePressEnterResult.Missing(instanceMaskId, player.ObjectId);

			var removedGroup = false;
			var removedAlliance = false;
			if (player.TeamMembership == PlayerTeamMembership.Group)
			{
				removedGroup = _playerGroups.RemoveMember(player) != null;
			}
			if (player.TeamMembership == PlayerTeamMembership.Alliance)
			{
				removedAlliance = _playerAlliances.RemoveMember(player) != null;
			}

			return new AutoGroupInstancePressEnterResult(
				instanceMaskId,
				player.ObjectId,
				AutoGroupInstancePressEnterStatus.ReadyToEnter,
				state.WorldId,
				state.Key.InstanceId,
				removedGroup,
				removedAlliance,
				state.CreateSnapshot(),
				"AutoGroupService.pressEnter -> getAutoInstance(player, mask) -> remove PlayerGroup/PlayerAlliance -> AutoInstance.onPressEnter -> SM_AUTO_GROUP(mask, 5)");
		}
	}

	public AutoGroupInstanceCancelEnterResult CancelEnter(Player player, int instanceMaskId)
	{
		lock (_sync)
		{
			var state = _instancesByKey.Values.FirstOrDefault(candidate =>
				candidate.InstanceMaskId == instanceMaskId && candidate.IsRegistered(player.ObjectId));
			if (state == null)
				return AutoGroupInstanceCancelEnterResult.Missing(instanceMaskId, player.ObjectId);

			var removed = state.Unregister(player.ObjectId);
			return new AutoGroupInstanceCancelEnterResult(
				instanceMaskId,
				player.ObjectId,
				removed
					? AutoGroupInstanceCancelEnterStatus.Unregistered
					: AutoGroupInstanceCancelEnterStatus.NoAutoInstance,
				state.WorldId,
				state.Key.InstanceId,
				state.RegisteredPlayerCount,
				state.CreateSnapshot(),
				"AutoGroupService.cancelEnter -> getAutoInstance(player, mask) -> AutoInstance.unregister(player) -> penalisePlayerAndScheduleRemoval -> destroyOrAddPlayersFromQuickEntries -> SM_AUTO_GROUP(mask, 2)");
		}
	}

	public AutoGroupInstanceRuntimeSnapshot? GetSnapshot(int worldId, int instanceId)
	{
		lock (_sync)
			return _instancesByKey.TryGetValue(new AutoGroupInstanceRuntimeKey(worldId, instanceId), out var state)
				? state.CreateSnapshot()
				: null;
	}

	private AutoGroupInstanceLeaveFacts CreateMissingInstanceFacts(Player player)
	{
		return new AutoGroupInstanceLeaveFacts(
			player.ObjectId,
			_autoGroupEnabled,
			HasAutoInstanceForCurrentMap: false,
			IsRegisteredAutoGroupPlayer: false,
			AutoGroupInstanceKind.Base,
			RegisteredPlayerCountBeforeLeave: 0,
			OnlinePlayersInsideAfterLeave: 0,
			QuickRegistrationAllowed: false,
			PlayerIsInGroup: player.TeamMembership == PlayerTeamMembership.Group,
			PlayerIsInAlliance: player.TeamMembership == PlayerTeamMembership.Alliance);
	}
}

public sealed record AutoGroupInstanceRuntimeRegistration(
	int WorldId,
	int InstanceId,
	AutoGroupInstanceKind InstanceKind,
	bool QuickRegistrationAllowed,
	IReadOnlyCollection<int> RegisteredPlayerObjectIds,
	int InstanceMaskId = 0,
	DateTimeOffset? ReadyEnterStartTime = null,
	DateTimeOffset? StartInstanceTime = null,
	byte DifficultyId = 0,
	int MaximumJoinTimeMilliseconds = 0,
	int MaxPlayers = 0,
	IReadOnlyDictionary<int, string>? RegisteredPlayerRacesByObjectId = null);

public sealed record AutoGroupInstanceRuntimeResult(
	AutoGroupInstanceLeavePlan Plan,
	AutoGroupInstanceRuntimeSnapshot? SnapshotAfterLeave,
	bool RemovedFromRegistry,
	InstanceDestroyWorkflowResult? DestroyWorkflowResult,
	IReadOnlyList<SmAutoGroup> OpenRegistrationPackets,
	string JavaSource);

public sealed record AutoGroupInstanceRuntimeSnapshot(
	int WorldId,
	int InstanceId,
	int InstanceMaskId,
	AutoGroupInstanceKind InstanceKind,
	bool QuickRegistrationAllowed,
	IReadOnlySet<int> RegisteredPlayerObjectIds,
	IReadOnlyDictionary<int, string> RegisteredPlayerRacesByObjectId,
	DateTimeOffset? ReadyEnterStartTime,
	DateTimeOffset? StartInstanceTime,
	int MaximumJoinTimeMilliseconds,
	int MaxPlayers);

public sealed record AutoGroupOpenQuickEntryRequest(
	int InstanceMaskId,
	int LeaderObjectId,
	IReadOnlyList<int> MemberObjectIds,
	string Race,
	AutoGroupEntryRequestType EntryRequestType,
	int MaxPlayersForRace,
	DateTimeOffset? ReadyEnterStartTime = null)
{
	public bool IsOnStartEnterTask(DateTimeOffset now)
	{
		return ReadyEnterStartTime.HasValue
			&& now - ReadyEnterStartTime.Value <= TimeSpan.FromMilliseconds(120000);
	}
}

public sealed record AutoGroupOpenQuickEntryResult(
	AutoGroupOpenQuickEntryStatus Status,
	AutoGroupOpenQuickEntryRequest Request,
	int WorldId,
	int InstanceId,
	AutoGroupInstanceRuntimeSnapshot? Snapshot,
	string JavaSource)
{
	public static AutoGroupOpenQuickEntryResult Added(
		AutoGroupOpenQuickEntryRequest request,
		AutoGroupInstanceRuntimeSnapshot snapshot)
	{
		return new AutoGroupOpenQuickEntryResult(
			AutoGroupOpenQuickEntryStatus.Added,
			request,
			snapshot.WorldId,
			snapshot.InstanceId,
			snapshot,
			"AutoGroupService.checkInstancesForOpenQuickEntries -> AutoPvpInstance.addLookingForParty(lfp) == ADDED -> removeSearchEntry, setStartEnterTime, SM_AUTO_GROUP(maskId, 4)");
	}

	public static AutoGroupOpenQuickEntryResult NotAdded(
		AutoGroupOpenQuickEntryRequest request,
		AutoGroupOpenQuickEntryStatus status,
		AutoGroupInstanceRuntimeSnapshot? snapshot,
		string javaSource)
	{
		return new AutoGroupOpenQuickEntryResult(
			status,
			request,
			snapshot?.WorldId ?? 0,
			snapshot?.InstanceId ?? 0,
			snapshot,
			javaSource);
	}
}

public enum AutoGroupOpenQuickEntryStatus
{
	Added,
	NoMatchingInstance,
	NotQuickEntry,
	OnStartEnterTask,
	QuickRegistrationDisabled,
	RegistrationDisabledByMaximumJoinTime,
	Full,
	RaceFull,
}

public sealed record AutoGroupInstancePressEnterResult(
	int InstanceMaskId,
	int PlayerObjectId,
	AutoGroupInstancePressEnterStatus Status,
	int WorldId,
	int InstanceId,
	bool RemovedGroup,
	bool RemovedAlliance,
	AutoGroupInstanceRuntimeSnapshot? Snapshot,
	string JavaSource)
{
	public static AutoGroupInstancePressEnterResult Missing(int instanceMaskId, int playerObjectId)
	{
		return new AutoGroupInstancePressEnterResult(
			instanceMaskId,
			playerObjectId,
			AutoGroupInstancePressEnterStatus.NoAutoInstance,
			WorldId: 0,
			InstanceId: 0,
			RemovedGroup: false,
			RemovedAlliance: false,
			Snapshot: null,
			"AutoGroupService.pressEnter -> getAutoInstance returned null");
	}
}

public enum AutoGroupInstancePressEnterStatus
{
	NoAutoInstance,
	ReadyToEnter,
}

public sealed record AutoGroupInstanceCancelEnterResult(
	int InstanceMaskId,
	int PlayerObjectId,
	AutoGroupInstanceCancelEnterStatus Status,
	int WorldId,
	int InstanceId,
	int RegisteredPlayerCountAfterCancel,
	AutoGroupInstanceRuntimeSnapshot? Snapshot,
	string JavaSource)
{
	public static AutoGroupInstanceCancelEnterResult Missing(int instanceMaskId, int playerObjectId)
	{
		return new AutoGroupInstanceCancelEnterResult(
			instanceMaskId,
			playerObjectId,
			AutoGroupInstanceCancelEnterStatus.NoAutoInstance,
			WorldId: 0,
			InstanceId: 0,
			RegisteredPlayerCountAfterCancel: 0,
			Snapshot: null,
			"AutoGroupService.cancelEnter -> getAutoInstance returned null");
	}
}

public enum AutoGroupInstanceCancelEnterStatus
{
	NoAutoInstance,
	Unregistered,
}

public readonly record struct AutoGroupInstanceRuntimeKey(int WorldId, int InstanceId);

internal sealed class AutoGroupInstanceRuntimeState
{
	private readonly HashSet<int> _registeredPlayerObjectIds;
	private readonly Dictionary<int, string> _registeredPlayerRacesByObjectId;

	public AutoGroupInstanceRuntimeState(
		int worldId,
		int instanceId,
		int instanceMaskId,
		AutoGroupInstanceKind instanceKind,
		bool quickRegistrationAllowed,
		IEnumerable<int> registeredPlayerObjectIds,
		DateTimeOffset? readyEnterStartTime = null,
		DateTimeOffset? startInstanceTime = null,
		int maximumJoinTimeMilliseconds = 0,
		int maxPlayers = 0,
		IReadOnlyDictionary<int, string>? registeredPlayerRacesByObjectId = null)
	{
		WorldId = worldId;
		Key = new AutoGroupInstanceRuntimeKey(worldId, instanceId == 0 ? 1 : instanceId);
		InstanceMaskId = instanceMaskId;
		InstanceKind = instanceKind;
		QuickRegistrationAllowed = quickRegistrationAllowed;
		ReadyEnterStartTime = readyEnterStartTime;
		StartInstanceTime = startInstanceTime;
		MaximumJoinTimeMilliseconds = maximumJoinTimeMilliseconds;
		MaxPlayers = Math.Max(0, maxPlayers);
		_registeredPlayerObjectIds = registeredPlayerObjectIds.ToHashSet();
		_registeredPlayerRacesByObjectId = registeredPlayerRacesByObjectId == null
			? []
			: new Dictionary<int, string>(registeredPlayerRacesByObjectId);
	}

	public int WorldId { get; }

	public AutoGroupInstanceRuntimeKey Key { get; }

	public int InstanceMaskId { get; }

	public AutoGroupInstanceKind InstanceKind { get; }

	public bool QuickRegistrationAllowed { get; }

	public DateTimeOffset? ReadyEnterStartTime { get; }

	public DateTimeOffset? StartInstanceTime { get; }

	public int MaximumJoinTimeMilliseconds { get; }

	public int MaxPlayers { get; }

	public AutoGroupInstanceLeaveFacts CreateLeaveFacts(
		Player player,
		bool autoGroupEnabled,
		int onlinePlayersInsideAfterLeave)
	{
		return new AutoGroupInstanceLeaveFacts(
			player.ObjectId,
			autoGroupEnabled,
			HasAutoInstanceForCurrentMap: true,
			IsRegisteredAutoGroupPlayer: _registeredPlayerObjectIds.Contains(player.ObjectId),
			InstanceKind,
			RegisteredPlayerCountBeforeLeave: _registeredPlayerObjectIds.Count,
			OnlinePlayersInsideAfterLeave: Math.Max(0, onlinePlayersInsideAfterLeave),
			QuickRegistrationAllowed,
			PlayerIsInGroup: player.TeamMembership == PlayerTeamMembership.Group,
			PlayerIsInAlliance: player.TeamMembership == PlayerTeamMembership.Alliance);
	}

	public AutoGroupOpenQuickEntryResult TryAddOpenQuickEntry(
		AutoGroupOpenQuickEntryRequest request,
		DateTimeOffset now)
	{
		var snapshot = CreateSnapshot();
		if (!QuickRegistrationAllowed)
			return AutoGroupOpenQuickEntryResult.NotAdded(
				request,
				AutoGroupOpenQuickEntryStatus.QuickRegistrationDisabled,
				snapshot,
				"AutoPvpInstance.addLookingForParty -> AutoInstance.isRegistrationDisabled(lfp) or !template.hasRegisterQuick -> reject open quick-entry");

		if (IsRegistrationDisabledForQuickEntry(now))
			return AutoGroupOpenQuickEntryResult.NotAdded(
				request,
				AutoGroupOpenQuickEntryStatus.RegistrationDisabledByMaximumJoinTime,
				snapshot,
				"AutoInstance.isRegistrationDisabled(lfp) -> now - startInstanceTime > AutoGroupType.getMaximumJoinTime() for QUICK_GROUP_ENTRY");

		if (MaxPlayers > 0 && _registeredPlayerObjectIds.Count + request.MemberObjectIds.Count > MaxPlayers)
			return AutoGroupOpenQuickEntryResult.NotAdded(
				request,
				AutoGroupOpenQuickEntryStatus.Full,
				snapshot,
				"AutoPvpInstance.addLookingForParty -> getPlayersInside() >= getMaxPlayers() or party would exceed instance max players");

		var registeredRaceCount = _registeredPlayerRacesByObjectId.Values.Count(race =>
			string.Equals(race, request.Race, StringComparison.OrdinalIgnoreCase));
		if (request.MaxPlayersForRace > 0 && registeredRaceCount + request.MemberObjectIds.Count > request.MaxPlayersForRace)
			return AutoGroupOpenQuickEntryResult.NotAdded(
				request,
				AutoGroupOpenQuickEntryStatus.RaceFull,
				snapshot,
				"AutoPvpInstance.addLookingForParty -> lfp.size() + registered race players > getMaxPlayers(race) -> reject");

		foreach (var memberObjectId in request.MemberObjectIds)
		{
			_registeredPlayerObjectIds.Add(memberObjectId);
			_registeredPlayerRacesByObjectId[memberObjectId] = request.Race;
		}

		return AutoGroupOpenQuickEntryResult.Added(request, CreateSnapshot());
	}

	public bool Unregister(int playerObjectId)
	{
		var removed = _registeredPlayerObjectIds.Remove(playerObjectId);
		if (removed)
			_registeredPlayerRacesByObjectId.Remove(playerObjectId);
		return removed;
	}

	public bool IsRegistered(int playerObjectId) => _registeredPlayerObjectIds.Contains(playerObjectId);

	public int RegisteredPlayerCount => _registeredPlayerObjectIds.Count;

	public AutoGroupInstanceRuntimeSnapshot CreateSnapshot()
	{
		return new AutoGroupInstanceRuntimeSnapshot(
			Key.WorldId,
			Key.InstanceId,
			InstanceMaskId,
			InstanceKind,
			QuickRegistrationAllowed,
			_registeredPlayerObjectIds.ToHashSet(),
			new Dictionary<int, string>(_registeredPlayerRacesByObjectId),
			ReadyEnterStartTime,
			StartInstanceTime,
			MaximumJoinTimeMilliseconds,
			MaxPlayers);
	}

	private bool IsRegistrationDisabledForQuickEntry(DateTimeOffset now)
	{
		return StartInstanceTime.HasValue
			&& MaximumJoinTimeMilliseconds > 0
			&& now - StartInstanceTime.Value > TimeSpan.FromMilliseconds(MaximumJoinTimeMilliseconds);
	}
}
