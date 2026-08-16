using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="HighPriestYatriAI"/>, translated from retail pattern <c>Naga_PhA</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The sender <see cref="ExedilGhostAI"/> shipped without. He is <see cref="ExedilAI"/>'s architecture
/// with none of his numbers, and the difference that matters is <em>where</em> the waves land: yatri's
/// first two are <c>spawn_on_target</c> and only his deepest comes home to him.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class HighPriestYatriAiTests
{
	private const int Brusthonin = 220040000;

	private const int Yatri = 212308;
	private const int YatriTwin = 280768;

	private const int PowerOfYatri = 280769;
	private const int TruePowerOfYatri = 280819;

	/// <summary>A plain <c>aggressive</c> NPC with no class of its own; it only has to be the thing he is fighting.</summary>
	private const int Quarry = 202541;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Brusthonin).WithWorldSize(2048)
			.WithAi(typeof(HighPriestYatriAI), typeof(ExedilGhostAI), typeof(AggressiveNpcAI),
				typeof(ServantNpcAI))
			.Build();

	/// <summary>
	/// <b>His quarry is an NPC here, not the harness's stand-in player.</b> Exedil's pins keep the
	/// player sixty metres back so his ghosts' casts never reach it — that trick does not work on a
	/// boss whose waves are <c>spawn_on_target</c>, because the summons land <em>on</em> the target
	/// whatever the distance, and a <c>servant</c> cast into the stand-in takes the effect engine down.
	/// Standing a plain NPC in as the thing he is fighting keeps the placement observable and keeps the
	/// casts off the player.
	/// </summary>
	/// <param name="quarryX">
	/// Where the thing he is fighting stands. Sixty metres out by default, so a wave placed on it
	/// cannot be mistaken for one at his feet — but that is <b>further than his own fifty-metre
	/// broadcast</b>, so any pin about 3319 has to stand the quarry where a real fight would.
	/// </param>
	private static (BossAiHarness, Npc, Npc) Engaged(int npcId = Yatri, float quarryX = 360f)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(npcId, 300f, 300f, 200f);
		Npc quarry = harness.Spawn(Quarry, quarryX, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, quarry);
		harness.Engage(boss, quarry);
		return (harness, boss, quarry);
	}

	private static void Advance(BossAiHarness harness, Npc boss, Npc quarry, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, quarry);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>At full health his rungs are all out of reach, so he calls nobody.</summary>
	[Fact]
	public void AboveEightyHeCallsNobody()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 90);
		Advance(harness, boss, quarry, 60);

		Assert.Equal(0, Count(harness, PowerOfYatri));
		Assert.Equal(0, Count(harness, TruePowerOfYatri));
	}

	/// <summary>
	/// <b>The fallback rung is what carries the clock while he is above eighty.</b> His 81–100 rung
	/// fires once and is spent; after that nothing but the bottom branch keeps the six-second heartbeat
	/// alive, so removing it means a boss who opened at full health never summons however far he is
	/// taken down.
	/// </summary>
	[Fact]
	public void TheFallbackCarriesTheClockOnceTheOpeningRungIsSpent()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		// t=8 the opening heartbeat lands on the 81-100 rung, which is spent from here on.
		BossAiHarness.SetExactPercent(boss, 90);
		Advance(harness, boss, quarry, 9);

		// t=18 finds no band at all; only the fallback keeps the clock going.
		Advance(harness, boss, quarry, 10);
		BossAiHarness.SetExactPercent(boss, 70);

		Advance(harness, boss, quarry, 6);
		Assert.Equal(2, Count(harness, PowerOfYatri));
	}

	/// <summary>
	/// <b>The 81–100 rung re-arms at ten seconds where the fallback re-arms at six</b>, which is the
	/// whole reason it is in the table at all — its casts are not translated and nothing else about it
	/// differs. Pinned by when the next wave can land rather than by what lands.
	/// </summary>
	[Fact]
	public void TheOpeningRungHoldsTheClockLongerThanTheFallbackWould()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 90);
		Advance(harness, boss, quarry, 9);          // t=8: the 81-100 rung, re-arming at ten.
		BossAiHarness.SetExactPercent(boss, 70);

		Advance(harness, boss, quarry, 6);          // t=15: a six-second re-arm would have fired at 14.
		Assert.Equal(0, Count(harness, PowerOfYatri));

		Advance(harness, boss, quarry, 4);          // t=19, past the ten-second one at 18.
		Assert.Equal(2, Count(harness, PowerOfYatri));
	}

	/// <summary>
	/// <b>His waves land on the raid, not on him.</b> Exedil scatters his ghosts around his own feet;
	/// yatri's first two are <c>spawn_on_target</c>. The player stands sixty metres out, so a wave at
	/// the boss's feet cannot be mistaken for one on its quarry.
	/// </summary>
	[Fact]
	public void TheFirstWaveLandsOnHisQuarry()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, quarry, 10);

		List<Npc> wave = harness.LiveNpcs().Where(n => n.GetNpcId() == PowerOfYatri).ToList();
		Assert.Equal(2, wave.Count);
		foreach (Npc summon in wave)
			Assert.True(Math.Abs(summon.GetX() - quarry.GetX()) < Math.Abs(summon.GetX() - boss.GetX()),
				$"placed at {summon.GetX():F0}, between quarry {quarry.GetX():F0} and boss {boss.GetX():F0}");
	}

	/// <summary>
	/// The hand-over: dropping into 26–55 takes the first pair away and puts two more on his target.
	/// Same NPC, new group, so the count is what shows it.
	/// </summary>
	[Fact]
	public void TheSecondBandReplacesTheFirstWave()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, quarry, 10);
		Assert.Equal(2, Count(harness, PowerOfYatri));

		BossAiHarness.SetExactPercent(boss, 40);
		Advance(harness, boss, quarry, 9);

		// Two, not four: the first pair went as the second arrived.
		Assert.Equal(2, Count(harness, PowerOfYatri));
	}

	/// <summary>
	/// <b>The deep rung comes home to him</b>, eight metres out rather than onto the raid — the one
	/// wave of the three that does.
	/// </summary>
	[Fact]
	public void TheDeepWaveGathersAroundHim()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, boss, quarry, 9);

		List<Npc> deep = harness.LiveNpcs().Where(n => n.GetNpcId() == TruePowerOfYatri).ToList();
		Assert.Equal(2, deep.Count);
		foreach (Npc summon in deep)
			Assert.True(Math.Abs(summon.GetX() - boss.GetX()) <= 8f,
				$"placed at {summon.GetX():F0} against the boss at {boss.GetX():F0}");
	}

	/// <summary>
	/// <b>And it ends the chain.</b> The deep rung arms timer 6 rather than timer 0, so a boss taken
	/// under twenty-five early gets one wave and never another — the same shape as Exedil's and Ulan's.
	/// </summary>
	[Fact]
	public void BelowTwentyFiveHeSummonsOnceAndStops()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, boss, quarry, 9);
		Assert.Equal(2, Count(harness, TruePowerOfYatri));

		Advance(harness, boss, quarry, 90);
		Assert.Equal(2, Count(harness, TruePowerOfYatri));
		Assert.Equal(0, Count(harness, PowerOfYatri));
	}

	/// <summary>
	/// <b>The deep rung kills the clock, not just its own turn.</b> Once it has fired, healing him back
	/// into a summoning band produces nothing — it arms timer 6 and never timer 0, so there is no
	/// heartbeat left to notice. Stated by walking back up, because that is the only way to tell "the
	/// band no longer matches" apart from "the clock is gone".
	/// </summary>
	[Fact]
	public void OnceTheDeepRungHasFiredHealingHimBackChangesNothing()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, boss, quarry, 9);
		Assert.Equal(2, Count(harness, TruePowerOfYatri));

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, quarry, 60);

		Assert.Equal(0, Count(harness, PowerOfYatri));
		Assert.Equal(2, Count(harness, TruePowerOfYatri));
	}

	/// <summary>
	/// <b>Unlike Exedil's, his deep pair expires.</b> Exedil's carries no <c>live_time</c> at all;
	/// yatri's carries the same twenty minutes as everything else he calls.
	/// </summary>
	[Fact]
	public void TheDeepWaveLastsTwentyMinutes()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, boss, quarry, 9);
		Assert.Equal(2, Count(harness, TruePowerOfYatri));

		Advance(harness, boss, quarry, 1180);
		Assert.Equal(2, Count(harness, TruePowerOfYatri));

		Advance(harness, boss, quarry, 30);
		Assert.Equal(0, Count(harness, TruePowerOfYatri));
	}

	/// <summary>
	/// <b>The half that had nobody to hear it.</b> His deep rung broadcasts 3319, and the first waves
	/// still standing shed their form for the deep one — the naga side of the branch
	/// <see cref="ExedilGhostAI"/> shipped with no sender. Driven by skipping the 26–55 band, which is
	/// the only way a first wave is still alive.
	/// </summary>
	/// <remarks>
	/// <b>The quarry stands five metres out here, not sixty.</b> His waves land on whoever he is
	/// fighting and his broadcast reaches fifty metres, so the two interact: a raid that fights him at
	/// range puts its own waves outside the message. That is retail's arithmetic rather than ours, and
	/// it is why this pin cannot share the geometry the placement pins need.
	/// <para>
	/// <b>And the wave has to be made known to him by hand.</b> <c>NpcMessageBus</c> walks the sender's
	/// known list and only falls back to a region scan when that list is <em>empty</em> — which it is
	/// not here, because the harness made the quarry known. In production the world's visibility system
	/// keeps a summon five metres away in the boss's list; the harness runs no visibility, so a pin
	/// about a broadcast to placed summons has to stand that part up itself. Exedil's equivalent pin
	/// needs none of this only because his ghosts land on his own position.
	/// </para>
	/// </remarks>
	[Fact]
	public void SkippingTheMiddleBandUpgradesTheFirstWave()
	{
		var (harness, boss, quarry) = Engaged(quarryX: 305f);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, quarry, 10);
		Assert.Equal(2, Count(harness, PowerOfYatri));

		foreach (Npc summon in harness.LiveNpcs().Where(n => n.GetNpcId() == PowerOfYatri))
			BossAiHarness.MakeMutuallyKnown(boss, summon);

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, boss, quarry, 9);

		Assert.Equal(0, Count(harness, PowerOfYatri));
		// The two the rung calls, plus the two the first wave became.
		Assert.Equal(4, Count(harness, TruePowerOfYatri));
	}

	/// <summary>Dying clears all three groups, as retail's <c>on_killed_by_user</c> does.</summary>
	[Fact]
	public void DyingClearsEveryWave()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, quarry, 10);
		Assert.Equal(2, Count(harness, PowerOfYatri));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Equal(0, Count(harness, PowerOfYatri));
	}

	/// <summary>Both ids retail binds to this pattern run it, not only the one the world places.</summary>
	[Fact]
	public void HisUnusedTwinRunsTheSameFight()
	{
		var (harness, boss, quarry) = Engaged(YatriTwin);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, quarry, 10);

		Assert.Equal(2, Count(harness, PowerOfYatri));
	}
}
