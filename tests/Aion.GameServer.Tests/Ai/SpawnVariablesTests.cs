using System.Collections.Generic;
using Aion.GameServer.World.Spawns;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The writer half of the conditional spawn engine: <c>set_condition_spawn_variable</c>'s own rule.
/// </summary>
/// <remarks>
/// 12,446 uses across the 5.8 patterns, and the rule is settled by the data rather than inferred:
/// <b>not one use carries a non-zero <c>set</c> and a non-zero <c>modify</c> together</b>. So
/// <c>modify == 0</c> means assign, and anything else means add.
/// </remarks>
public sealed class SpawnVariablesTests
{
	/// <summary><b>A modify of zero assigns.</b> 10,895 of the 12,446 uses are this.</summary>
	[Fact]
	public void AModifyOfZeroAssigns()
	{
		var store = new SpawnVariables();
		store.Write("boss", 5, 0);
		Assert.Equal(5, store["boss"]);

		store.Write("boss", 2, 0);
		Assert.Equal(2, store["boss"]);
	}

	/// <summary><b>Anything else adds.</b> 1,190 uses add one; 174 subtract one.</summary>
	[Fact]
	public void AnyOtherModifyAdds()
	{
		var store = new SpawnVariables();
		store.Write("wave", 0, 1);
		store.Write("wave", 0, 1);
		Assert.Equal(2, store["wave"]);

		store.Write("wave", 0, -1);
		Assert.Equal(1, store["wave"]);
	}

	/// <summary>
	/// <b>Zero in both fields is an assignment of zero.</b> Twenty-three uses are written that way, and
	/// reading them as "no change" would leave a counter retail resets running on.
	/// </summary>
	[Fact]
	public void ZeroInBothFieldsResets()
	{
		var store = new SpawnVariables();
		store.Add("count", 7);

		store.Write("count", 0, 0);

		Assert.Equal(0, store["count"]);
	}

	/// <summary><b>An unwritten name reads as zero</b>, matching the gate side.</summary>
	[Fact]
	public void AnUnwrittenNameIsZero()
	{
		var store = new SpawnVariables();

		Assert.Equal(0, store["never_written"]);
		Assert.True(SpawnCondition.Parse("never_written == 0").Holds(store.Snapshot()));
	}

	/// <summary><b>A write that changes nothing raises nothing</b>, so a heartbeat does not churn gates.</summary>
	[Fact]
	public void AWriteThatChangesNothingIsQuiet()
	{
		var store = new SpawnVariables();
		List<string> heard = new List<string>();
		store.Changed += (name, _) => heard.Add(name);

		store.Write("v", 1, 0);
		store.Write("v", 1, 0);
		store.Write("v", 1, 0);

		Assert.Equal(new[] { "v" }, heard);
	}

	/// <summary>
	/// <b>A gate can read a variable no pattern ever writes.</b> 738 of the 2,272 do — 39% of all gate
	/// uses — and they are server state: PvP status, siege conditions, portal wiring.
	/// </summary>
	[Fact]
	public void ServerFlagsAreReadableAndNotWritable()
	{
		var flags = new Dictionary<string, int> { ["GAb1_PvPStatus"] = 2 };
		var store = new SpawnVariables(flags);

		Assert.Equal(2, store["GAb1_PvPStatus"]);
		Assert.True(SpawnCondition.Parse("GAb1_PvPStatus == 2").Holds(store.Snapshot()));

		// A map's own write shadows the flag for that map, and does not reach back into the server's.
		store.Write("GAb1_PvPStatus", 5, 0);
		Assert.Equal(5, store["GAb1_PvPStatus"]);
		Assert.Equal(2, flags["GAb1_PvPStatus"]);
	}

	/// <summary>
	/// <b>Two maps do not share a name.</b> <c>v01</c> is written by patterns in nine unrelated maps,
	/// so one store for the server would have an instance and an abyss map overwriting each other.
	/// </summary>
	[Fact]
	public void TwoMapsDoNotShareAName()
	{
		var flags = new Dictionary<string, int>();
		var one = new SpawnVariables(flags);
		var other = new SpawnVariables(flags);

		one.Write("v01", 3, 0);

		Assert.Equal(3, one["v01"]);
		Assert.Equal(0, other["v01"]);
	}

	/// <summary><b>And the two halves meet</b>: a written variable satisfies the gate that reads it.</summary>
	[Fact]
	public void TheWriterFeedsTheGate()
	{
		var store = new SpawnVariables();
		SpawnCondition gate = SpawnCondition.Parse("(N_WAVE_01 == 1) && (SpecialServer_Cond == 0)");

		Assert.False(gate.Holds(store.Snapshot()));

		store.Write("N_WAVE_01", 1, 0);

		Assert.True(gate.Holds(store.Snapshot()));
	}
}
