using System;
using System.Linq;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.World.Spawns;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Npcs that exist in order to tell the world they exist.
/// </summary>
/// <remarks>
/// 209 retail patterns whose <c>on_wake_up</c> writes a spawn variable and does nothing else. They are
/// the largest untapped source of fuel for the conditional spawn engine: classifying the 1,201 gate
/// variables by who writes them puts <c>on_wake_up</c> at the top with 15,327 gated placements behind
/// it, and this unguarded subset alone reaches 11,121.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class WakeVariableAiTests
{
	private const int Map = 300520000;

	/// <summary><c>IDKamar_Invisible_Flag_1</c>: writes the variable 795 retail gates read.</summary>
	private const int Flag = 701918;

	/// <summary>Any npc with a template, to stand in for a gated group.</summary>
	private const int Gated = 283069;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Map).WithWorldSize(4096)
			.WithAi(typeof(WakeVariableAI), typeof(GeneralNpcAI), typeof(AggressiveNpcAI)).Build();

	/// <summary><b>Simply spawning it opens the gate.</b></summary>
	/// <remarks>
	/// No fight, no timer, nothing killed: the npc appears and a group that was gated shut appears with
	/// it. That is the whole mechanic, and it is why this was worth porting ahead of anything larger.
	/// </remarks>
	[Fact]
	public void SpawningTheFlagOpensTheGate()
	{
		using BossAiHarness harness = NewHarness();
		// The registry is process-wide and keyed on map plus instance, and instance ids repeat across
		// harnesses, so a variable another test wrote is still sitting there. `v01` in particular is
		// written by several of these npcs.
		SpawnVariableRegistry.Forget(Map, harness.InstanceId);
		SpawnVariables store = SpawnVariableRegistry.For(Map, harness.InstanceId);
		using var gated = new GatedSpawnController(Map, harness.InstanceId, store,
			[new GatedSpawn(Gated, 500f, 500f, 200f, 0, 0, true, SpawnCondition.Parse("v01 == 1"))]);
		gated.Refresh();
		Assert.Equal(0, gated.Placed);

		harness.Spawn(Flag, 300f, 300f, 200f);

		Assert.Equal(1, store["v01"]);
		Assert.Equal(1, gated.Placed);
	}

	/// <summary><b>It does not become aggressive by acquiring a job.</b></summary>
	/// <remarks>
	/// The reason this class extends <c>GeneralNpcAI</c> rather than <c>PatternAi</c>. Most of these
	/// npcs are passive <c>general</c> ones, and every other table here feeds a class that descends
	/// from <c>AggressiveNpcAI</c>; routing them through one would have made invisible flag markers
	/// attack players on sight, while the variable still got written and every other pin still passed.
	/// </remarks>
	[Fact]
	public void TheFlagStaysPassiveWithAPlayerNextToIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc flag = harness.Spawn(Flag, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(301f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(flag, player);

		harness.Clock.Advance(TimeSpan.FromSeconds(10));

		Assert.False(flag.GetAi().IsInState(Aion.GameServer.Ai.AIState.FIGHT),
			"a flag marker went hostile");
		Assert.Empty(flag.GetAggroList().Stream());
	}

	/// <summary><b>Every npc in the table is bound to the class that runs it.</b></summary>
	[Fact]
	public void EveryNpcInTheTableIsBound()
	{
		string templates = System.IO.File.ReadAllText(System.IO.Path.Combine(
			BossAiHarness.RepoRoot(), "game-server", "data", "static_data", "npcs",
			"npc_templates.xml"));

		foreach (int npc in WakeVariables.Npcs)
		{
			int at = templates.IndexOf($"npc_id=\"{npc}\"", StringComparison.Ordinal);
			Assert.True(at >= 0, $"npc {npc} has no template");
			Assert.Contains("ai=\"wake_variable\"", templates[at..templates.IndexOf('>', at)]);
		}
	}
}
