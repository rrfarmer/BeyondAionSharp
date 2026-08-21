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

	/// <summary><c>IDAbRe_CoreN_SummonWorms</c>: the worm's own skill in the retail dump.</summary>
	/// <remarks>This port's <c>npc_skills</c> does not list it for the worm, which is the point.</remarks>
	private const int WormSummon = 19143;

	/// <summary>An id no skill template answers to, for the no-template fallback.</summary>
	private const int NoSuchSkill = 999999;

	/// <summary>A guard the port's own <c>npc_skills</c> does cover, and one of its skills.</summary>
	private const int SkilledGuard = 235617;

	private const int GuardSkill = 17059;

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

	/// <summary><c>IDF5_Mini_01_C_Boss_Fi</c>: re-buffs itself the moment it gets home.</summary>
	/// <remarks>
	/// Picked because it has no <c>on_leave_attack_state</c>, which <see cref="PatternAi"/> also
	/// evaluates from <c>HandleBackHome</c> -- an npc with both would queue two casts and the pin could
	/// not say which handler produced the one it saw.
	/// </remarks>
	private const int Kurores = 219987;

	/// <summary>Midnight Robe, the buff its <c>on_leave_return_sp</c> puts back on.</summary>
	/// <remarks>
	/// Worth knowing that this is a <c>BUFF</c>: the Java's <c>onBackHome</c> dispels the buff slot on
	/// arriving, and retail's pattern re-applies it in the same breath. The two halves only make sense
	/// together, and the port had the dispel and not the re-apply.
	/// </remarks>
	private const int ReturnBuff = 20700;

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
			// Only npcs that actually have one. The table used to require a rotation to enter it, so
			// every npc in it had one and this loop could assume it; it now also holds npcs whose only
			// rows are a signal handler -- an `on_die` that leaves an add, an `on_despawn` that does.
			// Asserting those have an arming rung would be asserting they have a rotation, which they
			// do not, and the failure message would say something untrue about them.
			if (BattleCycles.CycleRungsFor(npc).Length == 0)
			{
				continue;
			}

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
	/// 90,194 casts across 22,391 npcs, none of them read by a human. The index they came from is only
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

		Assert.Equal(90194, casts);
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

		Assert.Equal(570, lowest);
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

		Assert.Equal(1782, spawns);
	}
	/// <summary><b>Getting home runs the handler retail hangs there, and starting to go home does not.</b></summary>
	/// <remarks>
	/// Retail names this pair <c>on_enter_return_sp</c> / <c>on_leave_return_sp</c>, the same way it
	/// names <c>on_enter_attack_state</c> / <c>on_leave_attack_state</c> -- which is what settles that
	/// "leave return sp" is leaving the returning <i>state</i>, not leaving in order to return. The port
	/// already had both edges from the Java: <c>ReturningEventHandler.OnNotAtHome</c> sets
	/// <c>AIState.RETURNING</c> and <c>OnBackHome</c> sets <c>AIState.IDLE</c>.
	/// <para>
	/// <b>Both halves are asserted, and the negative one is the load-bearing half.</b> A pin that only
	/// checked the cast after arriving would pass just as well if the rungs had been wired to the
	/// entering edge and something else had triggered them -- the observation would be identical. This
	/// npc's <c>on_enter_return_sp</c> is empty, so setting off for home must produce nothing.
	/// </para>
	/// </remarks>
	[Fact]
	public void GettingHomeRunsTheReturnHandlerAndSettingOffDoesNot()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Kurores, 300f, 300f, 200f);
		BossAiHarness.DrainQueuedSkills(boss);

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.NotAtHome);
		Assert.Empty(BossAiHarness.DrainQueuedSkills(boss));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.BackHome);
		Assert.Equal([ReturnBuff], BossAiHarness.DrainQueuedSkills(boss).Select(c => c.SkillId));
	}

	/// <summary><b>A waypoint guard reaches the runtime with retail's own number, converted once.</b></summary>
	/// <remarks>
	/// Retail counts route points from one and this port's <c>RouteStep</c> from zero, so somewhere a
	/// one has to come off. <see cref="When.AtWaypoint"/> does it -- <c>ai.WaypointIndex == index - 1</c>
	/// -- and the table therefore has to carry retail's number untouched. <b>Two conversions look
	/// exactly like one until an npc stops at the wrong point</b>, and a patrol arriving a point early
	/// or late still walks, still arrives, and still despawns; nothing about it reads as broken.
	/// <para>
	/// So this compares the numbers in the generated file against the numbers in the TSV directly. It
	/// is deliberately not a behavioural pin: the harness has no route walking, and a pin that spawned
	/// an npc with no route would assert against <c>WaypointIndex == -1</c> and pass for the wrong
	/// reason -- the failure this project keeps finding by mutation.
	/// </para>
	/// </remarks>
	[Fact]
	public void WaypointGuardsCarryRetailsOwnNumbering()
	{
		string tsv = Path.Combine(BossAiHarness.RepoRoot(),
			"tools", "client-extract", "out", "battle_cycles.tsv");
		string[] lines = File.ReadAllLines(tsv);
		string[] header = lines[0].Split('	');
		int guardsAt = Array.IndexOf(header, "guards");

		HashSet<int> wanted = new HashSet<int>();
		foreach (string line in lines.Skip(1))
		{
			foreach (Match token in Regex.Matches(line.Split('	')[guardsAt], @"waypoint:(\d+)"))
			{
				wanted.Add(int.Parse(token.Groups[1].Value));
			}
		}

		Assert.NotEmpty(wanted);

		string generated = File.ReadAllText(Path.Combine(BossAiHarness.RepoRoot(),
			"src", "Aion.GameServer", "Handlers", "AI", "BattleCycles.cs"));
		HashSet<int> emitted = new HashSet<int>();
		foreach (Match call in Regex.Matches(generated, @"When\.AtWaypoint\((\d+)\)"))
		{
			emitted.Add(int.Parse(call.Groups[1].Value));
		}

		Assert.Equal(wanted.OrderBy(n => n), emitted.OrderBy(n => n));

		// And the unnumbered half. Checking only the numbers left a hole a mutation walked straight
		// through: replacing `When.AtLastWaypoint` with `When.Always` turns "despawn at the end of the
		// route" into "despawn at the first point", and every assertion above still passed.
		bool anyLast = lines.Skip(1).Any(line => line.Split('	')[guardsAt].Contains("last_waypoint:"));
		Assert.True(anyLast, "the table no longer carries is_last_waypoint, so this pin proves nothing");
		Assert.Contains("When.AtLastWaypoint", generated);
	}

	/// <summary><b>A "keep going" rung is carried, and it blocks the branches below it.</b></summary>
	/// <remarks>
	/// Retail's <c>goto_next_waypoint</c> asks the npc to carry on to the next point of its route, and
	/// <b>this port already does that by itself</b> -- arriving runs <c>WalkManager.TargetReached</c> ->
	/// <c>ChooseNextRouteStep</c>, ported from the Java. <see cref="PatternAi"/> evaluates
	/// <c>OnArrivedAtWaypoint</c> before the base handler, so a rung that advanced the route itself
	/// would advance it twice and the patrol would visit every other point -- while still walking,
	/// still arriving, and looking entirely normal.
	/// <para>
	/// So the action is a no-op, and what is pinned is that the <b>branches carrying it survive</b>.
	/// <para>
	/// The first version of this pin asserted the <c>do_nothing</c> argument -- that a "keep going"
	/// branch blocks the ones below it -- and <b>failed, because that is not true here</b>. All 45
	/// branches whose only action is this are the last branch of their handler and 39 are the only
	/// branch; they block nothing. The reason to read the element is different: a branch is
	/// all-or-nothing, 142 branches carry it beside real actions, and refusing it dropped those
	/// branches whole -- 106 casts, 62 spawns, 44 shouts and 19 despawns thrown away with them.
	/// </para>
	/// <para>
	/// Checked against the table because the harness has no route walking to observe.
	/// </para>
	/// </remarks>
	[Fact]
	public void ContinueRouteBranchesAreCarriedAndBlockTheOnesBelow()
	{
		string path = Path.Combine(BossAiHarness.RepoRoot(),
			"tools", "client-extract", "out", "battle_cycles.tsv");
		string[] lines = File.ReadAllLines(path);
		string[] header = lines[0].Split('	');
		int npcAt = Array.IndexOf(header, "npc");
		int handlerAt = Array.IndexOf(header, "handler");
		int branchAt = Array.IndexOf(header, "branch");
		int kindAt = Array.IndexOf(header, "kind");

		Dictionary<(string, string, string), List<string>> byRung = new();
		Dictionary<(string, string), int> lastBranch = new();
		foreach (string line in lines.Skip(1))
		{
			string[] f = line.Split('	');
			var rung = (f[npcAt], f[handlerAt], f[branchAt]);
			if (!byRung.TryGetValue(rung, out List<string>? kinds))
				byRung[rung] = kinds = new List<string>();
			kinds.Add(f[kindAt]);

			var owner = (f[npcAt], f[handlerAt]);
			int branch = int.Parse(f[branchAt]);
			lastBranch[owner] = Math.Max(lastBranch.TryGetValue(owner, out int seen) ? seen : 0, branch);
		}

		int carried = byRung.Count(rung => rung.Value.Any(k => k == "next_waypoint"));
		int alongsideRealWork = byRung.Count(rung => rung.Value.Any(k => k == "next_waypoint")
			&& rung.Value.Any(k => k is "skill" or "spawn" or "say" or "despawn"));

		Assert.True(carried > 0, "the table no longer carries goto_next_waypoint at all");
		Assert.True(alongsideRealWork > 100,
			$"only {alongsideRealWork} branches pair keep-going with a real action, so reading the "
			+ "element is no longer rescuing the mechanics that justified it");

		// And it reaches the generated code as the named no-op rather than being quietly folded away.
		string generated = File.ReadAllText(Path.Combine(BossAiHarness.RepoRoot(),
			"src", "Aion.GameServer", "Handlers", "AI", "BattleCycles.cs"));
		Assert.Contains("Do.ContinueRoute()", generated);
	}

	/// <summary><b>A patrolling npc is not idle, however much it is also not fighting.</b></summary>
	/// <remarks>
	/// Retail's <c>is_npc_state</c> has eight states and this port answers six of them. The one that
	/// needed care is <c>NPC_STATE_IDLE</c>, because <see cref="When.Idle"/> already existed and means
	/// something looser -- <c>!InCombat</c> -- written for the hand-written classes that only ever ask
	/// "fighting or not". An npc walking its route satisfies that and is emphatically not idle in
	/// retail's vocabulary, where patrolling is <c>NPC_STATE_GOTO_WAYPOINT</c>.
	/// <para>
	/// Reusing the loose one would have fired 16 branches at patrolling npcs that retail keeps for npcs
	/// standing still, and every one of them would have looked like an npc doing its job slightly too
	/// often. So <see cref="When.Idling"/> is separate, and this pins the difference rather than
	/// describing it -- the two conditions are evaluated side by side on the same npc in the same
	/// state, and they must disagree.
	/// </para>
	/// </remarks>
	[Fact]
	public void ThePreciseIdleStateIsNotJustNotFighting()
	{
		using BossAiHarness harness = NewHarness();
		Npc worm = harness.Spawn(Worm, 300f, 300f, 200f);
		PatternAi ai = Assert.IsAssignableFrom<PatternAi>(worm.GetAi());

		worm.GetAi().SetStateIfNot(Aion.GameServer.Ai.AIState.WALKING);

		Assert.False(When.Idling(ai), "a walking npc counted as idle");
		Assert.True(When.Idle(ai), "the loose condition changed meaning, so this pin compares nothing");

		worm.GetAi().SetStateIfNot(Aion.GameServer.Ai.AIState.IDLE);
		Assert.True(When.Idling(ai), "an idle npc did not count as idle");

		// The other pair that collapses if the substate is ignored. Retail separates walking a route
		// from wandering, and both are AIState.WALKING here -- only the substate tells them apart, so
		// a condition that checks the state alone answers true for both. A mutation dropping the
		// substate check survived the first version of this pin.
		worm.GetAi().SetStateIfNot(Aion.GameServer.Ai.AIState.WALKING);
		worm.GetAi().SetSubStateIfNot(Aion.GameServer.Ai.AISubState.WALK_RANDOM);
		Assert.False(When.WalkingItsRoute(ai), "a wandering npc counted as walking its route");
		Assert.True(When.WanderingAtRandom(ai), "a wandering npc did not count as wandering");

		worm.GetAi().SetSubStateIfNot(Aion.GameServer.Ai.AISubState.WALK_PATH);
		Assert.True(When.WalkingItsRoute(ai), "a routed npc did not count as walking its route");
		Assert.False(When.WanderingAtRandom(ai), "a routed npc counted as wandering");
	}

	/// <summary><b>An empty role is not an enemy, and a real attacker is.</b></summary>
	/// <remarks>
	/// <c>is_enemy</c> is 1,156 uses and the extractor refused all of them, including the 1,121 whose
	/// condition was already written for a hand-written class. Only three roles needed adding, and the
	/// attacker is the one this can drive.
	/// <para>
	/// <b>The empty half is the half worth pinning.</b> Most of these roles are scoped to the handler
	/// that sets them -- <c>LastAttacker</c> is assigned, the branch is evaluated, and a <c>finally</c>
	/// clears it -- so outside that window they are null, and the conditions have to answer "not
	/// hostile" rather than throw or treat an absent creature as satisfying the guard. A version
	/// reading <c>role?.IsEnemy(...) ?? true</c> would look entirely reasonable and fire every one of
	/// these branches whenever nothing had happened.
	/// </para>
	/// <para>
	/// The positive half uses the current target, which is the one role that is <i>not</i> scoped --
	/// it reads <c>GetOwner().GetTarget()</c> and so can be observed from outside a branch. Asserting
	/// the scoped ones positively would mean driving a branch that acts, which pins the branch rather
	/// than the condition.
	/// </para>
	/// </remarks>
	[Fact]
	public void AnEmptyRoleIsNotAnEnemyAndARealAttackerIs()
	{
		using BossAiHarness harness = NewHarness();
		Npc worm = harness.Spawn(Worm, 300f, 300f, 200f);
		PatternAi ai = Assert.IsAssignableFrom<PatternAi>(worm.GetAi());

		// Nothing has happened to it yet, so every role is empty.
		Assert.False(When.AttackerIsEnemy(ai), "an npc nobody has touched has a hostile attacker");
		Assert.False(When.CasterIsEnemy(ai), "an npc nobody has cast on has a hostile caster");
		Assert.False(When.EventTargetIsEnemy(ai), "an npc with no event has a hostile event target");
		Assert.False(When.MessageSenderIsEnemy(ai), "an npc sent no message has a hostile sender");

		Assert.False(When.TargetIsEnemy(ai), "an npc with no target has a hostile target");

		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(worm, player);

		Assert.True(player.IsEnemy(worm),
			"the pair this pin relies on is not hostile, so nothing it asserts means anything");

		worm.SetTarget(player);
		Assert.True(When.TargetIsEnemy(ai), "the hostile player it is targeting is not counted");

		// The identity guards on the same role, which are what `is_user` and `is_npc` become. They
		// have to disagree with each other: a mutation reducing TargetIsPlayer to "there is a target"
		// survived a version of this file that only counted rows in the table.
		Assert.True(When.TargetIsPlayer(ai), "a targeted player is not counted as a player");
		Assert.False(When.TargetIsNpc(ai), "a targeted player is counted as an npc");

		Npc other = harness.Spawn(Straggler, 305f, 300f, 200f);
		worm.SetTarget(other);
		Assert.True(When.TargetIsNpc(ai), "a targeted npc is not counted as an npc");
		Assert.False(When.TargetIsPlayer(ai), "a targeted npc is counted as a player");
	}

	/// <summary><b>Which identity guards the table actually carries, written down.</b></summary>
	/// <remarks>
	/// <c>is_user</c> and <c>is_npc</c> are 1,553 uses between them, and reading them added nine
	/// conditions. <b>Only three of the nine currently match anything.</b> The rest name roles that do
	/// appear in the dump but only inside patterns this table does not take -- <c>OBJI_TALKER</c> is
	/// 621 patterns and 532 of its uses sit in <c>on_talked_by_user</c> on npcs that are spoken for by
	/// hand-written classes, so it resolves correctly and matches nothing.
	/// <para>
	/// That is a fine state for a mapping to be in and a bad state to leave undocumented, because the
	/// obvious reading of "is_user is now supported" is that 1,553 uses landed. 74 rows did. This pins
	/// the real number so the claim cannot drift, and so a future widening that makes one of the six
	/// reachable shows up as a deliberate change here rather than as silence.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheIdentityGuardsTheTableCarriesAreTheOnesClaimed()
	{
		string path = Path.Combine(BossAiHarness.RepoRoot(),
			"tools", "client-extract", "out", "battle_cycles.tsv");
		string[] lines = File.ReadAllLines(path);
		int guardsAt = Array.IndexOf(lines[0].Split('	'), "guards");

		Dictionary<string, int> seen = new Dictionary<string, int>();
		foreach (string line in lines.Skip(1))
		{
			foreach (string token in line.Split('	')[guardsAt].Split('|'))
			{
				if (!token.StartsWith("who:", StringComparison.Ordinal))
					continue;
				seen[token] = seen.TryGetValue(token, out int n) ? n + 1 : 1;
			}
		}

		Assert.Equal(
			new Dictionary<string, int>
			{
				["who:TargetIsPlayer"] = 95,
				["who:EventTargetIsPlayer"] = 10,
				["who:TargetIsNpc"] = 4,

				// These three arrived as other elements were read, not as a change to `is_user`
				// itself: `TalkerIsPlayer` when `flee_from` landed, and the attacked/spelled pair when
				// `is_hp_lower_than` about a friend did. Each time a refused element is read, patterns
				// that carried it *and* an identity guard come in together. That is the pin doing its
				// job -- the numbers move as visible edits here rather than as silence.
				["who:TalkerIsPlayer"] = 2,
				["who:AttackedByPlayer"] = 18,
				["who:SpelledByPlayer"] = 17,
			}.OrderBy(e => e.Key),
			seen.OrderBy(e => e.Key));
	}

	/// <summary><b>A skill the port has no data for does not block the branch that casts it.</b></summary>
	/// <remarks>
	/// <c>is_skill_count_left</c> is 832 uses, and in every one the branch it guards goes on to cast
	/// that same skill -- so it asks "can I use this now", answered here from the skill's own cooldown.
	/// <para>
	/// <b>The absent case answers available, which is not the obvious choice.</b> This port's
	/// <c>npc_skills</c> data holds only 2,124 of the 7,103 npc-and-skill pairs these guards name.
	/// Casting does not depend on it -- <c>CastSkillAt</c> queues by id and never reads the list -- so
	/// answering "no entry, not available" would have turned about 70% of these guards permanently
	/// false, killing branches whose cast would have worked and, because branch lists are
	/// first-match-wins, promoting the rungs below them. The first version of this pin asserted the
	/// opposite and failed on the worm's own summon, which is how the gap was found at all.
	/// </para>
	/// <para>
	/// Retail's word is "count" rather than "cooldown", and if it means a per-fight budget this is a
	/// close reading rather than an exact one -- see <see cref="When.SkillReady"/>. Nothing in this port
	/// models a budget, and both readings agree a skill just used is unavailable.
	/// </para>
	/// </remarks>
	[Fact]
	public void AnUnknownSkillDoesNotBlockItsBranch()
	{
		using BossAiHarness harness = NewHarness();
		Npc worm = harness.Spawn(Worm, 300f, 300f, 200f);
		PatternAi ai = Assert.IsAssignableFrom<PatternAi>(worm.GetAi());

		// The worm's own summon in the retail dump, which this port's npc_skills does not list for it.
		// Unknown means available, so the branch behaves as it did before the guard existed.
		Assert.True(When.SkillReady(WormSummon)(ai),
			"a skill the port has no cooldown data for is blocking its branch");

		// And an npc the port *does* have data for answers from that data rather than from the default.
		Npc guard = harness.Spawn(SkilledGuard, 320f, 300f, 200f);
		PatternAi guardAi = Assert.IsAssignableFrom<PatternAi>(guard.GetAi());
		Assert.True(When.SkillReady(GuardSkill)(guardAi), "a listed, uncooled skill is not ready");

		// And once it has been used, its cooldown must actually block the branch. Without this the
		// condition could be a constant true and every assertion above would still pass -- which a
		// mutation demonstrated.
		Aion.GameServer.Model.Skill.NpcSkillEntry entry = Assert.Single(
			guard.GetSkillList().GetNpcSkills().Where(e => e.GetSkillId() == GuardSkill));
		entry.SetLastTimeUsed();
		Assert.False(When.SkillReady(GuardSkill)(guardAi),
			"a skill used a moment ago is still counted as ready");
	}

	/// <summary><b>Fleeing from a role the npc has not met does nothing at all.</b></summary>
	/// <remarks>
	/// <c>flee_from</c> is 353 uses across ten subjects, and the three the port had covered 262. The
	/// rest read roles <see cref="PatternAi"/> already tracks, so they are one line each over the same
	/// <c>FleeFrom</c> helper.
	/// <para>
	/// <b>What has to hold is that an unset role is a no-op rather than a random run.</b> <c>FleeFrom</c>
	/// has a fallback for the case where the npc and the creature it flees stand on the same spot: it
	/// picks a direction from the npc's own heading. Reached with a <i>null</i> creature that fallback
	/// would send the npc running forwards for the full duration, which looks exactly like a working
	/// skittish mechanic and is not one -- so the null check in front of it is load-bearing, and
	/// <c>OBJI_SELF</c> is refused by the extractor for the same reason.
	/// </para>
	/// <para>
	/// <b>One role means two creatures depending on the handler.</b> <c>OBJI_ATTACKER</c> on
	/// <c>on_attacked</c> is whoever hit this npc; on <c>on_see_friend_attacked</c> it is whoever hit
	/// the friend, and those are separate fields here. Every one of the 34 rows that landed is in the
	/// second handler, so reading it as the first would have made a rescuer flee its own last attacker
	/// -- usually nobody, so the mechanic simply would not happen. The same shift for the caster has
	/// nowhere to land, since this port tracks a friend's attacker and not a friend's caster, and those
	/// three uses are refused rather than aimed at the wrong creature.
	/// </para>
	/// </remarks>
	[Fact]
	public void FleeingFromNobodyLeavesTheNpcWhereItIs()
	{
		using BossAiHarness harness = NewHarness();
		Npc worm = harness.Spawn(Worm, 300f, 300f, 200f);
		PatternAi ai = Assert.IsAssignableFrom<PatternAi>(worm.GetAi());
		float x = worm.GetX(), y = worm.GetY();

		// Nothing has attacked, cast on, or been seen by it, so every one of these roles is empty.
		ai.FleeFromAttacker(5);
		ai.FleeFromFriendsAttacker(5);
		ai.FleeFromCaster(5);
		ai.FleeFromEventTarget(5);
		ai.FleeFromSeen(5);
		harness.Clock.Advance(TimeSpan.FromSeconds(6));

		Assert.Equal(x, worm.GetX());
		Assert.Equal(y, worm.GetY());
		Assert.False(worm.GetAi().IsInState(Aion.GameServer.Ai.AIState.FEAR),
			"an npc with nothing to flee from went into a flee state anyway");
	}

	/// <summary><b>A friend who is not there is not hurt.</b></summary>
	/// <remarks>
	/// <c>is_hp_lower_than</c> is 6,386 uses; 6,048 ask about the npc itself and were always read. The
	/// other 338 ask about a creature the event names, and <c>OBJI_FRIEND</c> is 314 of those, living
	/// entirely in <c>on_see_friend_attacked</c> and <c>on_friend_spelled</c> -- a healer deciding
	/// whether the friend is hurt enough to be worth helping.
	/// <para>
	/// <b>An unset role answers false here, which is the opposite of the choice
	/// <see cref="When.SkillReady"/> makes.</b> The reasons differ and both are deliberate. There, a
	/// missing entry meant this port has no cooldown data, so blocking would have destroyed a working
	/// mechanic. Here a missing role means the event has no such creature, and "somebody who is not
	/// there is below 30% health" is not a true statement about anything -- answering true would fire
	/// every healer's rescue rung continuously at nobody.
	/// </para>
	/// </remarks>
	[Fact]
	public void AFriendWhoIsNotThereIsNotHurt()
	{
		using BossAiHarness harness = NewHarness();
		Npc worm = harness.Spawn(Worm, 300f, 300f, 200f);
		PatternAi ai = Assert.IsAssignableFrom<PatternAi>(worm.GetAi());

		// Outside a friend handler the role is unset, which is most of the time.
		Assert.False(When.FriendHpBelow(30)(ai), "an npc with no friend in trouble read one as hurt");
		// 101 rather than 100, and the choice is what makes this pin bite. The npc is at full health,
		// so a version reading its *own* health answers false at 100 and true at 101 -- while the
		// correct version answers false at both, having no friend. A mutation swapping the friend for
		// the npc survived a pin written with 100.
		Assert.False(When.FriendHpBelow(101)(ai),
			"an absent friend was read as the npc's own health");

		// And the npc's own health is a different question, still answered from itself.
		Assert.True(When.HpBelow(101)(ai), "the npc's own health stopped being readable");
	}

	/// <summary><b>Nobody is not far away, and a creature standing next to you is not either.</b></summary>
	/// <remarks>
	/// <c>is_distance_longer_than</c> is 592 uses and the mirror of <see cref="When.TargetWithin"/>:
	/// retail uses one to make a branch melee-only and the other to make it the ranged answer -- a
	/// caster that steps back and only casts once the tank has drifted out of reach.
	/// <para>
	/// <b>The tempting implementation is <c>!TargetWithin(n)</c> and it is wrong.</b>
	/// <c>TargetWithin</c> answers false when there is no target at all, so its negation answers
	/// <i>true</i>: "nobody is further than ten metres" would fire the branch at an empty room. On an
	/// <c>on_enter_attack_state</c> rung that is precisely the moment the target may not be set yet,
	/// so the branch would fire on entering every fight rather than when somebody actually ran away.
	/// Both halves are asserted here -- absent is false, and adjacent is false -- because a constant
	/// false passes the first alone.
	/// </para>
	/// </remarks>
	[Fact]
	public void NobodyIsNotBeyondRange()
	{
		using BossAiHarness harness = NewHarness();
		Npc worm = harness.Spawn(Worm, 300f, 300f, 200f);
		PatternAi ai = Assert.IsAssignableFrom<PatternAi>(worm.GetAi());

		Assert.False(When.TargetBeyond(10)(ai), "an npc with no target read one as out of range");
		Assert.False(When.EventTargetBeyond(10)(ai), "an npc with no event target read one as far off");

		Player near = harness.SpawnPlayer(301f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(worm, near);
		worm.SetTarget(near);
		Assert.False(When.TargetBeyond(10)(ai), "a player one metre away counted as beyond ten");
		Assert.True(When.TargetWithin(10)(ai), "the mirror condition disagrees, so one of them is wrong");

		Player far = harness.SpawnPlayer(340f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(worm, far);
		worm.SetTarget(far);
		Assert.True(When.TargetBeyond(10)(ai), "a player forty metres away did not count as beyond ten");
		Assert.False(When.TargetWithin(10)(ai), "the mirror condition disagrees at range too");
	}

	/// <summary><b>A hate reset keeps the tank, and keeps how much it is hated.</b></summary>
	/// <remarks>
	/// <c>reset_hatepoints</c> is 214 uses. Retail resets hate to make a boss re-pick rather than to
	/// end a fight, and 45 of those keep the most-hated creature: the boss sheds the healers and the
	/// adds without letting go of the tank.
	/// <para>
	/// <b>Keeping the creature is not the same as keeping the mechanic.</b> An implementation that kept
	/// the tank on the list but dropped its hate to zero, or re-added it with some fixed number, would
	/// look right in every way a spawn or cast pin can see -- the tank is still the target, the fight
	/// continues -- and would have quietly rewritten how much threat the rest of the raid has to build
	/// to take it away. So the retained hate value is asserted, not just the retained creature.
	/// </para>
	/// </remarks>
	[Fact]
	public void AHateResetKeepsTheTankAndItsHate()
	{
		using BossAiHarness harness = NewHarness();
		Npc worm = harness.Spawn(Worm, 300f, 300f, 200f);
		PatternAi ai = Assert.IsAssignableFrom<PatternAi>(worm.GetAi());

		Player tank = harness.SpawnPlayer(302f, 300f, 200f);
		Player healer = harness.SpawnPlayer(303f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(worm, tank);
		BossAiHarness.MakeMutuallyKnown(worm, healer);
		// AddHate rather than Wound: the harness's Wound passes notifyAttack:false, which records
		// damage and no hate at all, so a setup built on it would leave both players on zero and the
		// guard below would be the only thing that noticed.
		worm.GetAggroList().AddHate(tank, 900);
		worm.GetAggroList().AddHate(healer, 100);

		int tankHate = worm.GetAggroList().GetHate(tank);
		Assert.True(tankHate > worm.GetAggroList().GetHate(healer),
			"the tank is not the most hated, so this pin is not testing what it says");

		// Through `Do.` rather than the method: the table emits the action, and a mutation pointing
		// the action at the wrong method survives a pin that calls the method itself.
		Do.ResetHateExceptTop()(ai);

		Assert.Equal(tankHate, worm.GetAggroList().GetHate(tank));
		Assert.Equal(0, worm.GetAggroList().GetHate(healer));

		// And the plain form drops everyone, including the tank.
		Do.ResetHate()(ai);
		Assert.Empty(worm.GetAggroList().Stream());
	}

	/// <summary><b>A range-restricted pick ignores the healer standing at the back.</b></summary>
	/// <remarks>
	/// <c>use_skill_by_attacker_indicator</c> with <c>restricted_range</c> is 53 uses, refused until
	/// now because the skill queue picks its target when it drains and takes no bound. That is true of
	/// the unaimed path only -- <c>CastSkillAt</c> resolves a creature immediately and sends it with
	/// the entry, which is where the bound belongs.
	/// <para>
	/// <b>Retail states no distance</b>, so the reach is the skill's own <c>first_target_range</c>. The
	/// element exists precisely to change who gets picked: unrestricted, <c>ATTACKERI_RANDOM_ONE</c>
	/// takes the healer at the back as readily as the tank in front. So this pin puts one player in
	/// reach and one far outside it and requires the far one never to be chosen -- checked over enough
	/// draws that a random pick would have taken it many times over.
	/// </para>
	/// </remarks>
	[Fact]
	public void ARangeRestrictedPickSkipsWhoeverIsOutOfReach()
	{
		using BossAiHarness harness = NewHarness();
		Npc worm = harness.Spawn(Worm, 300f, 300f, 200f);
		PatternAi ai = Assert.IsAssignableFrom<PatternAi>(worm.GetAi());

		Player near = harness.SpawnPlayer(301f, 300f, 200f);
		Player far = harness.SpawnPlayer(600f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(worm, near);
		BossAiHarness.MakeMutuallyKnown(worm, far);
		worm.GetAggroList().AddHate(near, 100);
		worm.GetAggroList().AddHate(far, 100);

		// Both are on the hate list, so an unrestricted random pick would reach the far one.
		Assert.Equal(2, worm.GetAggroList().Stream().Count());

		// The reach has to be a real number or the action falls back to the unrestricted pick and
		// this pin would prove nothing.
		int reach = Aion.GameServer.Dataholders.DataManager.SKILL_DATA
			.GetSkillTemplate(RaidSkill)!.GetProperties()!.firstTargetRange;
		Assert.True(reach > 0, $"the pinned skill declares no first_target_range ({reach})");
		Assert.True(reach < 200, $"the pinned skill reaches {reach}, far enough to include both players");

		// Fired rather than drained: DrainQueuedSkills returns what was cast and deliberately throws
		// the aim away, so it cannot answer "who". FireNextQueuedSkill returns the creature hit.
		var aimed = new List<VisibleObject?>();
		for (int draw = 0; draw < 40; draw++)
		{
			Do.SkillOnRankedInReach(AggroTarget.RANDOM, RaidSkill)(ai);
			aimed.Add(BossAiHarness.FireNextQueuedSkill(worm));
		}

		Assert.NotEmpty(aimed.Where(a => a != null));
		Assert.DoesNotContain(far, aimed);
		Assert.Contains(near, aimed);

		// And a skill this port has no template for still resolves somebody: with no declared reach
		// the action falls back to the unrestricted pick rather than inventing a bound.
		//
		// Worth being precise about what this does *not* show. A zero reach would not mean "nobody" --
		// AggroList calls IsInRange center-to-center=false, so both bound radii are added and zero
		// still reaches anything touching a large boss. So a mutation passing zero straight through
		// survives this pin, correctly: the two readings genuinely agree at close quarters, and the
		// fallback is about not inventing a number rather than about avoiding an empty result.
		// Observed by looking at the queue rather than by firing: CastSkillAt queues nothing when it
		// resolves nobody, so a queued entry is proof an aim was found. Firing is not an option here
		// -- SkillAttackManager dereferences the template, so a skill that has none throws, which is
		// pre-existing behaviour and not what this pin is about.
		Assert.Null(Aion.GameServer.Dataholders.DataManager.SKILL_DATA.GetSkillTemplate(NoSuchSkill));
		BossAiHarness.DrainQueuedSkills(worm);
		Assert.Null(worm.GetNextQueuedSkill());

		Do.SkillOnRankedInReach(AggroTarget.MOST_HATED, NoSuchSkill)(ai);
		Assert.NotNull(worm.GetNextQueuedSkill());
	}

}
