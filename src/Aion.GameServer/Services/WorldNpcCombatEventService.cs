using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcCombatEventService
{
	private readonly ConcurrentDictionary<int, MutableWorldNpcCombatEventState> _states = new();
	private long _sequence;

	public bool TryGetState(int npcObjectId, out WorldNpcCombatEventRuntimeState? state)
	{
		if (!_states.TryGetValue(npcObjectId, out var mutable))
		{
			state = null;
			return false;
		}

		state = mutable.ToSnapshot();
		return true;
	}

	public WorldNpcAttackedObserverNotification NotifyAttackedObservers(
		int npcObjectId,
		int attackerObjectId,
		int skillId)
	{
		// Java parity: controllers/CreatureController.onAttack -> ObserveController.notifyAttackedObservers before aggro and HP reduction.
		var notification = new WorldNpcAttackedObserverNotification(
			npcObjectId,
			attackerObjectId,
			skillId,
			Interlocked.Increment(ref _sequence));
		var state = _states.GetOrAdd(npcObjectId, _ => new MutableWorldNpcCombatEventState(npcObjectId));
		lock (state)
		{
			state.AttackedObserverNotifications.Add(notification);
			return notification;
		}
	}

	public IReadOnlyList<WorldNpcSupportAiRequest> NotifyNearbySupportAi(
		IWorldNpcObject attackedNpc,
		IEnumerable<IWorldNpcObject> knownNpcs)
	{
		// Java parity: controllers/CreatureController.onAttack -> KnownList.forEachNpc(... CREATURE_NEEDS_SUPPORT) before HP reduction.
		var requests = knownNpcs
			.Where(npc => npc.ObjectId != attackedNpc.ObjectId)
			.Where(npc => IsWithinDefaultVisibility(npc.Position, attackedNpc.Position))
			.GroupBy(npc => npc.ObjectId)
			.Select(group => group.First())
			.OrderBy(npc => npc.ObjectId)
			.Select(npc => new WorldNpcSupportAiRequest(
				npc.ObjectId,
				attackedNpc.ObjectId,
				WorldNpcAiEventType.CreatureNeedsSupport,
				Interlocked.Increment(ref _sequence)))
			.ToArray();
		if (requests.Length == 0)
			return requests;

		var state = _states.GetOrAdd(attackedNpc.ObjectId, _ => new MutableWorldNpcCombatEventState(attackedNpc.ObjectId));
		lock (state)
		{
			state.SupportAiRequests.AddRange(requests);
			return requests;
		}
	}

	public void Clear(int objectId)
	{
		// Java parity: controllers/VisibleObjectController.delete/onDespawn clears creature-local observers and runtime AI event surface state.
		_states.TryRemove(objectId, out _);
		foreach (var state in _states.Values)
		{
			lock (state)
			{
				state.AttackedObserverNotifications.RemoveAll(notification => notification.AttackerObjectId == objectId);
				state.SupportAiRequests.RemoveAll(request =>
					request.AttackedNpcObjectId == objectId || request.SupportNpcObjectId == objectId);
			}
		}
	}

	private static bool IsWithinDefaultVisibility(WorldPosition sourcePosition, WorldPosition targetPosition)
	{
		if (sourcePosition.WorldId != targetPosition.WorldId || sourcePosition.InstanceId != targetPosition.InstanceId)
			return false;

		var deltaX = sourcePosition.X - targetPosition.X;
		var deltaY = sourcePosition.Y - targetPosition.Y;
		var deltaZ = sourcePosition.Z - targetPosition.Z;
		return deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ <= WorldVisibility.DefaultVisibleDistance * WorldVisibility.DefaultVisibleDistance;
	}

	private sealed class MutableWorldNpcCombatEventState
	{
		public MutableWorldNpcCombatEventState(int npcObjectId)
		{
			NpcObjectId = npcObjectId;
		}

		public int NpcObjectId { get; }

		public List<WorldNpcAttackedObserverNotification> AttackedObserverNotifications { get; } = [];

		public List<WorldNpcSupportAiRequest> SupportAiRequests { get; } = [];

		public WorldNpcCombatEventRuntimeState ToSnapshot()
		{
			return new WorldNpcCombatEventRuntimeState(
				NpcObjectId,
				AttackedObserverNotifications.ToArray(),
				SupportAiRequests.ToArray());
		}
	}
}

public sealed record WorldNpcCombatEventRuntimeState(
	int NpcObjectId,
	IReadOnlyList<WorldNpcAttackedObserverNotification> AttackedObserverNotifications,
	IReadOnlyList<WorldNpcSupportAiRequest> SupportAiRequests);

public sealed record WorldNpcAttackedObserverNotification(
	int NpcObjectId,
	int AttackerObjectId,
	int SkillId,
	long Sequence);

public sealed record WorldNpcSupportAiRequest(
	int SupportNpcObjectId,
	int AttackedNpcObjectId,
	WorldNpcAiEventType EventType,
	long Sequence);

public enum WorldNpcAiEventType
{
	CreatureNeedsSupport,
}
