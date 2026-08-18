using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="PriestZitanAI"/>, translated from retail pattern
/// <c>IDTP_Fanatic_Boss_EL_ve40</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Seven illusions of melancholy across one fight, and <b>where they land is the mechanic</b>: three
/// at his own feet on the pull, then two on his quarry at each of the two crossings. Blows are
/// delivered as bare Attack events rather than through <c>Rehate</c>, which raises one of its own —
/// these branches fire on the first blow past a threshold, so a doubled swing would read as one.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PriestZitanAiTests
{
	private const int Inggison = 210050000;

	private const int Zitan = 216512;
	private const int Illusion = 281524;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Inggison).WithWorldSize(2048)
			// IllusionOfMelancholyAI because 281524 is shared: Zitan calls it and so does Vallakhan, and
			// retail binds the npc to IDTP_Fanatic_Elementalearth2 whoever summoned it. A test must
			// register what the fight can spawn, and what the npc runs is the npc's business.
			.WithAi(typeof(PriestZitanAI), typeof(IllusionOfMelancholyAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>He stands at three hundred; his quarry forty metres off, so placement is readable.</summary>
	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Zitan, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(340f, 300f, 200f);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static void Strike(Npc boss, Player player) =>
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);

	private static List<Npc> Illusions(BossAiHarness harness) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == Illusion).ToList();

	/// <summary><b>Three come with him, and they come to his side.</b></summary>
	[Fact]
	public void ThreeComeWithHimAndStandAtHisFeet()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		List<Npc> wave = Illusions(harness);
		Assert.Equal(3, wave.Count);
		Assert.All(wave, n => Assert.True(Math.Abs(n.GetX() - 300f) < 7f,
			$"{n.GetX():F1} is not beside him"));
	}

	/// <summary>
	/// <b>The first blow under fifty puts two more on the player he is fighting</b> — forty metres
	/// from where the opening three are standing.
	/// </summary>
	[Fact]
	public void TheFirstBlowUnderFiftyPutsTwoOnHisQuarry()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 40);
		Strike(boss, player);

		List<Npc> all = Illusions(harness);
		Assert.Equal(5, all.Count);
		Assert.Equal(2, all.Count(n => Math.Abs(n.GetX() - 340f) < 7f));
	}

	/// <summary><b>And two more on the first blow under twenty-five: seven in all.</b></summary>
	[Fact]
	public void AndTwoMoreUnderTwentyFive()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 40);
		Strike(boss, player);

		BossAiHarness.SetExactPercent(boss, 20);
		Strike(boss, player);

		Assert.Equal(7, Illusions(harness).Count);
	}

	/// <summary>
	/// <b>Each crossing pays once, however many blows land.</b> Retail writes both crossings twice —
	/// under <c>on_attacked</c> and <c>on_spelled</c> — behind one flag var apiece, so the pair is one
	/// payment either way.
	/// </summary>
	[Fact]
	public void EachCrossingPaysOnce()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 40);
		for (int i = 0; i < 20; i++)
			Strike(boss, player);

		Assert.Equal(5, Illusions(harness).Count);
	}

	/// <summary>And above fifty no blow brings anybody.</summary>
	[Fact]
	public void AboveFiftyNoBlowBringsAnybody()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 80);
		for (int i = 0; i < 20; i++)
			Strike(boss, player);

		Assert.Equal(3, Illusions(harness).Count);
	}

	/// <summary>Both of his exits clear every one of them.</summary>
	[Fact]
	public void BothExitsClearThemAll()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Strike(boss, player);
		Strike(boss, player);
		Assert.Equal(7, Illusions(harness).Count);

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Empty(Illusions(harness));
	}
}
