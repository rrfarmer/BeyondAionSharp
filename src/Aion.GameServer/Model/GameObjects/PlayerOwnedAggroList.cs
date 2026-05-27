using Aion.GameServer.Services;

namespace Aion.GameServer.Model.GameObjects;

public sealed class PlayerOwnedAggroList
{
	private readonly Dictionary<int, PlayerAggroEntrySnapshot> _entries = [];

	// Java parity: controllers/attack/AggroList.clear cancels the hate reduction task before clearing entries.
	public bool HasHateReductionTask { get; private set; }

	public IReadOnlyList<PlayerAggroEntrySnapshot> Entries =>
		_entries.Values
			.OrderBy(entry => entry.AttackerObjectId)
			.ToArray();

	public bool TryAddKnownAttacker(
		int attackerObjectId,
		int damage,
		int hate,
		bool ownerKnownListKnowsAttacker,
		bool startsHateReductionTask = false)
	{
		// Java parity: PlayerAggroList.isAware only checks owner.getKnownList().knows(creature).
		if (!ownerKnownListKnowsAttacker)
			return false;

		var normalizedDamage = Math.Max(0, damage);
		var normalizedHate = Math.Max(1, hate);
		if (_entries.TryGetValue(attackerObjectId, out var existing))
		{
			normalizedDamage += existing.Damage;
			normalizedHate += existing.Hate;
		}

		_entries[attackerObjectId] = new PlayerAggroEntrySnapshot(
			attackerObjectId,
			normalizedDamage,
			normalizedHate);
		HasHateReductionTask |= startsHateReductionTask;
		return true;
	}

	public void MarkHateReductionTaskActiveForParity()
	{
		HasHateReductionTask = true;
	}

	public IReadOnlyList<PlayerAggroEntrySnapshot> Clear()
	{
		var clearedEntries = Entries;
		HasHateReductionTask = false;
		_entries.Clear();
		return clearedEntries;
	}
}
