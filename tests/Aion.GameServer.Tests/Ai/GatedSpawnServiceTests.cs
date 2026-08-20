using System;
using System.Linq;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World.Spawns;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The call site: conditional spawn groups going into the world at start.
/// </summary>
/// <remarks>
/// <see cref="GatedSpawnService"/> reads 14,292 placements across 91 maps and starts a controller for
/// every non-instance map that has any. Roughly 619 hold before a pattern writes anything, so that many
/// npcs appear at boot and the rest wait on a condition.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GatedSpawnServiceTests : IDisposable
{
	/// <summary>
	/// A map with 270 non-duplicate groups whose gates hold on an empty store.
	/// </summary>
	/// <remarks>
	/// Written first with <c>110010000</c>, which the file does carry rows for — but every one of them
	/// duplicates a static spawn this port already makes, so the loader filters them all and the map
	/// contributes nothing. "Has rows" and "has groups to place" are different questions.
	/// </remarks>
	private const int Map = 300090000;

	public void Dispose()
	{
		GatedSpawnService.Stop();
		SpawnVariableRegistry.Clear();
	}

	/// <summary><b>Starting places the groups whose gates already hold.</b></summary>
	[Fact]
	public void StartingPlacesTheGroupsThatHold()
	{
		using BossAiHarness harness = BossAiHarness.For(Map).WithWorldSize(4096).Build();

		int placed = GatedSpawnService.Start(BossAiHarness.RepoRoot());

		Assert.True(placed > 0, "no gated group was placed at all");
		Assert.Equal(placed, GatedSpawnService.Placed);
	}

	/// <summary>
	/// <b>And a pattern writing a variable afterwards still moves them.</b> The controllers stay
	/// subscribed; one that is collected would stop listening and the gate would silently stop working.
	/// </summary>
	[Fact]
	public void TheControllersKeepListeningAfterStart()
	{
		using BossAiHarness harness = BossAiHarness.For(Map).WithWorldSize(4096).Build();
		GatedSpawnService.Start(BossAiHarness.RepoRoot());
		int before = GatedSpawnService.Placed;

		// Most of this map's gates are `SpecialServer_Cond == 0`, so setting it to 1 closes them.
		SpawnVariableRegistry.For(Map, harness.InstanceId).Write("SpecialServer_Cond", 1, 0);

		Assert.NotEqual(before, GatedSpawnService.Placed);
	}

	/// <summary>
	/// <b>Stopping unsubscribes.</b> A restart must not leave the old controllers listening.
	/// </summary>
	/// <remarks>
	/// Asserted through the world rather than through <c>Placed</c>. <c>Placed</c> sums over the live
	/// controllers, so simply forgetting the list zeroes it whether or not anyone unsubscribed — a
	/// mutation that dropped the <c>Dispose</c> and kept the <c>Clear</c> passed happily until this
	/// looked at the npcs instead.
	/// </remarks>
	[Fact]
	public void StoppingUnsubscribes()
	{
		using BossAiHarness harness = BossAiHarness.For(Map).WithWorldSize(4096).Build();
		SpawnVariables store = SpawnVariableRegistry.For(Map, harness.InstanceId);
		store.Write("SpecialServer_Cond", 1, 0);
		GatedSpawnService.Start(BossAiHarness.RepoRoot());
		GatedSpawnService.Stop();
		int afterStop = harness.LiveNpcs().Count;

		// Opening the gates again must reach nobody.
		store.Write("SpecialServer_Cond", 0, 0);

		Assert.Equal(0, GatedSpawnService.Placed);
		Assert.Equal(afterStop, harness.LiveNpcs().Count);
	}

	/// <summary>An instance map with 764 non-duplicate gated groups, all of them skipped until now.</summary>
	private const int InstanceMap = 301370000;

	/// <summary>
	/// <b>An instance gets its own controller.</b> 63 instance maps carry gated groups and the service
	/// used to skip every one of them.
	/// </summary>
	[Fact]
	public void AnInstanceGetsItsOwnController()
	{
		using BossAiHarness harness = BossAiHarness.For(InstanceMap).WithWorldSize(4096).Build();
		GatedSpawnService.Start(BossAiHarness.RepoRoot());

		GatedSpawnService.AttachInstance(InstanceMap, harness.InstanceId);

		// This map's gates are its wave counters -- N_WAVE_01 through _04, 185 groups on the first
		// alone. Written first with SpecialServer_Cond, which this map's gates never read, so nothing
		// moved and the pin failed for the reason it was testing rather than the one it was written for.
		SpawnVariables store = SpawnVariableRegistry.For(InstanceMap, harness.InstanceId);
		int before = GatedSpawnService.Placed;

		store.Write("N_WAVE_01", 1, 0);

		Assert.True(GatedSpawnService.Placed > before,
			$"wave one opened no gate: {before} -> {GatedSpawnService.Placed}");
	}

	/// <summary>
	/// <b>Detaching drops the controller and the instance's counters.</b> Counters left behind would be
	/// inherited by the next instance to take the same id.
	/// </summary>
	[Fact]
	public void DetachingForgetsTheInstance()
	{
		using BossAiHarness harness = BossAiHarness.For(InstanceMap).WithWorldSize(4096).Build();
		GatedSpawnService.Start(BossAiHarness.RepoRoot());
		GatedSpawnService.AttachInstance(InstanceMap, harness.InstanceId);
		SpawnVariableRegistry.For(InstanceMap, harness.InstanceId).Write("WAVE_COUNT", 4, 0);

		GatedSpawnService.DetachInstance(InstanceMap, harness.InstanceId);

		Assert.Equal(0, SpawnVariableRegistry.For(InstanceMap, harness.InstanceId)["WAVE_COUNT"]);
	}

	/// <summary><b>Attaching twice does not double the groups.</b></summary>
	[Fact]
	public void AttachingTwiceIsNotTwoControllers()
	{
		using BossAiHarness harness = BossAiHarness.For(InstanceMap).WithWorldSize(4096).Build();
		GatedSpawnService.Start(BossAiHarness.RepoRoot());
		GatedSpawnService.AttachInstance(InstanceMap, harness.InstanceId);
		int once = GatedSpawnService.Placed;

		GatedSpawnService.AttachInstance(InstanceMap, harness.InstanceId);

		Assert.Equal(once, GatedSpawnService.Placed);
	}
}
