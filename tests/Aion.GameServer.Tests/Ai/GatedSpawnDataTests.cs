using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aion.GameServer.World.Spawns;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The gated spawn data as the server will read it.
/// </summary>
/// <remarks>
/// 21,096 placements across 97 maps, from 78,865 that retail gates: the rest name npcs this port has no
/// template for, or sit in worlds <c>world_maps.xml</c> does not name.
/// </remarks>
public sealed class GatedSpawnDataTests
{
	private static string Path_ => Path.Combine(BossAiHarness.RepoRoot(), "game-server", "data",
		"static_data", GatedSpawnData.RelativePath.Replace('/', Path.DirectorySeparatorChar));

	/// <summary><b>Every row loads, and lands on a map.</b></summary>
	[Fact]
	public void TheWholeFileLoads()
	{
		IReadOnlyDictionary<int, IReadOnlyList<GatedSpawn>> byMap = GatedSpawnData.Load(Path_);

		// Fewer maps than the file names, because a map whose every placement duplicates an existing
		// static spawn contributes nothing once those are filtered.
		Assert.Equal(91, byMap.Count);

		// 21,096 rows, less 6,800 that duplicate a static spawn and four behind retail's broken gates.
		Assert.Equal(14292, byMap.Values.Sum(g => g.Count));
		Assert.All(byMap.Keys, mapId => Assert.True(mapId > 0));
	}

	/// <summary>
	/// <b>Most groups track their condition both ways.</b> 23,844 of retail's 25,012 carried
	/// <c>despawnAtOther</c> before the map join; a loader that dropped the flag would leave every one
	/// of them in the world for good.
	/// </summary>
	[Fact]
	public void MostGroupsCarryDespawnAtOther()
	{
		IReadOnlyDictionary<int, IReadOnlyList<GatedSpawn>> byMap = GatedSpawnData.Load(Path_);
		List<GatedSpawn> all = byMap.Values.SelectMany(g => g).ToList();

		Assert.True(all.Count(g => g.DespawnAtOther) > all.Count / 2);
		Assert.Contains(all, g => !g.DespawnAtOther);
	}

	/// <summary>
	/// <b>Only a fraction hold on an empty store.</b> 619 of them, so wiring the call site places about
	/// that many npcs that are absent today.
	/// <para>
	/// This number has moved twice and both moves were real: 1,525 measured on the pre-join file, 1,447
	/// after the map join dropped worlds <c>world_maps.xml</c> cannot name, and 693 once the placements
	/// that duplicate an existing static spawn were filtered out. Each figure was right for the file it
	/// was measured on, and 693 was a guess in between that the suite corrected to 619.
	/// </para>
	/// </summary>
	[Fact]
	public void OnlyAFractionHoldBeforeAnythingIsWritten()
	{
		IReadOnlyDictionary<int, IReadOnlyList<GatedSpawn>> byMap = GatedSpawnData.Load(Path_);
		var empty = new Dictionary<string, int>();

		int holds = byMap.Values.SelectMany(g => g).Count(g => g.Gate.Holds(empty));

		Assert.Equal(619, holds);
	}

	/// <summary>
	/// <b>The duplicates are there and are excluded by default.</b> 6,800 of the 21,096 are the same
	/// npc within five metres of a spawn this port already makes unconditionally, so loading both would
	/// put two of everything in those worlds.
	/// </summary>
	[Fact]
	public void TheDuplicatesAreExcludedUnlessAskedFor()
	{
		int filtered = GatedSpawnData.Load(Path_).Values.Sum(g => g.Count);
		int everything = GatedSpawnData.Load(Path_, includeOverlapping: true).Values.Sum(g => g.Count);

		Assert.Equal(21092, everything);
		Assert.Equal(6800, everything - filtered);
	}

	/// <summary><b>A missing file is empty, not a crash.</b></summary>
	[Fact]
	public void AMissingFileLoadsAsNothing()
	{
		Assert.Empty(GatedSpawnData.Load(Path.Combine(Path.GetTempPath(), "no_such_gated_spawns.tsv")));
	}
}
