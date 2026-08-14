using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pin for <see cref="MacunbelloAI"/>, the retail-sourced Beshmundir Temple encounter
/// (patterns IDCT_Boss_LichKing / IDCTH_Boss_LichKing; see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// These assertions mirror the pattern's three battle timers, so a regression in the cadence, the HP-band
/// latching or the soul-reaper combo fails here instead of being discovered in-game.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class MacunbelloAiTests
{
	private const int Macunbello = 216245;
	private const int SoulReaper = 281698;

	private const int DevourSoul = 19051;
	private const int Shockwave = 19052;
	private const int TideOfDarkness = 19053;
	private const int AbsorbEnergyOfDarkness = 19054;
	private const int ShieldBuff = 19049;
	private const int CurseOfSouls = 19050;

	private static BossAiHarness NewHarness() => BossAiHarness.For()
		.WithAi(typeof(MacunbelloAI), typeof(MacunbelloSoulReaperAI))
		.Build();

	[Fact]
	public void ShieldsItselfOnSpawnAtTheLevelItsNpcSkillsEntryDefines()
	{
		using var harness = NewHarness();

		Npc boss = harness.Spawn(Macunbello);

		BossAiHarness.QueuedCast shield = Assert.Single(BossAiHarness.DrainQueuedSkills(boss));
		Assert.Equal(ShieldBuff, shield.SkillId);
		Assert.Equal(NpcSkillTargetAttribute.ME, shield.Target);
		// npc_skills.xml gives 216245 the shield at lv 1 — the AI must take the level from the data.
		Assert.Equal(1, shield.Level);
	}

	[Fact]
	public void OpensWithTideOfDarknessThenBeatsShockwaveEveryTenSeconds()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(Macunbello);
		Player player = harness.SpawnPlayer(x: 985f);
		BossAiHarness.DrainQueuedSkills(boss);

		harness.Engage(boss, player);

		BossAiHarness.QueuedCast opener = Assert.Single(BossAiHarness.DrainQueuedSkills(boss));
		Assert.Equal(TideOfDarkness, opener.SkillId);
		Assert.Equal(NpcSkillTargetAttribute.MOST_HATED, opener.Target);

		// 30 seconds of fight at full HP: three Shockwave beats and three self-cast Tides, no band crossings.
		harness.Clock.Advance(TimeSpan.FromSeconds(30));

		var casts = BossAiHarness.DrainQueuedSkills(boss);
		Assert.Equal(3, casts.Count(c => c.SkillId == Shockwave));
		Assert.All(casts.Where(c => c.SkillId == Shockwave), c =>
		{
			Assert.Equal(NpcSkillTargetAttribute.MOST_HATED, c.Target);
			// npc_skills.xml gives 216245 Shockwave at lv 20; the AI must take the level from the data, not hardcode 1.
			Assert.Equal(20, c.Level);
		});
		Assert.Equal(3, casts.Count(c => c.SkillId == TideOfDarkness && c.Target == NpcSkillTargetAttribute.ME));
		Assert.DoesNotContain(casts, c => c.SkillId == AbsorbEnergyOfDarkness);
	}

	/// <summary>
	/// The bands are 91/71/51/31/11. Retail latches each with its own flag, so an HP drop past several at once
	/// still yields one crossing per 10s phase tick, in order, and never a repeat.
	/// </summary>
	[Theory]
	[InlineData(95, 0)]
	[InlineData(90, 1)]
	[InlineData(70, 2)]
	[InlineData(50, 3)]
	[InlineData(10, 5)]
	public void CrossesEachHpBandExactlyOnceAtOnePerPhaseTick(int hpPercent, int expectedBands)
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(Macunbello);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);
		BossAiHarness.DrainQueuedSkills(boss);

		BossAiHarness.SetHpPercent(boss, hpPercent);

		var ticksThatCrossedABand = new List<int>();
		for (int tick = 1; tick <= 12; tick++)
		{
			harness.Clock.Advance(TimeSpan.FromSeconds(10));
			var casts = BossAiHarness.DrainQueuedSkills(boss);
			var absorbs = casts.Where(c => c.SkillId == AbsorbEnergyOfDarkness).ToList();
			Assert.InRange(absorbs.Count, 0, 1);
			if (absorbs.Count == 1)
			{
				Assert.Equal(NpcSkillTargetAttribute.RANDOM, absorbs[0].Target);
				ticksThatCrossedABand.Add(tick);
				// A band crossing replaces that tick's self-cast rather than joining it.
				Assert.DoesNotContain(casts, c => c.SkillId == TideOfDarkness && c.Target == NpcSkillTargetAttribute.ME);
			}
		}

		// Every pending band is consumed by the earliest ticks, then the fight settles into the self-cast.
		Assert.Equal(Enumerable.Range(1, expectedBands), ticksThatCrossedABand);
	}

	[Fact]
	public void AddsTwoSoulReapersAboveHalfHpAndFourBelow()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(Macunbello);
		Player player = harness.SpawnPlayer(x: 985f);
		harness.Engage(boss, player);

		int before = harness.LiveNpcs().Count(n => n.GetNpcId() == SoulReaper);
		harness.Clock.Advance(TimeSpan.FromSeconds(20));
		Assert.Equal(before + 2, harness.LiveNpcs().Count(n => n.GetNpcId() == SoulReaper));

		BossAiHarness.SetHpPercent(boss, 40);
		harness.Clock.Advance(TimeSpan.FromSeconds(30));
		Assert.Equal(before + 6, harness.LiveNpcs().Count(n => n.GetNpcId() == SoulReaper));
	}

	[Fact]
	public void DevoursExactlyThePlayerASoulReaperReportsCursing()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(Macunbello);
		Npc reaper = harness.Spawn(SoulReaper, x: 982f);
		Player victim = harness.SpawnPlayer(x: 985f);
		BossAiHarness.MakeMutuallyKnown(boss, reaper);
		harness.Engage(boss, victim);
		harness.Engage(reaper, victim);
		reaper.GetAggroList().AddHate(victim, 1000);
		BossAiHarness.DrainQueuedSkills(boss);
		BossAiHarness.DrainQueuedSkills(reaper);

		// The reaper's first curse lands 5s after it engages.
		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Assert.Contains(BossAiHarness.DrainQueuedSkills(reaper), c => c.SkillId == CurseOfSouls);
		var bossCasts = BossAiHarness.DrainQueuedSkills(boss);
		Assert.Contains(bossCasts, c => c.SkillId == DevourSoul);
		Assert.Same(victim, boss.GetTarget());
	}

	[Fact]
	public void StopsEveryBattleTimerWhenItDies()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(Macunbello);
		Player player = harness.SpawnPlayer(x: 985f);
		harness.Engage(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(10));
		Assert.True(harness.Clock.ArmedTimerCount > 0);

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);
		BossAiHarness.DrainQueuedSkills(boss);

		harness.Clock.Advance(TimeSpan.FromMinutes(2));
		Assert.Empty(BossAiHarness.DrainQueuedSkills(boss));
	}
}
