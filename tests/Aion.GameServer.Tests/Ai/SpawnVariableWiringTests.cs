using System;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World.Spawns;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The two halves of the conditional spawn engine, joined: a pattern writes, a gate reads.
/// </summary>
/// <remarks>
/// <see cref="SpawnCondition"/> parses the gates and <see cref="SpawnVariables"/> holds the counters;
/// this is the wire between them. A <c>PatternAi</c> writes into its own map's store, which is where
/// the measurement put it — generic names like <c>v01</c> are written by patterns in nine unrelated
/// maps, so a single store would have them overwriting each other.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SpawnVariableWiringTests : IDisposable
{
	private const int OneMap = 300520000;

	private const int AnotherMap = 400010000;

	/// <summary>Any npc on a pattern class; what it does otherwise does not matter here.</summary>
	private const int Beacon = 283156;

	/// <summary>Names used only by this class, so no other test can move them.</summary>
	private const string WaveOne = "WIRE_WAVE_ONE";

	private const string Counter = "WIRE_COUNTER";

	private const string Flag = "WIRE_SERVER_FLAG";

	/// <summary>This test needs no world, only two distinct store keys.</summary>
	private const int AnyInstance = 1;

	/// <remarks>
	/// The registry is process-wide, so every test here uses <b>its own variable name</b> rather than
	/// relying on this to have run. Clearing between tests is tidiness; the names are the isolation.
	/// One version of this class shared <c>SpecialServer_Cond</c> across four tests and failed once in
	/// a full run and never in a filtered one, which is the signature of exactly that coupling.
	/// </remarks>
	public void Dispose() => SpawnVariableRegistry.Clear();

	/// <summary><b>A pattern's write lands in its own map's store.</b></summary>
	[Fact]
	public void APatternWritesIntoItsOwnMap()
	{
		using BossAiHarness harness = BossAiHarness.For(OneMap).WithWorldSize(4096)
			.WithAi(typeof(TiamatBeaconAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc npc = harness.Spawn(Beacon, 300f, 300f, 200f);

		((PatternAi)npc.GetAi()).SetSpawnVariable(WaveOne, 1, 0);

		Assert.Equal(1, SpawnVariableRegistry.For(OneMap, harness.InstanceId)[WaveOne]);
		Assert.Equal(0, SpawnVariableRegistry.For(AnotherMap, harness.InstanceId)[WaveOne]);
	}

	/// <summary><b>And the gate for that map then holds.</b></summary>
	[Fact]
	public void AndTheGateForThatMapThenHolds()
	{
		using BossAiHarness harness = BossAiHarness.For(OneMap).WithWorldSize(4096)
			.WithAi(typeof(TiamatBeaconAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc npc = harness.Spawn(Beacon, 300f, 300f, 200f);

		// The dump's own shape: a wave counter against the server's siege flag.
		SpawnCondition gate = SpawnCondition.Parse("(WIRE_WAVE_ONE == 1) && (SpecialServer_Cond == 0)");
		Assert.False(gate.Holds(SpawnVariableRegistry.For(OneMap, harness.InstanceId).Snapshot()));

		((PatternAi)npc.GetAi()).SetSpawnVariable(WaveOne, 1, 0);

		Assert.True(gate.Holds(SpawnVariableRegistry.For(OneMap, harness.InstanceId).Snapshot()));
		Assert.False(gate.Holds(SpawnVariableRegistry.For(AnotherMap, harness.InstanceId).Snapshot()));
	}

	/// <summary><b>The counting form works through the same wire.</b></summary>
	[Fact]
	public void TheCountingFormWorksToo()
	{
		using BossAiHarness harness = BossAiHarness.For(OneMap).WithWorldSize(4096)
			.WithAi(typeof(TiamatBeaconAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		var ai = (PatternAi)harness.Spawn(Beacon, 300f, 300f, 200f).GetAi();

		ai.SetSpawnVariable(Counter, 0, 1);
		ai.SetSpawnVariable(Counter, 0, 1);
		ai.SetSpawnVariable(Counter, 0, 1);

		Assert.True(SpawnCondition.Parse($"{Counter} >= 3").Holds(SpawnVariableRegistry.For(OneMap, harness.InstanceId).Snapshot()));
	}

	/// <summary>
	/// <b>A server flag reaches every map and no map writes back into it.</b> 738 variables carrying
	/// 21,286 gate uses are supplied this way and never written by a pattern.
	/// </summary>
	[Fact]
	public void AServerFlagReachesEveryMap()
	{
		SpawnVariableRegistry.Supply(Flag, 1);

		Assert.Equal(1, SpawnVariableRegistry.For(OneMap, AnyInstance)[Flag]);
		Assert.Equal(1, SpawnVariableRegistry.For(AnotherMap, AnyInstance)[Flag]);

		SpawnVariableRegistry.For(OneMap, AnyInstance).Write(Flag, 7, 0);

		Assert.Equal(7, SpawnVariableRegistry.For(OneMap, AnyInstance)[Flag]);
		Assert.Equal(1, SpawnVariableRegistry.For(AnotherMap, AnyInstance)[Flag]);
	}

	/// <summary>
	/// <b>Two instances of one map do not share a counter.</b>
	/// </summary>
	/// <remarks>
	/// Measured rather than assumed: of the patterns that write a spawn variable, <b>234 have their
	/// npcs only on instance maps</b> against 231 only on world maps. Keyed on the map alone, two groups
	/// running the same instance would share one set of counters and one group's wave progress would
	/// open the other group's gates. A world map has a single instance, so nothing changes for one.
	/// </remarks>
	[Fact]
	public void TwoInstancesOfOneMapDoNotShareACounter()
	{
		SpawnVariables first = SpawnVariableRegistry.For(OneMap, 1);
		SpawnVariables second = SpawnVariableRegistry.For(OneMap, 2);

		first.Write("WIRE_INSTANCE_WAVE", 3, 0);

		Assert.Equal(3, first["WIRE_INSTANCE_WAVE"]);
		Assert.Equal(0, second["WIRE_INSTANCE_WAVE"]);
	}

	/// <summary><b>And an instance can be forgotten when it closes.</b></summary>
	[Fact]
	public void AnInstanceCanBeForgotten()
	{
		SpawnVariableRegistry.For(OneMap, 7).Write("WIRE_CLOSED", 1, 0);
		Assert.Equal(1, SpawnVariableRegistry.For(OneMap, 7)["WIRE_CLOSED"]);

		SpawnVariableRegistry.Forget(OneMap, 7);

		Assert.Equal(0, SpawnVariableRegistry.For(OneMap, 7)["WIRE_CLOSED"]);
	}
}
