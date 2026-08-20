using System.IO;
using System;
using System.Linq;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

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

	/// <summary><c>IDDF3_NamedNWi</c>: leaves something however it died.</summary>
	private const int AnyDeath = 213778;

	private const int AnyDeathLeaves = 282155;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AnyMap).WithWorldSize(4096)
			.WithAi(typeof(DeathSpawnAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

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
}
