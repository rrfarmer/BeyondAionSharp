using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Combat rotations: the adds a boss puts on the ground during the fight, which never appeared here.
/// </summary>
/// <remarks>
/// Retail bosses are not HP ladders. Entering combat arms a battle timer, and the branch that timer
/// fires arms the next link itself, so a fight is a chain of timers. <c>IDAbRe_Core_FlyingWorm_02</c>
/// below is that shape at its smallest: ten seconds after the fight starts it places three worms and
/// arms itself again for fifteen.
/// <para>
/// The engine was always here -- thirty battle-timer slots in <see cref="PatternAi"/>, combat-gated and
/// cancelled on death. These pins are about the data reaching it, and about the two properties that
/// distinguish a battle timer from an idle one: <b>it does not run out of combat</b>, and <b>it stops
/// when the fight does</b>.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class BattleCycleAiTests
{
	private const int AnyMap = 300520000;

	/// <summary>An Abyssal core worm: arms at 10s, spawns three adds, re-arms at 15s.</summary>
	private const int Worm = 219549;

	/// <summary>The two adds it places -- two of the first, one of the second.</summary>
	private const int Swarm = 283215;

	private const int Straggler = 283216;

	/// <summary><c>IDYun_Nmd1</c>: two timers, one that casts and one that summons.</summary>
	private const int Caster = 217307;

	/// <summary>The five adds his second timer places.</summary>
	private const int Retinue = 217301;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AnyMap).WithWorldSize(4096)
			.WithAi(typeof(BattleCycleAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(npc => npc.GetNpcId() == npcId);

	/// <summary><b>An npc nobody is fighting never starts its rotation.</b></summary>
	/// <remarks>
	/// Note what this does and does not pin. Nothing arms the timer out of combat, so this catches a
	/// rotation wired to the wrong handler -- but <b>not</b> the combat gate on firing, which cannot
	/// bite on a timer that was never armed. <see cref="ALeftFightStopsTheRotationMidFlight"/> is the
	/// one that pins the gate.
	/// </remarks>
	[Fact]
	public void NothingHappensWhileNobodyIsFighting()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Worm, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromMinutes(2));

		Assert.Equal(0, Count(harness, Swarm));
		Assert.Equal(0, Count(harness, Straggler));
	}

	/// <summary><b>A timer already ticking does not fire once the fight is over.</b></summary>
	/// <remarks>
	/// The gate that makes a battle timer different from an idle one, pinned at the only moment it can
	/// be observed: armed, still pending, and then the fight ends. Without it a boss that resets keeps
	/// spawning adds at an empty room until its chain happens to stop.
	/// </remarks>
	[Fact]
	public void ALeftFightStopsTheRotationMidFlight()
	{
		using BossAiHarness harness = NewHarness();
		Npc worm = harness.Spawn(Worm, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(worm, player);
		harness.Engage(worm, player);

		// Armed for ten seconds; drop out of combat with it still pending.
		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		worm.GetAi().SetStateIfNot(Aion.GameServer.Ai.AIState.IDLE);
		harness.Clock.Advance(TimeSpan.FromSeconds(20));

		Assert.Equal(0, Count(harness, Swarm));
		Assert.Equal(0, Count(harness, Straggler));
	}

	/// <summary><b>Ten seconds into the fight, the adds arrive.</b></summary>
	[Fact]
	public void TheFightArmsTheTimerAndTheAddsFollow()
	{
		using BossAiHarness harness = NewHarness();
		Npc worm = harness.Spawn(Worm, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(worm, player);
		harness.Engage(worm, player);

		// Retail arms the first timer for ten seconds, so nothing is due at nine.
		harness.Clock.Advance(TimeSpan.FromSeconds(9));
		Assert.Equal(0, Count(harness, Swarm));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(2, Count(harness, Swarm));
		Assert.Equal(1, Count(harness, Straggler));
	}

	/// <summary><b>The rotation repeats: each firing arms the next.</b></summary>
	/// <remarks>
	/// A port that drops the re-arm gets one wave of a mechanic that should run all fight, which is the
	/// failure this table exists to prevent. The second wave is due fifteen seconds after the first.
	/// </remarks>
	[Fact]
	public void EachFiringArmsTheNextOne()
	{
		using BossAiHarness harness = NewHarness();
		Npc worm = harness.Spawn(Worm, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(worm, player);
		harness.Engage(worm, player);

		harness.Clock.Advance(TimeSpan.FromSeconds(11));
		Assert.Equal(2, Count(harness, Swarm));

		harness.Clock.Advance(TimeSpan.FromSeconds(15));
		Assert.Equal(4, Count(harness, Swarm));
		Assert.Equal(2, Count(harness, Straggler));
	}

	/// <summary><b>Two timers run at once without disturbing each other.</b></summary>
	/// <remarks>
	/// The indicator is the whole point of retail's design and the thing an HP-ladder port cannot
	/// express. <c>IDYun_Nmd1</c> arms two: timer 0 shouts and casts at fifteen seconds, timer 1 shouts
	/// and places five adds at twenty, and each re-arms on its own schedule. A port that collapsed them
	/// into one clock would fire both together and look plausible while being wrong.
	/// <para>
	/// The casts are the part that was impossible until this week: <c>SKILLI_INDEX</c> resolves against
	/// the npc's own skill list, which was only found in the 5.8 server dump.
	/// </para>
	/// </remarks>
	[Fact]
	public void TwoTimersKeepTheirOwnSchedules()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Caster, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);

		// Timer 0 is due at fifteen seconds, timer 1 not until twenty.
		harness.Clock.Advance(TimeSpan.FromSeconds(16));
		Assert.Equal([19698, 19695], BossAiHarness.DrainQueuedSkills(boss).Select(c => c.SkillId));
		Assert.Equal(0, Count(harness, Retinue));

		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		Assert.Equal(5, Count(harness, Retinue));
		Assert.Empty(BossAiHarness.DrainQueuedSkills(boss));
	}

	/// <summary><b>A cast names the creature retail named.</b></summary>
	/// <remarks>
	/// Retail says <c>OBJI_CUR_TARGET</c> for the first and <c>OBJI_SELF</c> for the second, and a
	/// rotation that self-buffed with its attack would still pass a test that only counted casts.
	/// </remarks>
	[Fact]
	public void EachCastKeepsRetailsTarget()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Caster, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);

		harness.Clock.Advance(TimeSpan.FromSeconds(16));

		Assert.Equal(
			[NpcSkillTargetAttribute.MOST_HATED, NpcSkillTargetAttribute.ME],
			BossAiHarness.DrainQueuedSkills(boss).Select(cast => cast.Target));
	}

	/// <summary><b>The table only carries rotations something arms.</b></summary>
	/// <remarks>
	/// A cycle rung whose timer is never armed is inert, and a table full of them would look ported
	/// while doing nothing. 82 retail rotations are refused for exactly this reason -- they arm from
	/// <c>on_message</c> or <c>on_attacked</c>, handlers this table does not read.
	/// </remarks>
	[Fact]
	public void EveryRotationHasSomethingThatStartsIt()
	{
		foreach (int npc in BattleCycles.Npcs)
			Assert.NotEmpty(BattleCycles.ArmingRungsFor(npc));
	}

	/// <summary><b>Every cast names a skill this port actually has.</b></summary>
	/// <remarks>
	/// 4,810 casts across 1,858 npcs, none of them read by a human. The index they came from is only
	/// meaningful against one npc's list, so a resolver bug would not produce nonsense -- it would
	/// produce a <i>real skill belonging to somebody else</i>, which no smoke test would notice. This
	/// at least holds the line that every id is castable here; <see cref="NpcSkillListTests"/> is what
	/// argues the indices are the right ones.
	/// </remarks>
	[Fact]
	public void EveryCastNamesAKnownSkill()
	{
		using BossAiHarness harness = NewHarness();
		int casts = 0;
		foreach (string[] fields in Rows("skill"))
		{
			casts++;
			int skill = int.Parse(fields[0]);
			Assert.True(DataManager.SKILL_DATA.GetSkillTemplate(skill) != null,
				$"skill {skill} is in skill_templates.xml but SkillData did not load it");
		}

		Assert.Equal(4810, casts);
	}

	/// <summary><b>Every timer sits in one of retail's thirty slots.</b></summary>
	/// <remarks>
	/// <see cref="PatternAi.ArmTimer"/> throws outside 0..29, so a bad indicator would take the npc
	/// down mid-fight rather than misbehave quietly. Cheaper to catch here than in a raid.
	/// </remarks>
	[Fact]
	public void EveryTimerSlotIsOneRetailHas()
	{
		foreach (string[] fields in Rows("arm"))
		{
			int slot = int.Parse(fields[0]);
			Assert.InRange(slot, 0, 29);
		}
	}

	/// <summary>The (a1, a2) pair of every row of one action kind.</summary>
	private static IEnumerable<string[]> Rows(string kind)
	{
		string path = Path.Combine(BossAiHarness.RepoRoot(),
			"tools", "client-extract", "out", "battle_cycles.tsv");
		string[] lines = File.ReadAllLines(path);
		string[] header = lines[0].Split('	');
		int kindAt = Array.IndexOf(header, "kind");
		int firstAt = Array.IndexOf(header, "a1");
		int secondAt = Array.IndexOf(header, "a2");

		foreach (string line in lines.Skip(1))
		{
			string[] fields = line.Split('	');
			if (fields[kindAt] == kind)
				yield return [fields[firstAt], fields[secondAt]];
		}
	}

	/// <summary><b>Every spawn names an npc this port can actually place.</b></summary>
	[Fact]
	public void EverySpawnNamesAKnownNpc()
	{
		using BossAiHarness harness = NewHarness();
		string path = Path.Combine(BossAiHarness.RepoRoot(),
			"tools", "client-extract", "out", "battle_cycles.tsv");
		string[] lines = File.ReadAllLines(path);
		string[] header = lines[0].Split('\t');
		int kind = Array.IndexOf(header, "kind");
		int first = Array.IndexOf(header, "a1");

		int spawns = 0;
		foreach (string line in lines.Skip(1))
		{
			string[] fields = line.Split('\t');
			if (fields[kind] != "spawn")
				continue;
			spawns++;
			Assert.NotNull(DataManager.NPC_DATA.GetNpcTemplate(int.Parse(fields[first])));
		}

		Assert.Equal(34, spawns);
	}
}
