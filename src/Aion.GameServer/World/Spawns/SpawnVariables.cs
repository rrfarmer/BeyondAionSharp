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
/// <b>Scope is not settled and this class does not assume one.</b> It is a plain store the owner keeps;
/// whether retail's variables live per world, per instance, per faction or globally is the open question
/// recorded in docs/retail-ai-fidelity.md, and <c>[SAVE]</c> persistence is a further one.
/// </para>
/// </remarks>
public sealed class SpawnVariables
{
	private readonly ConcurrentDictionary<string, int> values = new(StringComparer.Ordinal);

	/// <summary>Raised after any write that changed a value, with the name that changed.</summary>
	/// <remarks>
	/// A gate has to be re-checked when something it reads moves —
	/// <see cref="SpawnCondition.Variables"/> is what a listener matches against. Not raised when a
	/// write leaves the value where it was, so a heartbeat that keeps assigning the same number does not
	/// churn every gate in the world.
	/// </remarks>
	public event Action<string, int>? Changed;

	/// <summary>The value, or zero for a name nothing has written.</summary>
	public int this[string name] => values.TryGetValue(name, out int found) ? found : 0;

	/// <summary>A snapshot for <see cref="SpawnCondition.Holds"/>.</summary>
	public IReadOnlyDictionary<string, int> Snapshot() => new Dictionary<string, int>(values, StringComparer.Ordinal);

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
