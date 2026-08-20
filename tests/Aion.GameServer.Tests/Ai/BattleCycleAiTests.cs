using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
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

	/// <summary>An npc whose add retail does <i>not</i> scope to the fight, and never despawns.</summary>
	/// <remarks>
	/// This pin used to use <c>IDTP_Fanatic_Boss_EL</c>, until reading <c>on_die</c> revealed that its
	/// pattern despawns that add explicitly on death. The add did start outliving its summoner and
	/// stopped, correctly, because the table became more faithful -- so the pin needed an npc whose
	/// pattern really does leave one behind, not a different assertion.
	/// </remarks>
	private const int Fanatic = 296445;

	/// <summary>The add it summons and never takes away.</summary>
	private const int FanaticAdd = 281472;

	/// <summary>A plain attack skill, used to watch how a cast picks its creature.</summary>
	private const int RaidSkill = 17063;

	/// <summary>
	/// <c>ND2_FhO</c>'s own self-cast: a skill whose <c>first_target</c> is <c>TARGET</c>, aimed at
	/// <c>ME</c>.
	/// </summary>
	/// <remarks>
	/// The combination matters. A skill whose <c>first_target</c> is already <c>ME</c> never reaches the
	/// target switch at all -- an earlier branch points it at the caster -- so pinning one of those
	/// proves nothing about <see cref="NpcSkillTargetAttribute.ME"/>. A mutation aiming self-casts at
	/// the tank survived a pin written with such a skill. This one is 16858, which retail's
	/// <c>ND2_FhO</c> really does cast on itself, and it does go through the switch.
	/// </remarks>
	private const int SelfCast = 16858;

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
	/// while doing nothing. Entering combat is the usual trigger but not the only one -- retail also
	/// starts a chain from a message, a hit, a spell or waking -- so this accepts any of the seven.
	/// It is deliberately not <c>ArmingRungsFor</c> alone: that version passed only because the
	/// extractor read one handler, and it failed the moment the others were added, which is what a
	/// pin should do.
	/// </remarks>
	[Fact]
	public void EveryRotationHasSomethingThatStartsIt()
	{
		foreach (int npc in BattleCycles.Npcs)
		{
			Assert.True(
				BattleCycles.ArmingRungsFor(npc).Length > 0
				|| BattleCycles.MessageRungsFor(npc).Length > 0
				|| BattleCycles.AttackedRungsFor(npc).Length > 0
				|| BattleCycles.SpelledRungsFor(npc).Length > 0
				|| BattleCycles.WakeRungsFor(npc).Length > 0
				|| BattleCycles.SeeNpcRungsFor(npc).Length > 0
				|| BattleCycles.SeeUserRungsFor(npc).Length > 0,
				$"npc {npc} has a rotation but nothing that arms it");
		}
	}

	/// <summary><b>The npcs bound to this class are exactly the ones the table drives.</b></summary>
	/// <remarks>
	/// Both halves matter and both have already gone wrong. An npc bound with no rungs is a lie in the
	/// data -- it reads as ported and behaves as plain <c>aggressive</c> -- and 355 of those appeared
	/// the first time the table shrank, because rebinding is additive and nothing took the old ones
	/// back. An npc in the table with no binding is a rotation that exists and never runs.
	/// </remarks>
	/// <summary>Every class that fills its slots from <c>GeneratedPattern</c>.</summary>
	/// <remarks>
	/// This used to name one class, because one table meant one class. It cannot any more: the tables
	/// stopped being mutually exclusive, so an npc with a rotation may be bound to whichever of these
	/// its aggression calls for, and asserting it is on <c>battle_cycle</c> specifically would now be
	/// asserting the old wall is still standing.
	/// <para>
	/// The check that matters is unchanged and is still two-way: a table row with no binding is a
	/// mechanic that exists and never runs, which is exactly how the rotation table lost npcs the first
	/// time it shrank.
	/// </para>
	/// </remarks>
	private static readonly string[] Composing =
	[
		"battle_cycle", "death_spawn", "idle_cycle", "idle_cycle_passive",
		"aggressive_pattern", "passive_pattern", "wake_variable", "wake_variable_aggressive",
	];

	[Fact]
	public void TheBindingsAndTheTableAgree()
	{
		string path = Path.Combine(BossAiHarness.RepoRoot(),
			"game-server", "data", "static_data", "npcs", "npc_templates.xml");
		HashSet<int> bound = new HashSet<int>();
		foreach (Match element in Regex.Matches(File.ReadAllText(path), "<npc_template [^>]*>"))
		{
			if (!Composing.Any(name => element.Value.Contains($"ai=\"{name}\"")))
				continue;
			bound.Add(int.Parse(Regex.Match(element.Value, "npc_id=\"([0-9]+)\"").Groups[1].Value));
		}

		// One-way, and deliberately so. Every npc with a rotation must be bound to something that
		// runs it; the reverse is no longer a defect, because a composing class is also where an npc
		// with only a wake rung or only a death spawn now lives.
		Assert.Empty(BattleCycles.Npcs.Where(id => !bound.Contains(id)));
	}

	/// <summary><b>Every cast names a skill this port actually has.</b></summary>
	/// <remarks>
	/// 57,908 casts across 13,488 npcs, none of them read by a human. The index they came from is only
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

		Assert.Equal(57908, casts);
	}

	/// <summary><b>Extending the skill-target enum did not renumber what was already in it.</b></summary>
	/// <remarks>
	/// <c>LOWEST_HP</c> and <c>MOST_HP</c> were appended so a boss can cast at whoever is closest to
	/// dying, not merely turn to face them. CLAUDE.md flags this exact hazard: Java compares this enum
	/// by <c>ordinal()</c> and C# by its integer value, so inserting a member anywhere but the end
	/// silently repoints every one after it -- npc_skills entries would keep their names and change
	/// their meaning. This pins the members that existed before.
	/// </remarks>
	[Fact]
	public void TheSkillTargetEnumKeptItsOldNumbering()
	{
		Assert.Equal(0, (int)NpcSkillTargetAttribute.FRIEND);
		Assert.Equal(1, (int)NpcSkillTargetAttribute.ME);
		Assert.Equal(2, (int)NpcSkillTargetAttribute.MOST_HATED);
		Assert.Equal(3, (int)NpcSkillTargetAttribute.SECOND_MOST_HATED);
		Assert.Equal(4, (int)NpcSkillTargetAttribute.THIRD_MOST_HATED);
		Assert.Equal(5, (int)NpcSkillTargetAttribute.RANDOM);
		Assert.Equal(6, (int)NpcSkillTargetAttribute.RANDOM_EXCEPT_CURRENT_TARGET);
		Assert.Equal(7, (int)NpcSkillTargetAttribute.NONE);
	}

	/// <summary><b>A boss that retail aims at the weakest carries that target, not the most-hated.</b></summary>
	/// <remarks>
	/// <b>This pins the table, not the resolution.</b> The harness drains queued skills instead of
	/// executing them, so <c>SkillAttackManager</c>'s enum-to-<c>AggroTarget</c> mapping -- the two
	/// lines added for this -- is not reached by any pin here. The ranking those lines delegate to is
	/// covered, in <c>FrostmaneLestinAiTests</c>, through the target-switch path.
	/// </remarks>
	[Fact]
	public void AWeakestTargetCastKeepsThatTarget()
	{
		int lowest = 0;
		foreach (string[] fields in Rows("skill"))
		{
			if (fields[2] == "LOWEST_HP")
				lowest++;
		}

		Assert.Equal(157, lowest);
	}

	/// <summary><b>A weakest-target cast actually lands on the weakest creature.</b></summary>
	/// <remarks>
	/// The pin the previous entry could not write. <see cref="BossAiHarness.FireNextQueuedSkill"/> runs
	/// the queued cast through the real <c>SkillAttackManager</c>, so the
	/// <see cref="NpcSkillTargetAttribute"/> is turned into an actual creature the way it is in a fight.
	/// <para>
	/// The tank is the one being fought and is <b>not</b> the answer: the boss must leave the creature
	/// holding it and reach past for the one closest to dying. A mutation pointing this at the
	/// most-hated creature used to survive the entire suite.
	/// </para>
	/// </remarks>
	[Fact]
	public void AWeakestTargetCastPicksTheWeakestCreature()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Caster, 300f, 300f, 200f);
		Player tank = harness.SpawnPlayer(302f, 300f, 200f);
		Player wounded = harness.SpawnPlayer(303f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, tank);
		BossAiHarness.MakeMutuallyKnown(boss, wounded);
		harness.Engage(boss, tank);
		BossAiHarness.Rehate(boss, wounded);
		BossAiHarness.Rehate(boss, tank);

		// The tank holds it; somebody else is nearly dead.
		BossAiHarness.SetExactPercent(wounded, 5);
		Assert.Same(tank, boss.GetTarget());

		boss.QueueSkill(RaidSkill, 1, 0, NpcSkillTargetAttribute.LOWEST_HP);

		Assert.Same(wounded, BossAiHarness.FireNextQueuedSkill(boss));
	}

	/// <summary><b>And a most-hated cast still lands on the tank.</b></summary>
	/// <remarks>
	/// The other half. Without it the pin above passes for a resolver that always answers "whoever is
	/// most hurt", which would be just as wrong in the other direction.
	/// </remarks>
	[Fact]
	public void AMostHatedCastStillPicksTheTank()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Caster, 300f, 300f, 200f);
		Player tank = harness.SpawnPlayer(302f, 300f, 200f);
		Player wounded = harness.SpawnPlayer(303f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, tank);
		BossAiHarness.MakeMutuallyKnown(boss, wounded);
		harness.Engage(boss, tank);
		// Both must be on the hate list, or the two target modes cannot disagree and this pins nothing:
		// a mutation swapping most-hated for weakest survived until the wounded one was really in the fight.
		BossAiHarness.Rehate(boss, wounded);
		BossAiHarness.Rehate(boss, tank);
		BossAiHarness.SetExactPercent(wounded, 5);

		boss.QueueSkill(RaidSkill, 1, 0, NpcSkillTargetAttribute.MOST_HATED);

		Assert.Same(tank, BossAiHarness.FireNextQueuedSkill(boss));
	}

	/// <summary><b>The hate-ranked modes each pick their own place in the list.</b></summary>
	/// <remarks>
	/// Second and third exist because retail's <c>ATTACKERI_SECOND_HATING</c> and <c>THIRD_HATING</c>
	/// are 725 and 281 uses across the dump -- a boss reaching past the tank for the healer behind him.
	/// Ranked by hate, so the order is built explicitly here rather than assumed from spawn order.
	/// </remarks>
	[Fact]
	public void TheHateRankedModesEachPickTheirOwnPlace()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Caster, 300f, 300f, 200f);
		Player first = harness.SpawnPlayer(302f, 300f, 200f);
		Player second = harness.SpawnPlayer(303f, 300f, 200f);
		Player third = harness.SpawnPlayer(304f, 300f, 200f);
		foreach (Player who in new[] { first, second, third })
			BossAiHarness.MakeMutuallyKnown(boss, who);

		harness.Engage(boss, first);
		boss.GetAggroList().AddHate(third, 100);
		boss.GetAggroList().AddHate(second, 200);
		boss.GetAggroList().AddHate(first, 300);

		boss.QueueSkill(RaidSkill, 1, 0, NpcSkillTargetAttribute.SECOND_MOST_HATED);
		Assert.Same(second, BossAiHarness.FireNextQueuedSkill(boss));

		boss.QueueSkill(RaidSkill, 1, 0, NpcSkillTargetAttribute.THIRD_MOST_HATED);
		Assert.Same(third, BossAiHarness.FireNextQueuedSkill(boss));
	}

	/// <summary><b>A cast on itself does not wander onto the raid.</b></summary>
	[Fact]
	public void ASelfCastStaysOnTheCaster()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Caster, 300f, 300f, 200f);
		Player tank = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, tank);
		harness.Engage(boss, tank);

		boss.QueueSkill(SelfCast, 1, 0, NpcSkillTargetAttribute.ME);

		Assert.Same(boss, BossAiHarness.FireNextQueuedSkill(boss));
	}

	/// <summary><b>A friendly cast finds the other npc, not the raid attacking it.</b></summary>
	/// <remarks>
	/// The one mode that does not read the hate list at all -- it searches the known list for a living
	/// npc the caster is not hostile to. Retail uses it for the buffs and heals a boss puts on its own
	/// adds, so a mutation pointing it at the hate list would turn a heal into an attack.
	/// </remarks>
	[Fact]
	public void AFriendlyCastFindsTheOtherNpc()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Caster, 300f, 300f, 200f);
		Npc ally = harness.Spawn(Caster, 303f, 300f, 200f);
		Player tank = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, ally);
		BossAiHarness.MakeMutuallyKnown(boss, tank);
		harness.Engage(boss, tank);

		boss.QueueSkill(SelfCast, 1, 0, NpcSkillTargetAttribute.FRIEND);

		Assert.Same(ally, BossAiHarness.FireNextQueuedSkill(boss));
	}

	/// <summary><b>A random cast still has to land on somebody in the fight.</b></summary>
	/// <remarks>
	/// Randomness does not have to make a pin vague. With exactly one creature on the hate list the
	/// answer is forced, so this asserts the part that is not random: <b>a random pick comes from the
	/// hate list</b>. A mutation returning the caster, or nobody, fails here without any roll seam.
	/// </remarks>
	[Fact]
	public void ARandomCastComesFromTheHateList()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Caster, 300f, 300f, 200f);
		Player only = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, only);
		harness.Engage(boss, only);

		boss.QueueSkill(RaidSkill, 1, 0, NpcSkillTargetAttribute.RANDOM);

		Assert.Same(only, BossAiHarness.FireNextQueuedSkill(boss));
	}

	/// <summary><b>"Anyone but the one I am fighting" really excludes them.</b></summary>
	/// <remarks>
	/// The same trick with the exclusion made to carry the whole answer: two creatures hated, one of
	/// them the current target, so the only admissible pick is the other. This is the mode retail uses
	/// to make a boss spin onto somebody who is not the tank, and a mutation that ignores the exclusion
	/// leaves it hitting the tank forever.
	/// </remarks>
	[Fact]
	public void ARandomCastThatExcludesTheTargetPicksSomebodyElse()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Caster, 300f, 300f, 200f);
		Player tank = harness.SpawnPlayer(302f, 300f, 200f);
		Player other = harness.SpawnPlayer(303f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, tank);
		BossAiHarness.MakeMutuallyKnown(boss, other);
		harness.Engage(boss, tank);
		boss.GetAggroList().AddHate(other, 100);
		boss.GetAggroList().AddHate(tank, 500);
		Assert.Same(tank, boss.GetTarget());

		boss.QueueSkill(RaidSkill, 1, 0, NpcSkillTargetAttribute.RANDOM_EXCEPT_CURRENT_TARGET);

		Assert.Same(other, BossAiHarness.FireNextQueuedSkill(boss));
	}

	/// <summary><b>A cast aimed at one creature keeps it, whatever the hate list says.</b></summary>
	/// <remarks>
	/// The whole point of <c>AimedSkillEntry</c>. Retail's role targets name the creature involved in
	/// the event -- whoever started the fight, whoever just hit us -- and this port could only resolve
	/// a target when the queue drained, out of the aggro list, which finds whoever is convenient by
	/// then. Here the tank is most-hated by a wide margin and the aim is somebody else entirely.
	/// </remarks>
	[Fact]
	public void AnAimedCastKeepsItsCreature()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Caster, 300f, 300f, 200f);
		Player tank = harness.SpawnPlayer(302f, 300f, 200f);
		Player other = harness.SpawnPlayer(303f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, tank);
		BossAiHarness.MakeMutuallyKnown(boss, other);
		harness.Engage(boss, tank);
		boss.GetAggroList().AddHate(tank, 5000);
		Assert.Same(tank, boss.GetTarget());

		((PatternAi)boss.GetAi()).CastSkillAt(other, RaidSkill);

		Assert.Same(other, BossAiHarness.FireNextQueuedSkill(boss));
	}

	/// <summary><b>Whoever opened the fight is remembered, not re-derived.</b></summary>
	/// <remarks>
	/// <c>OBJI_EVENT_TARGET</c> is 1,912 uses and the single largest role in the dump. The tempting
	/// shortcut is to call it the most-hated creature, which is true at the instant combat starts and
	/// false a moment later; this pins that the npc still knows who opened on it after somebody else
	/// has taken the top of the hate list.
	/// </remarks>
	[Fact]
	public void TheOneWhoOpenedTheFightIsStillKnownLater()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Caster, 300f, 300f, 200f);
		Player opener = harness.SpawnPlayer(302f, 300f, 200f);
		Player tank = harness.SpawnPlayer(303f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, opener);
		BossAiHarness.MakeMutuallyKnown(boss, tank);

		harness.Engage(boss, opener);
		// The tank takes over well after the pull. (Hate moves immediately; the npc's current target
		// only follows on its next think, which is why this asserts the list rather than the target.)
		boss.GetAggroList().AddHate(tank, 9000);
		Assert.Same(tank, boss.GetAggroList().GetTarget(AggroTarget.MOST_HATED));

		PatternAi ai = (PatternAi)boss.GetAi();
		Assert.Same(opener, ai.EventTarget);

		ai.CastSkillAt(ai.EventTarget, RaidSkill);
		Assert.Same(opener, BossAiHarness.FireNextQueuedSkill(boss));
	}

	/// <summary><b>A role with nobody in it does not fall back to the tank.</b></summary>
	/// <remarks>
	/// <c>on_spelled</c> can run with no caster left. A cast with no target is not a cast at the
	/// most-hated creature -- it is a cast that does not happen -- and quietly redirecting it would
	/// turn a missing target into an attack on whoever is nearest.
	/// </remarks>
	[Fact]
	public void ACastWithNobodyToAimAtDoesNotHappen()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Caster, 300f, 300f, 200f);
		Player tank = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, tank);
		harness.Engage(boss, tank);

		((PatternAi)boss.GetAi()).CastSkillAt(null, RaidSkill);

		Assert.Null(BossAiHarness.FireNextQueuedSkill(boss));
	}

	/// <summary><b>No rung re-arms, with no delay, the very timer that fired it.</b></summary>
	/// <remarks>
	/// The one shape that spins. <c>ArmTimer</c> with a zero delay fires on the next pool tick, so a
	/// branch guarded by timer N that arms N with zero would re-enter itself as fast as the pool
	/// allows, taking a thread with it. Retail does write zero delays -- 5 of its 31,442
	/// <c>add_battle_timer</c> uses, 10 rows here -- and today every one of them either arms a
	/// <i>different</i> slot or sits behind a one-shot flag, so none of them spins.
	/// <para>
	/// <b>That is luck, not a rule</b>, which is why it is pinned. The table is regenerated whenever
	/// the extractor changes, and this is the check that says whether the next regeneration introduced
	/// something that will hang a live server rather than fail a test.
	/// </para>
	/// </remarks>
	[Fact]
	public void NoRungReArmsItsOwnTimerWithNoDelay()
	{
		string path = Path.Combine(BossAiHarness.RepoRoot(),
			"tools", "client-extract", "out", "battle_cycles.tsv");
		string[] lines = File.ReadAllLines(path);
		string[] header = lines[0].Split('	');
		int kindAt = Array.IndexOf(header, "kind");
		int guardsAt = Array.IndexOf(header, "guards");
		int slotAt = Array.IndexOf(header, "a1");
		int delayAt = Array.IndexOf(header, "a2");
		int npcAt = Array.IndexOf(header, "npc");

		foreach (string line in lines.Skip(1))
		{
			string[] fields = line.Split('	');
			if (fields[kindAt] != "arm" || fields[delayAt] != "0")
				continue;

			// A zero-delay arm is only dangerous when it re-arms the slot whose firing ran this branch.
			Assert.DoesNotContain($"timer:{fields[slotAt]}", fields[guardsAt].Split('|'));
		}
	}

	/// <summary><b>Adds that belong to the fight are gone when the fight is.</b></summary>
	/// <remarks>
	/// Retail marks a spawn <c>despawn_at_attack_state</c> when the add belongs to the encounter rather
	/// than to the world -- <b>12,614 of its 16,343 spawns, and 7,690 of those are permanent</b>. This
	/// port dropped the field, so every one of them stayed on the ground forever once the boss reset. A
	/// summoner on a one-second timer fought for ten minutes left six hundred behind.
	/// <para>
	/// The worm's three adds carry a live time, so this uses the fight ending rather than waiting them
	/// out: they are alive, the fight stops, and they go with it.
	/// </para>
	/// </remarks>
	[Fact]
	public void AddsThatBelongToTheFightLeaveWithIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc worm = harness.Spawn(Worm, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(worm, player);
		harness.Engage(worm, player);

		harness.Clock.Advance(TimeSpan.FromSeconds(11));
		Assert.Equal(2, Count(harness, Swarm));
		Assert.Equal(1, Count(harness, Straggler));

		// The fight ends well inside their sixty-second lifetime. Killing the boss is one of the three
		// ways retail leaves attack state; going home and despawning are the others.
		BossAiHarness.Kill(worm, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(0, Count(harness, Swarm));
		Assert.Equal(0, Count(harness, Straggler));
	}

	/// <summary><b>And an add retail does not mark stays behind.</b></summary>
	/// <remarks>
	/// The other half, without which "remove them all" passes just as well as the real rule -- a
	/// mutation that ignored the flag and treated every add as fight-scoped survived until this
	/// existed. <c>IDTP_Fanatic_Boss_EL</c> summons a permanent add on entering combat and retail marks
	/// it <c>FALSE</c>: it belongs to the world, and killing the summoner does not take it away.
	/// </remarks>
	[Fact]
	public void AnAddRetailDoesNotMarkOutlivesItsSummoner()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Fanatic, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.Equal(1, Count(harness, FanaticAdd));

		// Fifty seconds of life left, and no branch that removes it.
		BossAiHarness.Kill(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(1, Count(harness, FanaticAdd));
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

	/// <summary>The (a1, a2, place) fields of every row of one action kind.</summary>
	private static IEnumerable<string[]> Rows(string kind)
	{
		string path = Path.Combine(BossAiHarness.RepoRoot(),
			"tools", "client-extract", "out", "battle_cycles.tsv");
		string[] lines = File.ReadAllLines(path);
		string[] header = lines[0].Split('	');
		int kindAt = Array.IndexOf(header, "kind");
		int firstAt = Array.IndexOf(header, "a1");
		int secondAt = Array.IndexOf(header, "a2");
		int placeAt = Array.IndexOf(header, "place");

		foreach (string line in lines.Skip(1))
		{
			string[] fields = line.Split('	');
			if (fields[kindAt] == kind)
				yield return [fields[firstAt], fields[secondAt], fields[placeAt]];
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

		Assert.Equal(1276, spawns);
	}
}
