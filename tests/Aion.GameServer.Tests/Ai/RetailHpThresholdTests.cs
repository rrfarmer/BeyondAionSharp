using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for the bosses whose <c>HpPhases</c> thresholds were corrected against retail
/// patterns (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// A threshold renumbering compiles cleanly and no other test can see it, so without these the only
/// evidence that e.g. Calindi now runs his event four times rather than three is that someone read the
/// diff. Each test walks HP down a point at a time — the phase check runs on every attack, as in the real
/// fight — and observes the phase through whatever the handler actually does: an add appearing, or a skill
/// landing in the queue.
/// <para>
/// Each boss is spawned in <b>its own map</b>, not a default one. <c>HpPhases.TryEnterNextPhase</c> returns
/// silently unless the owner <c>IsSpawned()</c>, and a boss whose <c>HandleSpawned</c> places objects at
/// hardcoded world coordinates — Padmarashka's four shield NPCs, for one — fails to finish spawning in a map
/// those coordinates do not belong to. The phases then never fire and the test fails with no clue why.
/// </para>
/// <para>
/// <c>SetCurrentHpPercent</c> truncates, so an observed percentage is always read back from the boss rather
/// than assumed from the value that was set.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class RetailHpThresholdTests
{
	private const int Gelkmaros = 220070000;
	private const int GelkmarosPadmarashka = 216580;
	private const int PadmarashkaRockSlide = 281936;

	private const int DragonLordsRefuge = 300520000;
	private const int CalindiFlamelord = 219359;
	private const int CalindiFinisher = 20942;
	private const int HallucinatoryVictory = 20911;

	private const int NightmareCircus = 301200000;
	private const int NightmareLordHeiramune = 233467;
	private const int HeiramuneAdd = 233162;

	private const int RentusBase = 300280000;
	private const int Vasharti = 217313;
	private const int VashartiPhaseSkill = 20532;

	private const int AzoturanFortress = 310100000;
	private const int IcaronixTheDeceiver = 214598;
	private const int IcaronixTheBetrayer = 214599;

	/// <summary>Drives HP down a point at a time, reporting the HP at which <paramref name="entered"/> trips.</summary>
	private static List<int> PhasesEnteredWhile(Npc boss, Player player, Func<int> observe, int from = 100, int to = 3)
	{
		var at = new List<int>();
		int seen = observe();
		for (int hp = from; hp >= to; hp--)
		{
			BossAiHarness.SetHpPercent(boss, hp);
			boss.SetTarget(player);
			boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
			int now = observe();
			if (now > seen)
				at.Add(boss.GetLifeStats().GetHpPercentage());
			seen = now;
		}
		return at;
	}

	[Fact]
	public void GelkmarosPadmarashkaDropsHerRocksAtTenPercent()
	{
		using var harness = BossAiHarness.For(Gelkmaros).WithWorldSize(4096).WithAi(typeof(GelkmarosPadmarashkaAI), typeof(AggressiveNpcAI), typeof(RockSlideAI)).Build();
		// Her own shield NPCs sit around 2906..2963 / 859..878, so spawn her where she actually stands.
		Npc boss = harness.Spawn(GelkmarosPadmarashka, 2940.20f, 851.29f, 35.89f);
		Player player = harness.SpawnPlayer(2945f, 855f, 35.89f);
		harness.Engage(boss, player);

		var at = PhasesEnteredWhile(boss, player,
			() => harness.LiveNpcs().Count(n => n.GetNpcId() == PadmarashkaRockSlide));

		// Retail pattern DF4_Dramata puts the rock slide at 10%, not the 33% we had.
		Assert.Equal([10], at);
	}

	[Fact]
	public void CalindiRunsHisEventFourTimesThenFinishesAtFifteen()
	{
		using var harness = BossAiHarness.For(DragonLordsRefuge).WithAi(typeof(CalindiFlamelordAI)).Build();
		Npc boss = harness.Spawn(CalindiFlamelord);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);
		BossAiHarness.DrainQueuedSkills(boss);

		var finisherAt = new List<int>();
		var eventAt = new List<int>();
		for (int hp = 100; hp >= 3; hp--)
		{
			BossAiHarness.SetHpPercent(boss, hp);
			boss.SetTarget(player);
			boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
			int observed = boss.GetLifeStats().GetHpPercentage();
			foreach (var cast in BossAiHarness.DrainQueuedSkills(boss))
			{
				if (cast.SkillId == HallucinatoryVictory)
					eventAt.Add(observed);
				else if (cast.SkillId == CalindiFinisher)
					finisherAt.Add(observed);
			}
		}

		// Retail pattern IDTiamat_Kalrindy: four repeats of the hallucinatory event, then a
		// different finisher at 15. We previously ran three repeats and finished at 12.
		Assert.Equal([80, 60, 40, 25], eventAt);
		Assert.Equal([15], finisherAt);
	}

	[Fact]
	public void HeiramuneSpawnsHisAddAtFiftyFive()
	{
		using var harness = BossAiHarness.For(NightmareCircus).WithAi(typeof(NightmareLordHeiramuneAI), typeof(NightmareLordHeiramuneCloneAI)).Build();
		Npc boss = harness.Spawn(NightmareLordHeiramune);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		var at = PhasesEnteredWhile(boss, player,
			() => harness.LiveNpcs().Count(n => n.GetNpcId() == HeiramuneAdd));

		// Retail pattern IDAsteria_IU_world_3Stage_Boss spawns at 55, not the 50 we had.
		Assert.Equal([55], at);
	}

	[Fact]
	public void IcaronixSwapsFormAtSeventyFive()
	{
		using var harness = BossAiHarness.For(AzoturanFortress).WithAi(typeof(BetrayerIcaronixAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(IcaronixTheDeceiver, 461.07f, 439.876f, 993.046f);
		Player player = harness.SpawnPlayer(461.07f, 439.876f, 993.046f);
		harness.Engage(boss, player);

		var at = PhasesEnteredWhile(boss, player,
			() => harness.LiveNpcs().Count(n => n.GetNpcId() == IcaronixTheBetrayer));

		// Retail pattern ND2_AhC_1 swaps at 75, not the 50 we had.
		Assert.Equal([75], at);
	}

	[Fact]
	public void VashartiStepsAtEightySixFiftySixAndTwentySix()
	{
		using var harness = BossAiHarness.For(RentusBase)
			.WithAi(typeof(BrigadeGeneralVashartiAI), typeof(AggressiveNpcAI))
			.Build();
		Npc boss = harness.Spawn(Vasharti);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);
		BossAiHarness.DrainQueuedSkills(boss);

		// Every step queues the same skill, so the observable is when it arrives rather than which.
		var at = new List<int>();
		for (int hp = 100; hp >= 5; hp--)
		{
			BossAiHarness.SetHpPercent(boss, hp);
			BossAiHarness.Rehate(boss, player);
			int observed = boss.GetLifeStats().GetHpPercentage();
			boss.SetTarget(player);
			boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
			if (BossAiHarness.DrainQueuedSkills(boss).Any(c => c.SkillId == VashartiPhaseSkill))
				at.Add(observed);
		}

		// Retail pattern IDYun_Nmd6 has three steps, not the four at 75/50/25/10 we had. These land on
		// the thresholds exactly; SetCurrentHpPercent's truncation happens to be a no-op at these values.
		Assert.Equal([86, 56, 26], at);
	}
}
