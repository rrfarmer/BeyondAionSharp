using System;
using System.Linq;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World.Spawns;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The conditional spawn engine, end to end: a counter moves and a spawn group appears.
/// </summary>
/// <remarks>
/// Retail hides 78,865 npc placements behind gates, 25,012 of which this port has templates for and
/// never placed. This is the piece that places them.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GatedSpawnControllerTests
{
	private const int Map = 300520000;

	/// <summary>Any npc with a template; what it does is irrelevant to the gate.</summary>
	private const int Guard = 283069;

	private static GatedSpawn Group(string gate, bool despawnAtOther = true, int npcId = Guard) =>
		new GatedSpawn(npcId, 300f, 300f, 200f, 0, 0, despawnAtOther, SpawnCondition.Parse(gate));

	/// <summary><b>A group whose gate does not hold is not placed.</b></summary>
	[Fact]
	public void AClosedGatePlacesNothing()
	{
		using BossAiHarness harness = BossAiHarness.For(Map).WithWorldSize(4096).Build();
		var store = new SpawnVariables();
		using var controller = new GatedSpawnController(Map, harness.InstanceId, store,
			[Group("GATE_A == 1")]);

		controller.Refresh();

		Assert.Equal(0, controller.Placed);
		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == Guard);
	}

	/// <summary><b>One that holds is placed.</b></summary>
	[Fact]
	public void AnOpenGatePlacesTheGroup()
	{
		using BossAiHarness harness = BossAiHarness.For(Map).WithWorldSize(4096).Build();
		var store = new SpawnVariables();
		store.Write("GATE_B", 1, 0);
		using var controller = new GatedSpawnController(Map, harness.InstanceId, store,
			[Group("GATE_B == 1")]);

		controller.Refresh();

		Assert.Equal(1, controller.Placed);
		Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == Guard));
	}

	/// <summary>
	/// <b>A write opens the gate on its own</b>, without anyone asking for a refresh — which is the
	/// whole point: a pattern moves a counter and the world changes.
	/// </summary>
	[Fact]
	public void AWriteOpensTheGateByItself()
	{
		using BossAiHarness harness = BossAiHarness.For(Map).WithWorldSize(4096).Build();
		var store = new SpawnVariables();
		using var controller = new GatedSpawnController(Map, harness.InstanceId, store,
			[Group("GATE_C == 1")]);
		controller.Refresh();
		Assert.Equal(0, controller.Placed);

		store.Write("GATE_C", 1, 0);

		Assert.Equal(1, controller.Placed);
	}

	/// <summary><b>And closing it again takes the group away, when retail says so.</b></summary>
	[Fact]
	public void ClosingTheGateRemovesTheGroup()
	{
		using BossAiHarness harness = BossAiHarness.For(Map).WithWorldSize(4096).Build();
		var store = new SpawnVariables();
		store.Write("GATE_D", 1, 0);
		using var controller = new GatedSpawnController(Map, harness.InstanceId, store,
			[Group("GATE_D == 1")]);
		controller.Refresh();
		Assert.Equal(1, controller.Placed);

		store.Write("GATE_D", 0, 0);

		Assert.Equal(0, controller.Placed);
		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == Guard);
	}

	/// <summary>
	/// <b>Without <c>despawnAtOther</c> the group stays.</b> Roughly a third of retail's gated groups
	/// are placed once their condition is met and never removed; taking them away would be a mechanic
	/// retail does not have.
	/// </summary>
	[Fact]
	public void WithoutTheFlagTheGroupStays()
	{
		using BossAiHarness harness = BossAiHarness.For(Map).WithWorldSize(4096).Build();
		var store = new SpawnVariables();
		store.Write("GATE_E", 1, 0);
		using var controller = new GatedSpawnController(Map, harness.InstanceId, store,
			[Group("GATE_E == 1", despawnAtOther: false)]);
		controller.Refresh();
		Assert.Equal(1, controller.Placed);

		store.Write("GATE_E", 0, 0);

		Assert.Equal(1, controller.Placed);
	}

	/// <summary>
	/// <b>A write only re-checks the gates that read it.</b> A fortress counter ticking must not walk
	/// every gate in the world.
	/// </summary>
	[Fact]
	public void AWriteOnlyTouchesTheGatesThatReadIt()
	{
		using BossAiHarness harness = BossAiHarness.For(Map).WithWorldSize(4096).Build();
		var store = new SpawnVariables();
		using var controller = new GatedSpawnController(Map, harness.InstanceId, store,
			[Group("GATE_F == 1"), Group("GATE_G == 1")]);
		controller.Refresh();

		long afterRefresh = controller.Evaluations;
		Assert.Equal(2, afterRefresh);

		store.Write("GATE_F", 1, 0);

		// Exactly one more gate looked at: the one that mentions GATE_F.
		Assert.Equal(afterRefresh + 1, controller.Evaluations);
		Assert.Equal(1, controller.Placed);
	}

	/// <summary><b>A real gate from the dump works the same way.</b></summary>
	[Fact]
	public void ARealGateFromTheDumpWorks()
	{
		using BossAiHarness harness = BossAiHarness.For(Map).WithWorldSize(4096).Build();
		var store = new SpawnVariables();
		using var controller = new GatedSpawnController(Map, harness.InstanceId, store,
			[Group("(N_WAVE_01 == 1) && (SpecialServer_Cond == 0)")]);
		controller.Refresh();
		Assert.Equal(0, controller.Placed);

		// SpecialServer_Cond is unset, so it reads zero and that half already holds.
		store.Write("N_WAVE_01", 1, 0);

		Assert.Equal(1, controller.Placed);
	}
}
