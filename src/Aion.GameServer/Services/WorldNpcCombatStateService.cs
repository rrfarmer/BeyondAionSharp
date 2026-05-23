using System.Collections.Concurrent;

namespace Aion.GameServer.Services;

public sealed class WorldNpcCombatStateService
{
	private readonly ConcurrentDictionary<int, MutableWorldNpcCombatState> _states = new();

	public bool TryGetState(int npcObjectId, out WorldNpcCombatRuntimeState? state)
	{
		if (!_states.TryGetValue(npcObjectId, out var mutable))
		{
			state = null;
			return false;
		}

		state = mutable.ToSnapshot();
		return true;
	}

	public WorldNpcCombatRuntimeState AddDamage(
		int npcObjectId,
		int attackerObjectId,
		int damage,
		bool notifyAttack,
		WorldNpcDamageHopType hopType)
	{
		// Java parity: controllers/CreatureController.onAttack -> AggroList.addDamage before HP reduction.
		var normalizedDamage = Math.Max(0, damage);
		var state = _states.GetOrAdd(npcObjectId, _ => new MutableWorldNpcCombatState(npcObjectId));
		lock (state)
		{
			var existing = state.HateByAttacker.GetValueOrDefault(attackerObjectId);
			state.HateByAttacker[attackerObjectId] = existing == null
				? new WorldNpcAggroEntry(attackerObjectId, normalizedDamage, normalizedDamage, notifyAttack, hopType)
				: existing with
				{
					Damage = existing.Damage + normalizedDamage,
					Hate = existing.Hate + normalizedDamage,
					NotifyAttack = notifyAttack,
					HopType = hopType,
				};
			return state.ToSnapshot();
		}
	}

	public WorldNpcCombatRuntimeState IncrementAttackedCount(int npcObjectId)
	{
		// Java parity: controllers/CreatureController.onAttack -> Creature.incrementAttackedCount after HP reduction.
		var state = _states.GetOrAdd(npcObjectId, _ => new MutableWorldNpcCombatState(npcObjectId));
		lock (state)
		{
			state.AttackedCount++;
			return state.ToSnapshot();
		}
	}

	public void Clear(int npcObjectId)
	{
		// Java parity: controllers/VisibleObjectController.delete/onDespawn drops runtime creature combat state.
		_states.TryRemove(npcObjectId, out _);
	}

	private sealed class MutableWorldNpcCombatState
	{
		public MutableWorldNpcCombatState(int npcObjectId)
		{
			NpcObjectId = npcObjectId;
		}

		public int NpcObjectId { get; }

		public int AttackedCount { get; set; }

		public Dictionary<int, WorldNpcAggroEntry> HateByAttacker { get; } = [];

		public WorldNpcCombatRuntimeState ToSnapshot()
		{
			return new WorldNpcCombatRuntimeState(
				NpcObjectId,
				AttackedCount,
				HateByAttacker.Values.OrderBy(entry => entry.AttackerObjectId).ToArray());
		}
	}
}

public sealed record WorldNpcCombatRuntimeState(
	int NpcObjectId,
	int AttackedCount,
	IReadOnlyList<WorldNpcAggroEntry> HateEntries);

public sealed record WorldNpcAggroEntry(
	int AttackerObjectId,
	int Damage,
	int Hate,
	bool NotifyAttack,
	WorldNpcDamageHopType HopType);

public enum WorldNpcDamageHopType
{
	Damage,
}
