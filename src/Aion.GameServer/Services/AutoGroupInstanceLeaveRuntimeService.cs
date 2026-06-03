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
				registration.StartInstanceTime);
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
	DateTimeOffset? StartInstanceTime = null);

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
	DateTimeOffset? ReadyEnterStartTime,
	DateTimeOffset? StartInstanceTime);

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

	public AutoGroupInstanceRuntimeState(
		int worldId,
		int instanceId,
		int instanceMaskId,
		AutoGroupInstanceKind instanceKind,
		bool quickRegistrationAllowed,
		IEnumerable<int> registeredPlayerObjectIds,
		DateTimeOffset? readyEnterStartTime = null,
		DateTimeOffset? startInstanceTime = null)
	{
		WorldId = worldId;
		Key = new AutoGroupInstanceRuntimeKey(worldId, instanceId == 0 ? 1 : instanceId);
		InstanceMaskId = instanceMaskId;
		InstanceKind = instanceKind;
		QuickRegistrationAllowed = quickRegistrationAllowed;
		ReadyEnterStartTime = readyEnterStartTime;
		StartInstanceTime = startInstanceTime;
		_registeredPlayerObjectIds = registeredPlayerObjectIds.ToHashSet();
	}

	public int WorldId { get; }

	public AutoGroupInstanceRuntimeKey Key { get; }

	public int InstanceMaskId { get; }

	public AutoGroupInstanceKind InstanceKind { get; }

	public bool QuickRegistrationAllowed { get; }

	public DateTimeOffset? ReadyEnterStartTime { get; }

	public DateTimeOffset? StartInstanceTime { get; }

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
			ReadyEnterStartTime,
			StartInstanceTime);
	}
}
