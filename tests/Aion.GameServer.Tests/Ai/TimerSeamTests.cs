using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The two pins the timer seam was built for: <b>questions about a timer rather than about what the
/// timer did.</b>
/// </summary>
/// <remarks>
/// Both stalled for entries on visibility. Kingspin's accelerator windows open once per fight in retail
/// and once per cry without their guard — one arming against three, which no throw count over a
/// realistic watch separates. Masto's bands act only through a random target switch, so a fire is
/// invisible whenever the dice land on the creature already targeted.
/// <para>
/// <c>TimerArmCount</c> and <c>TimerFireCount</c> answer both directly. They live together here because
/// they pin the seam as much as the encounters.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TimerSeamTests
{
	private const int LowerUdasTemple = 300160000;
	private const int Kingspin = 215792;

	private const int Brusthonin = 220050000;
	private const int Masto = 213729;

	/// <summary>Retail's <c>BTIMERI_INDEX_3</c> and <c>_4</c>: the two accelerator windows.</summary>
	private const int DeepWindow = 3;
	private const int MiddleWindow = 4;

	private static PatternAi Pattern(Npc npc) => (PatternAi)npc.GetAi();

	/// <summary>
	/// <b>Four cries open the accelerator windows once.</b> Retail guards Kingspin's message branch with
	/// <c>set_flag_var</c>, so the first web to catch somebody arms timers 3 and 4 and later cries are
	/// ignored.
	/// </summary>
	[Fact]
	public void KingspinsWindowsOpenOncePerFight()
	{
		using BossAiHarness harness = BossAiHarness.For(LowerUdasTemple).WithWorldSize(2048)
			.WithAi(typeof(KingspinAI), typeof(KingspinWebAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

		Npc boss = harness.Spawn(Kingspin, 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, raider);
		harness.Engage(boss, raider);
		BossAiHarness.SetExactPercent(boss, 35);

		for (int i = 0; i < 4; i++)
			((INpcMessageListener)boss.GetAi()).OnNpcMessage(boss, KingspinAI.WebCaught, null);

		Assert.Equal(1, Pattern(boss).TimerArmCount(DeepWindow));
		Assert.Equal(1, Pattern(boss).TimerArmCount(MiddleWindow));
	}

	/// <summary>
	/// <b>And without the guard they would open on every cry.</b> Stated as the mirror of the pin above,
	/// so the arm count is shown to be sensitive to the thing it measures rather than fixed at one.
	/// </summary>
	[Fact]
	public void AndTheArmCountTracksTheCallsItIsMeantTo()
	{
		using BossAiHarness harness = BossAiHarness.For(LowerUdasTemple).WithWorldSize(2048)
			.WithAi(typeof(KingspinAI), typeof(KingspinWebAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

		Npc boss = harness.Spawn(Kingspin, 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, raider);
		harness.Engage(boss, raider);

		// His own heartbeat is armed on entering the fight and re-armed on every tick, so this slot
		// climbs while the guarded ones do not.
		int early = Pattern(boss).TimerArmCount(0);
		harness.Watch(20, null);

		Assert.True(Pattern(boss).TimerArmCount(0) > early,
			"the heartbeat slot never re-armed, so the counter is not measuring arms at all");
	}

	/// <summary>
	/// <b>Masto's bands fire on their own clocks.</b> Counted through <c>TimerFireCount</c> because a
	/// band's only action is a random target switch, which is invisible whenever the pick lands on the
	/// creature already targeted.
	/// </summary>
	[Fact]
	public void MastosBandTimerFires()
	{
		using BossAiHarness harness = BossAiHarness.For(Brusthonin).WithWorldSize(2048)
			.WithAi(typeof(MastoTheAncientAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

		Npc boss = harness.Spawn(Masto, 300f, 300f, 200f);
		Player tank = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		Player other = harness.SpawnPlayer(304f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(boss, tank);
		BossAiHarness.MakeMutuallyKnown(boss, other);

		BossAiHarness.SetExactPercent(boss, 50);
		harness.Engage(boss, tank);

		int before = 0;
		for (int slot = 0; slot < 8; slot++)
			before += Pattern(boss).TimerFireCount(slot);

		harness.Watch(60, null);

		int after = 0;
		for (int slot = 0; slot < 8; slot++)
			after += Pattern(boss).TimerFireCount(slot);

		Assert.True(after > before, $"no timer fired in a minute: {after} against {before}");
	}
}
