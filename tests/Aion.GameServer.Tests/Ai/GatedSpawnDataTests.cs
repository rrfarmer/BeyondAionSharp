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

		Assert.Equal(97, byMap.Count);

		// Nine gates in the dump are retail's own broken ones; four placements sit behind two of them.
		Assert.Equal(21092, byMap.Values.Sum(g => g.Count));
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
	/// <b>Only a fraction hold on an empty store.</b> 1,447 of them, so a fresh world places about that
	/// many and leaves the rest waiting on a condition.
	/// <para>
	/// Measured at 1,525 before the map join, on the 25,012 placements that included worlds
	/// <c>world_maps.xml</c> cannot name. The loadable set is smaller and so is its opening count.
	/// </para>
	/// </summary>
	[Fact]
	public void OnlyAFractionHoldBeforeAnythingIsWritten()
	{
		IReadOnlyDictionary<int, IReadOnlyList<GatedSpawn>> byMap = GatedSpawnData.Load(Path_);
		var empty = new Dictionary<string, int>();

		int holds = byMap.Values.SelectMany(g => g).Count(g => g.Gate.Holds(empty));

		Assert.Equal(1447, holds);
	}

	/// <summary><b>A missing file is empty, not a crash.</b></summary>
	[Fact]
	public void AMissingFileLoadsAsNothing()
	{
		Assert.Empty(GatedSpawnData.Load(Path.Combine(Path.GetTempPath(), "no_such_gated_spawns.tsv")));
	}
}
