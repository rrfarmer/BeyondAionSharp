using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the tursin loudmouths and everyone who answers them, translated from retail patterns
/// <c>Krall_KnA</c>, <c>Krall_KnC</c>, <c>NKrall_KeA</c>, <c>NBrownie_FnC</c>, <c>Brownie_FnQ</c> and
/// <c>Brownie_FnR</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class TursinLoudmouthAiTests
{
	private const int Altgard = 220030000;

	private const int TursinBigBoss = 210160;   // Krall_KnA -- calls below forty
	private const int KaidanBigmouth = 210838;  // NKrall_KeA -- calls on a clock
	private const int MamakiWorker = 210834;    // NBrownie_FnC -- answers with a hundred
	private const int DukakiMiner = 210145;     // Brownie_FnQ -- answers with a hundred and one

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Altgard).WithWorldSize(2048)
			.WithAi(typeof(TursinLoudmouthAI), typeof(KaidanBigmouthAI), typeof(MamakiWorkerAI),
				typeof(DukakiMinerAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, Npc, Player) Camp(int callerId, int answererId)
	{
		BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(callerId, 300f, 300f, 200f);
		Npc answerer = harness.Spawn(answererId, 306f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(caller, answerer);
		return (harness, caller, answerer, raider);
	}

	/// <summary>
	/// <b>Below forty health the boss names whoever is beating it, and the dukaki come.</b> The
	/// creature with the pattern is not the threat; the creature that answers is.
	/// </summary>
	[Fact]
	public void BelowFortyTheBossCallsAndTheDukakiCome()
	{
		var (harness, boss, miner, raider) = Camp(TursinBigBoss, DukakiMiner);
		using BossAiHarness _h = harness;

		harness.Engage(boss, raider);
		BossAiHarness.SetExactPercent(boss, 60);
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		Assert.Equal(0, miner.GetAggroList().GetHate(raider));

		BossAiHarness.SetExactPercent(boss, 30);
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(101, miner.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A mamaki worker answers with a hundred, a dukaki with a hundred and one.</b> Retail gives the
	/// workers a bare switch and the miners an <c>add_hate_point</c> before it, which is one point of
	/// difference and is retail's.
	/// </summary>
	[Fact]
	public void TheWorkersAnswerWithAHundredAndTheMinersWithOneMore()
	{
		var (harness, boss, worker, raider) = Camp(TursinBigBoss, MamakiWorker);
		using BossAiHarness _h = harness;

		Npc miner = harness.Spawn(DukakiMiner, 307f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, miner);

		harness.Engage(boss, raider);
		BossAiHarness.SetExactPercent(boss, 30);
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(100, worker.GetAggroList().GetHate(raider));
		Assert.Equal(101, miner.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A spell calls them too, and the flag is shared</b>, so a boss calls once however it is being
	/// beaten.
	/// </summary>
	[Fact]
	public void ASpellCallsThemTooAndTheFlagIsShared()
	{
		var (harness, boss, miner, raider) = Camp(TursinBigBoss, DukakiMiner);
		using BossAiHarness _h = harness;

		harness.Engage(boss, raider);
		BossAiHarness.SetExactPercent(boss, 30);
		boss.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);
		Assert.Equal(101, miner.GetAggroList().GetHate(raider));

		Npc late = harness.Spawn(DukakiMiner, 302f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, late);
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(0, late.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The kaidan bigmouth calls on a clock rather than on its health</b> — fifteen seconds into any
	/// fight, once. One killed inside fifteen seconds never calls, and one that survives always does,
	/// whatever health it is on.
	/// </summary>
	/// <remarks>
	/// <b>Measured as a jump, because the baseline is neither zero nor fixed.</b> A fight running near
	/// a friendly npc puts a point on the attacker through the engine's own support aggro — not at
	/// engage, but on the first attack tick — so the miner already has one point before any call is
	/// made. The first version asserted zero and failed on that point, and the second took the baseline
	/// too early and failed on the same one. What separates "called" from "did not" is the size of the
	/// step, not the total.
	/// </remarks>
	[Fact]
	public void TheBigmouthCallsOnAClockAndNotOnItsHealth()
	{
		var (harness, bigmouth, miner, raider) = Camp(KaidanBigmouth, DukakiMiner);
		using BossAiHarness _h = harness;

		harness.Engage(bigmouth, raider);

		harness.Watch(10, null);
		int beforeTheCall = miner.GetAggroList().GetHate(raider);
		Assert.True(beforeTheCall < 101, "the bigmouth called inside ten seconds");

		harness.Watch(10, null);

		Assert.Equal(beforeTheCall + 101, miner.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And it calls once</b>: the timer that carries the call is never re-armed, so the second
	/// clock — which runs forever — carries a different number entirely.
	/// </summary>
	[Fact]
	public void AndTheBigmouthCallsOnlyOnce()
	{
		var (harness, bigmouth, miner, raider) = Camp(KaidanBigmouth, DukakiMiner);
		using BossAiHarness _h = harness;

		harness.Engage(bigmouth, raider);
		harness.Watch(20, null);
		int afterFirst = miner.GetAggroList().GetHate(raider);

		harness.Watch(90, null);

		Assert.Equal(afterFirst, miner.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>Fifteen metres for a tursin and twenty for a kaidan</b>, which is retail's — the bigger mouth
	/// carries further.
	/// </summary>
	[Fact]
	public void TheBiggerMouthCarriesFurther()
	{
		var (near, boss, close, raider) = Camp(TursinBigBoss, DukakiMiner);
		using BossAiHarness _n = near;
		Npc far = near.Spawn(DukakiMiner, 318f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, far);

		near.Engage(boss, raider);
		BossAiHarness.SetExactPercent(boss, 30);
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(101, close.GetAggroList().GetHate(raider));
		Assert.Equal(0, far.GetAggroList().GetHate(raider));

		var (wide, bigmouth, alsoClose, other) = Camp(KaidanBigmouth, DukakiMiner);
		using BossAiHarness _w = wide;
		Npc alsoFar = wide.Spawn(DukakiMiner, 318f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(bigmouth, alsoFar);

		wide.Engage(bigmouth, other);
		wide.Watch(20, null);

		Assert.True(alsoFar.GetAggroList().GetHate(other) >= 101,
			"twenty metres did not carry to a miner fifteen away");
	}

	/// <summary><b>The message number and both ranges are retail's, not ours.</b></summary>
	[Fact]
	public void TheNumbersAreRetails()
	{
		Assert.Equal(1002, TursinLoudmouthAI.GetHim);
		Assert.Equal(15f, TursinLoudmouthAI.CallReach);
		Assert.Equal(20f, KaidanBigmouthAI.CallReach);
	}
}
