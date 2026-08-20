using System.IO;
using System;
using System.Linq;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.World.Spawns;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// What retail npcs leave behind when they die, for the encounters no rotation table could reach.
/// </summary>
/// <remarks>
/// 109 retail patterns across 265 npcs. <see cref="BattleCycles"/> reads <c>on_die</c> as well, but it
/// is keyed on a battle-timer chain and 179 of the encounters still missing an add have no rotation for
/// it to hang off. These are those encounters.
/// <para>
/// The distinction that matters most here is retail's own: <c>on_die</c> fires however the npc died and
/// <c>on_killed_by_user</c> only when a player did it. Getting that backwards is invisible in a spot
/// check -- the add still appears -- and wrong every time something else lands the blow.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DeathSpawnTableTests
{
	private const int AnyMap = 300520000;

	/// <summary><c>LF2A_TBox</c>: leaves something only when a <i>player</i> kills it.</summary>
	private const int PlayerKillOnly = 213905;

	private const int PlayerKillLeaves = 280722;

	/// <summary><c>IDSlk_KK</c>: leaves something however it died, and the something stays.</summary>
	/// <remarks>
	/// This used to be <c>IDDF3_NamedNWi</c>, whose add is <c>IDDF3_BroadNPC_System</c> -- a relay that
	/// appears, shouts to fifty metres and removes itself. Once the passive pattern table started
	/// running that npc's own pattern the add stopped lingering, and the pin counting it failed. It was
	/// only ever countable because nothing ran the pattern; the fix is a different encounter, not a
	/// looser assertion.
	/// </remarks>
	private const int AnyDeath = 215079;

	private const int AnyDeathLeaves = 281197;

	/// <summary><c>BGuard_Chief_Gab1_L</c>: leaves something only when an <i>npc</i> kills it.</summary>
	private const int NpcKilled = 277400;

	private const int NpcKilledLeaves = 295092;

	/// <summary>An npc of a tribe the guard's own tribe data marks hostile.</summary>
	private const int HostileToTheGuard = 277234;

	/// <summary>Tiamat, whose death writes the variable 70 retail placements are gated on.</summary>
	private const int Tiamat = 856029;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AnyMap).WithWorldSize(4096)
			// PassivePatternAI is registered because some of the adds these npcs leave are themselves
			// driven by a pattern now; without it the spawn silently produces nothing.
			.WithAi(typeof(DeathSpawnAI), typeof(PassivePatternAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI)).Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(npc => npc.GetNpcId() == npcId);

	/// <summary><b>A player kill leaves what retail leaves.</b></summary>
	[Fact]
	public void APlayerKillLeavesTheAdd()
	{
		using BossAiHarness harness = NewHarness();
		Npc box = harness.Spawn(PlayerKillOnly, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(box, player);
		// The guard reads the aggro list for player damage, so the player has to have actually hit it.
		// The death is raised as the event rather than through Kill, which tears the aggro list down
		// before the handler runs and leaves no record of who did it.
		harness.Engage(box, player);
		BossAiHarness.Wound(box, player);
		box.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(1, Count(harness, PlayerKillLeaves));
	}

	/// <summary><b>And a death with no player behind it leaves nothing.</b></summary>
	/// <remarks>
	/// The half that is easy to miss. Retail hangs this on <c>on_killed_by_user</c>, so an npc killed
	/// by anything else -- another npc, a hazard, expiry -- leaves nothing at all. A table that
	/// flattened the two handlers together would pass the pin above and be wrong here.
	/// </remarks>
	[Fact]
	public void ADeathWithNoPlayerBehindItLeavesNothing()
	{
		using BossAiHarness harness = NewHarness();
		Npc box = harness.Spawn(PlayerKillOnly, 300f, 300f, 200f);
		Npc other = harness.Spawn(AnyDeath, 303f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(box, other);

		BossAiHarness.Kill(box, other);
		box.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(0, Count(harness, PlayerKillLeaves));
	}

	/// <summary><b>An <c>on_die</c> spawn does not care who did it.</b></summary>
	[Fact]
	public void APlainDeathSpawnDoesNotCareWhoKilledIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc npc = harness.Spawn(AnyDeath, 300f, 300f, 200f);
		Npc killer = harness.Spawn(PlayerKillOnly, 303f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(npc, killer);

		BossAiHarness.Kill(npc, killer);
		npc.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(1, Count(harness, AnyDeathLeaves));
	}

	/// <summary><b>Every npc in the table is bound to the class that runs it.</b></summary>
	/// <remarks>
	/// The same two-way check the rotation table carries, for the same reason: a binding with no rungs
	/// reads as ported and behaves as plain <c>aggressive</c>, and a table row with no binding is a
	/// death spawn that exists and never runs. <see cref="DeathSpawnAI"/>'s nine hand-read npcs are
	/// bound as well but are not in this table, so this checks one direction on them.
	/// </remarks>
	[Fact]
	public void EveryNpcInTheTableIsBound()
	{
		string path = Path.Combine(BossAiHarness.RepoRoot(),
			"game-server", "data", "static_data", "npcs", "npc_templates.xml");
		string templates = File.ReadAllText(path);

		foreach (int npc in DeathSpawns.Npcs)
		{
			Assert.Contains($"npc_id=\"{npc}\"", templates);
			int at = templates.IndexOf($"npc_id=\"{npc}\"", StringComparison.Ordinal);
			int end = templates.IndexOf('>', at);
			Assert.Contains("ai=\"death_spawn\"", templates[at..end]);
		}
	}
	/// <summary><b>Tiamat's death opens the gate that brings Kahrun in.</b></summary>
	/// <remarks>
	/// The two halves of the conditional spawn engine, finally joined. The reading half --
	/// <see cref="GatedSpawnController"/> -- was built long ago and tested against a store written by
	/// hand, because nothing in the port wrote one. The writers turn out to live largely on death:
	/// <b>521 of this table's 960 actions are <c>set_condition_spawn_variable</c></b>, and 82 of the
	/// 101 variables they write are read by real gates, covering 5,082 of retail's 21,096 gated
	/// placements.
	/// <para>
	/// <c>IDTiamat_Hard_Tiamat_Dragon_Dying</c> writes <c>KAHRUN_SPAWN = 4</c> as it dies, and retail
	/// gates <b>70 placements</b> on it. This pin runs the real pattern into the real registry and asks
	/// the real controller what appeared -- no hand-written store anywhere in it.
	/// </para>
	/// </remarks>
	[Fact]
	public void TiamatsDeathOpensTheGateThatBringsKahrunIn()
	{
		using BossAiHarness harness = NewHarness();
		SpawnVariables store = SpawnVariableRegistry.For(AnyMap, harness.InstanceId);
		using var gated = new GatedSpawnController(AnyMap, harness.InstanceId, store,
			[new GatedSpawn(AnyDeathLeaves, 500f, 500f, 200f, 0, 0, true,
				SpawnCondition.Parse("KAHRUN_SPAWN == 4"))]);
		gated.Refresh();
		Assert.Equal(0, gated.Placed);

		Npc tiamat = harness.Spawn(Tiamat, 300f, 300f, 200f);
		tiamat.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(4, store["KAHRUN_SPAWN"]);
		Assert.Equal(1, gated.Placed);
	}
	/// <summary><b>An npc kill leaves what retail leaves for an npc kill.</b></summary>
	/// <remarks>
	/// The previous entry recorded this as unreachable from the harness, because a dying npc's aggro
	/// list only accepts an attacker it is aware of or hostile to, and two npcs of unrelated tribes are
	/// neither. That was true of the pair being used, not of the harness: <b>the tribe data contains
	/// real hostile pairs</b>, and one of them is this very guard. <c>GAB1_SUB_DEST_70</c> is hostile to
	/// <c>GAB1_01_POINT_01</c>, so the damage lands and the branch fires.
	/// <para>
	/// The hostility is asserted rather than assumed. If the tribe relations change, this fails saying
	/// the pair is no longer hostile instead of quietly becoming a test that proves nothing -- which is
	/// what it would have been had the pairing been left implicit.
	/// </para>
	/// </remarks>
	[Fact]
	public void AnNpcKillLeavesTheNpcKillAdd()
	{
		using BossAiHarness harness = NewHarness();
		Npc guard = harness.Spawn(NpcKilled, 300f, 300f, 200f);
		Npc slayer = harness.Spawn(HostileToTheGuard, 303f, 300f, 200f);
		Assert.True(
			DataManager.TRIBE_RELATIONS_DATA.IsHostileRelation(guard.GetTribe(), slayer.GetTribe()),
			"the pair this pin relies on is no longer hostile, so nothing it asserts means anything");

		BossAiHarness.MakeMutuallyKnown(guard, slayer);
		BossAiHarness.Wound(guard, slayer);
		guard.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(1, Count(harness, NpcKilledLeaves));
	}

	/// <summary><b>The npc-kill branches exist and carry their guard.</b></summary>
	/// <remarks>
	/// The count, over the whole table, beside the two behavioural pins that show one branch firing and
	/// one correctly not. A table that quietly lost its npc-kill branches would still pass those two if
	/// the one npc they use kept its own.
	/// </remarks>
	[Fact]
	public void TheNpcKillBranchesCarryTheirGuard()
	{
		string path = Path.Combine(BossAiHarness.RepoRoot(),
			"tools", "client-extract", "out", "death_spawns.tsv");
		string[] lines = File.ReadAllLines(path);
		string[] header = lines[0].Split('	');
		int killerAt = Array.IndexOf(header, "killer");

		int npcKills = lines.Skip(1).Count(line => line.Split('	')[killerAt] == "KilledByNpc");

		Assert.Equal(615, npcKills);
	}

	/// <summary><b>And it does not fire when nothing killed it at all.</b></summary>
	/// <remarks>
	/// The distinction that makes this a real condition rather than "no player did it". An npc that
	/// expires, or that nothing ever touched, has no top damager -- reading the absence of a player as
	/// the presence of an npc would fire these branches on every quiet despawn in the game.
	/// </remarks>
	[Fact]
	public void AnUntouchedDeathLeavesNothing()
	{
		using BossAiHarness harness = NewHarness();
		Npc guard = harness.Spawn(NpcKilled, 300f, 300f, 200f);

		guard.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(0, Count(harness, NpcKilledLeaves));
	}
	/// <summary><b>The harness can find an enemy for an npc without one being named by hand.</b></summary>
	/// <remarks>
	/// <see cref="AnNpcKillLeavesTheNpcKillAdd"/> names its slayer, which is clearer for that pin and
	/// useless for the next one. This checks the general route works, so the next npc-kill pin does not
	/// have to repeat the search through the tribe data that this entry did by hand.
	/// </remarks>
	[Fact]
	public void TheHarnessFindsAnEnemyOnItsOwn()
	{
		using BossAiHarness harness = NewHarness();
		Npc guard = harness.Spawn(NpcKilled, 300f, 300f, 200f);

		Npc enemy = harness.SpawnEnemyOf(guard, 305f, 300f, 200f);

		Assert.NotEqual(guard.GetTribe(), enemy.GetTribe());
		BossAiHarness.Wound(guard, enemy);
		guard.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(1, Count(harness, NpcKilledLeaves));
	}
}
