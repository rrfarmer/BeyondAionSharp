using Aion.GameServer.Model.GameObjects;
using System.Threading;

namespace Aion.GameServer.Services;

public sealed class AutoGroupInstanceLeaveRuntimeService
{
	private readonly Lock _sync = new();
	private readonly Dictionary<AutoGroupInstanceRuntimeKey, AutoGroupInstanceRuntimeState> _instancesByKey = [];
	private readonly PlayerGroupRuntime _playerGroups;
	private readonly PlayerAllianceRuntime _playerAlliances;
	private readonly Func<int, int, InstanceDestroyWorkflowResult>? _destroyInstance;
	private readonly bool _autoGroupEnabled;

	public AutoGroupInstanceLeaveRuntimeService(
		PlayerGroupRuntime playerGroups,
		PlayerAllianceRuntime playerAlliances,
		Func<int, int, InstanceDestroyWorkflowResult>? destroyInstance = null,
		bool autoGroupEnabled = true)
	{
		_playerGroups = playerGroups;
		_playerAlliances = playerAlliances;
		_destroyInstance = destroyInstance;
		_autoGroupEnabled = autoGroupEnabled;
	}

	public AutoGroupInstanceRuntimeSnapshot RegisterInstance(AutoGroupInstanceRuntimeRegistration registration)
	{
		lock (_sync)
		{
			var state = new AutoGroupInstanceRuntimeState(
				registration.WorldId,
				registration.InstanceId,
				registration.InstanceKind,
				registration.QuickRegistrationAllowed,
				registration.RegisteredPlayerObjectIds);
			_instancesByKey[state.Key] = state;
			return state.CreateSnapshot();
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
				"AutoGroupService.onLeaveInstance live adapter slice -> planner, unregister, team cleanup, registry removal when destroyIfPossible is true");
		}

		if (destroyKey is { } keyToDestroy && _destroyInstance != null)
		{
			var destroyResult = _destroyInstance(keyToDestroy.WorldId, keyToDestroy.InstanceId);
			result = result with { DestroyWorkflowResult = destroyResult };
		}

		return result;
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
	IReadOnlyCollection<int> RegisteredPlayerObjectIds);

public sealed record AutoGroupInstanceRuntimeResult(
	AutoGroupInstanceLeavePlan Plan,
	AutoGroupInstanceRuntimeSnapshot? SnapshotAfterLeave,
	bool RemovedFromRegistry,
	InstanceDestroyWorkflowResult? DestroyWorkflowResult,
	string JavaSource);

public sealed record AutoGroupInstanceRuntimeSnapshot(
	int WorldId,
	int InstanceId,
	AutoGroupInstanceKind InstanceKind,
	bool QuickRegistrationAllowed,
	IReadOnlySet<int> RegisteredPlayerObjectIds);

public readonly record struct AutoGroupInstanceRuntimeKey(int WorldId, int InstanceId);

internal sealed class AutoGroupInstanceRuntimeState
{
	private readonly HashSet<int> _registeredPlayerObjectIds;

	public AutoGroupInstanceRuntimeState(
		int worldId,
		int instanceId,
		AutoGroupInstanceKind instanceKind,
		bool quickRegistrationAllowed,
		IEnumerable<int> registeredPlayerObjectIds)
	{
		Key = new AutoGroupInstanceRuntimeKey(worldId, instanceId == 0 ? 1 : instanceId);
		InstanceKind = instanceKind;
		QuickRegistrationAllowed = quickRegistrationAllowed;
		_registeredPlayerObjectIds = registeredPlayerObjectIds.ToHashSet();
	}

	public AutoGroupInstanceRuntimeKey Key { get; }

	public AutoGroupInstanceKind InstanceKind { get; }

	public bool QuickRegistrationAllowed { get; }

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

	public bool Unregister(int playerObjectId) => _registeredPlayerObjectIds.Remove(playerObjectId);

	public AutoGroupInstanceRuntimeSnapshot CreateSnapshot()
	{
		return new AutoGroupInstanceRuntimeSnapshot(
			Key.WorldId,
			Key.InstanceId,
			InstanceKind,
			QuickRegistrationAllowed,
			_registeredPlayerObjectIds.ToHashSet());
	}
}
