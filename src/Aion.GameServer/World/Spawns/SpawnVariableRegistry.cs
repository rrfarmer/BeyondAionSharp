using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Aion.GameServer.World.Spawns;

/// <summary>
/// One <see cref="SpawnVariables"/> per map, plus the server flags every map reads through to.
/// </summary>
/// <remarks>
/// Scope was settled by measurement rather than choice — see <see cref="SpawnVariables"/> for the
/// numbers. In short: <b>738 of the 2,272 variables gates read are never written by any pattern</b> and
/// are the engine's own state, while the names patterns do write belong to a map, with generic ones
/// like <c>v01</c> reused across nine unrelated encounters.
/// <para>
/// <b>Keyed on map id, not on instance id.</b> That is the honest limit of what was measured: the
/// evidence places writers in maps, and says nothing about whether two simultaneous instances of the
/// same map should share their counters. For a world map they are the same thing; for an instance they
/// are not, and this is the first place to look when an instanced encounter behaves as though another
/// group had already done it.
/// </para>
/// </remarks>
public static class SpawnVariableRegistry
{
	private static readonly ConcurrentDictionary<(int Map, int Instance), SpawnVariables> ByInstance = new();

	private static readonly ConcurrentDictionary<string, int> ServerFlags = new(StringComparer.Ordinal);

	/// <summary>The store for one running instance of one map, created on first use.</summary>
	/// <remarks>
	/// <b>Keyed on the instance, not just the map, and that was measured rather than assumed.</b> Of the
	/// patterns that write a spawn variable, <b>234 have their npcs only on instance maps</b> against 231
	/// only on world maps. Keyed on the map alone, two groups running the same instance would share one
	/// set of counters — one group's wave progress would open the other group's gates.
	/// <para>
	/// A world map has a single instance, so nothing changes for one.
	/// </para>
	/// </remarks>
	public static SpawnVariables For(int mapId, int instanceId)
		=> ByInstance.GetOrAdd((mapId, instanceId), static _ => new SpawnVariables(ServerFlags));

	/// <summary>Forgets one instance's counters, for an instance being destroyed or reused.</summary>
	public static void Forget(int mapId, int instanceId) => ByInstance.TryRemove((mapId, instanceId), out _);

	/// <summary>
	/// Sets one of the variables no pattern writes — siege and PvP status, portal wiring — which every
	/// map reads through to.
	/// </summary>
	/// <remarks>
	/// The list of names that belong here is <c>tools/client-extract/out/spawn_inputs.tsv</c>: 738
	/// variables carrying 21,286 gate uses. Nothing supplies them yet, which is why those gates all
	/// read zero today.
	/// </remarks>
	public static void Supply(string name, int value)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		ServerFlags[name] = value;
	}

	/// <summary>Every server flag currently set.</summary>
	public static IReadOnlyDictionary<string, int> Supplied
		=> new Dictionary<string, int>(ServerFlags, StringComparer.Ordinal);

	/// <summary>Forgets every map's counters and the server flags, for a test or a restart.</summary>
	public static void Clear()
	{
		ByInstance.Clear();
		ServerFlags.Clear();
	}
}
