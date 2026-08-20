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

	/// <summary>One of the 168 writers that was already aggressive and must stay so.</summary>
	private const int AggressiveWriter = 216846;

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

		// The difference between the two base classes is what they do with a creature walking up to
		// them: AggressiveNpcAI handles the aggro event, GeneralNpcAI ignores it. Advancing the clock
		// proves nothing, because nothing raises the event on its own here -- a pin written that way
		// passed a mutation that swapped the base class outright.
		flag.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CreatureAggro, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.False(flag.GetAi().IsInState(Aion.GameServer.Ai.AIState.FIGHT),
			"a flag marker went hostile");
		Assert.Empty(flag.GetAggroList().Stream());

		// And structurally, because behaviour cannot see it for *this* npc: its tribe would not aggro
		// a player even if the class were the aggressive one, so a mutation swapping the base class
		// survives every observation above. The invariant being protected is the class, so the class
		// is what is asserted -- the 719 npcs on this table include ones whose tribe would.
		Assert.IsNotAssignableFrom<AggressiveNpcAI>(flag.GetAi());
	}

	/// <summary><b>Every npc in the table is bound to one of the two classes that run it.</b></summary>
	/// <remarks>
	/// Two, not one, and which one matters: <see cref="WakeVariableAI"/> is passive and
	/// <c>WakeVariableAggressiveAI</c> is not. Binding an npc to the wrong one writes the variable
	/// correctly and changes whether it attacks, which no gate pin would ever notice.
	/// </remarks>
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
			string element = templates[at..templates.IndexOf('>', at)];
			Assert.True(element.Contains("ai=\"wake_variable\"")
				|| element.Contains("ai=\"wake_variable_aggressive\""),
				$"npc {npc} is in the table and bound to neither wake class");
		}
	}

	/// <summary><b>An aggressive writer keeps its aggression.</b></summary>
	/// <remarks>
	/// The mirror of the passive pin above. 168 of these npcs were already <c>aggressive</c>, and the
	/// obvious shortcut -- one class for the whole table -- would have written every variable correctly
	/// while quietly making a third of them stop fighting.
	/// </remarks>
	[Fact]
	public void AnAggressiveWriterStillFights()
	{
		using BossAiHarness harness = NewHarness();
		SpawnVariableRegistry.Forget(Map, harness.InstanceId);
		Npc writer = harness.Spawn(AggressiveWriter, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(writer, player);

		// The same event the passive pin fires, and this one must act on it. Engage() would force the
		// state whatever the base class is, which is why it is not used here.
		writer.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CreatureAggro, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.NotEmpty(writer.GetAggroList().Stream());
		Assert.NotEmpty(SpawnVariableRegistry.For(Map, harness.InstanceId).Snapshot());
	}
}
