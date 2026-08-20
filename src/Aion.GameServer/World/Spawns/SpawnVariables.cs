using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Aion.GameServer.World.Spawns;

/// <summary>
/// The named counters retail's spawn gates read, and the rule its patterns write them by.
/// </summary>
/// <remarks>
/// The writer half of the conditional spawn engine. <c>set_condition_spawn_variable</c> appears 12,446
/// times across the 5.8 patterns over 2,122 names, and every use carries three fields: a
/// <c>&lt;string&gt;</c> name, a <c>&lt;set&gt;</c> value and a <c>&lt;modify&gt;</c> value.
/// <para>
/// <b>The two value fields are strictly complementary</b>, which is what settles the rule without
/// guesswork: across all 12,446 uses there is <b>not one</b> where both are non-zero.
/// <c>modify</c> is zero in 10,895 of them and <c>set</c> then carries the value to assign; in the other
/// 1,551 <c>set</c> is zero and <c>modify</c> carries a delta — 1 in 1,190 of them, −1 in 174, and the
/// rest spread from −48 to 350. Twenty-three carry zero in both, which is an assignment of zero: a
/// deliberate reset, not a no-op.
/// </para>
/// <para>
/// <b>An unread variable is zero</b>, matching <see cref="SpawnCondition"/>, so a gate on a name nothing
/// has written yet behaves as retail's <c>== 0</c> gates expect.
/// </para>
/// <para>
/// <b>Scope, measured.</b> 2,272 variables are read by gates and <b>738 of them are never written by
/// any pattern</b> — 21,286 gate uses, 39% of the total: <c>GAb1_PvPStatus</c>,
/// <c>SpecialServer_Cond</c>, <c>InterServer_Cond</c>, the <c>DirectPortalDest_*</c> family. Those are
/// server state, not npc state, and a store carrying only what patterns write would leave every one of
/// those gates reading zero. <see cref="Supplied"/> is where they come from.
/// <para>
/// The rest belong to a map. Of the 2,122 names patterns do write, <b>345 have writers in exactly one
/// map and 88 in more than one</b> — and the multi-map ones are all generic (<c>v01</c>…<c>v09</c>)
/// spanning unrelated encounters, an instance and an abyss map among them. Those are the same name
/// reused, not shared state, so a single global store would have them corrupting each other. One store
/// per map, reading through to the server's own flags.
/// </para>
/// <para>
/// <b>Still not settled:</b> what <c>[SAVE]</c> persistence attaches to, and whether the 1,689 names
/// whose writers this port cannot place on a map behave differently.
/// </para>
/// </remarks>
public sealed class SpawnVariables
{
	private readonly ConcurrentDictionary<string, int> values = new(StringComparer.Ordinal);

	private readonly IReadOnlyDictionary<string, int>? supplied;

	/// <summary>A store for one map, optionally reading through to the server's own flags.</summary>
	/// <param name="serverFlags">
	/// The variables no pattern writes — siege and PvP status, portal wiring — which the engine supplies
	/// rather than the AI. Names here are read-only: a write always lands in this map's own store.
	/// </param>
	public SpawnVariables(IReadOnlyDictionary<string, int>? serverFlags = null)
	{
		supplied = serverFlags;
	}

	/// <summary>The server flags this store reads through to, or empty.</summary>
	public IReadOnlyDictionary<string, int> Supplied
		=> supplied ?? new Dictionary<string, int>(StringComparer.Ordinal);

	/// <summary>Raised after any write that changed a value, with the name that changed.</summary>
	/// <remarks>
	/// A gate has to be re-checked when something it reads moves —
	/// <see cref="SpawnCondition.Variables"/> is what a listener matches against. Not raised when a
	/// write leaves the value where it was, so a heartbeat that keeps assigning the same number does not
	/// churn every gate in the world.
	/// </remarks>
	public event Action<string, int>? Changed;

	/// <summary>
	/// The value: this map's own first, then the server's, then zero.
	/// </summary>
	public int this[string name]
	{
		get
		{
			if (values.TryGetValue(name, out int found))
				return found;

			return supplied is not null && supplied.TryGetValue(name, out int given) ? given : 0;
		}
	}

	/// <summary>A snapshot for <see cref="SpawnCondition.Holds"/>, server flags included.</summary>
	public IReadOnlyDictionary<string, int> Snapshot()
	{
		var all = new Dictionary<string, int>(StringComparer.Ordinal);
		if (supplied is not null)
		{
			foreach ((string name, int value) in supplied)
				all[name] = value;
		}

		foreach ((string name, int value) in values)
			all[name] = value;

		return all;
	}

	/// <summary>
	/// Retail's <c>set_condition_spawn_variable</c>, by its own rule: <paramref name="modify"/> of zero
	/// assigns <paramref name="set"/>, and anything else adds <paramref name="modify"/>.
	/// </summary>
	public void Write(string name, int set, int modify)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		if (modify == 0)
			Assign(name, set);
		else
			Add(name, modify);
	}

	/// <summary>Sets a value outright.</summary>
	public void Assign(string name, int value)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		int before = this[name];
		values[name] = value;
		if (before != value)
			Changed?.Invoke(name, value);
	}

	/// <summary>Moves a value by a delta, treating an unwritten name as zero.</summary>
	public int Add(string name, int delta)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		int after = values.AddOrUpdate(name, delta, (_, current) => current + delta);
		if (delta != 0)
			Changed?.Invoke(name, after);

		return after;
	}

	/// <summary>Forgets everything, for a world or instance being reused.</summary>
	public void Clear() => values.Clear();
}
