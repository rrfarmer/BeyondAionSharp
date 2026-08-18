using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="KlawSpawnerAI"/> and <see cref="BroadAttAnswerAI"/>, translated from retail
/// patterns <c>BroadAtt_MR</c>, <c>ND2_CnD_RE1_egg</c>, <c>ND2_CnD_BR1_egg</c> and <c>D2_FnA_B1</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The nest is kept out of the player's known list so the call is the only way it can reach them, and
/// the player is Asmodian because the aggro list refuses hate between friends. Everything stands
/// within a few metres of everything else, because a listener too far from the player cannot take hate
/// on them however well it heard the call.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class KlawSpawnerAiTests
{
	private const int Heiron = 210040000;

	private const int Spawner = 700169;
	private const int Worker = 210874;
	private const int Seeker = 210928;
	private const int Kerub = 210670;
	private const int Klawspawn = 700209;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Heiron).WithWorldSize(2048)
			.WithAi(typeof(KlawSpawnerAI), typeof(KlawspawnAI), typeof(BroadAttAnswerAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <remarks>
	/// <b>The player stands outside the listeners' own sight</b> — their <c>srange</c> is seven and
	/// eight metres and the raider is eleven away — so the call, which reaches twenty-five and fifty,
	/// is doing all the work rather than sharing it with an aggro scan.
	/// <para>
	/// Worth having, and <em>not</em> the fix for the flake it was first written for: the seven-in-fifty
	/// failures measured here turned out to be a poisoned <c>SiegeService</c> type initialiser, not this
	/// geometry. See <c>SiegeServiceTestInit</c> and docs/retail-ai-fidelity.md.
	/// </para>
	/// </remarks>
	private static (BossAiHarness, Npc, Npc, Player) Nest()
	{
		BossAiHarness harness = NewHarness();
		Npc spawner = harness.Spawn(Spawner, 300f, 300f, 200f);
		Npc worker = harness.Spawn(Worker, 310f, 300f, 200f);
		Player raider = harness.SpawnPlayer(299f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(spawner, worker);
		return (harness, spawner, worker, raider);
	}

	private static void Strike(Npc target, Creature attacker) =>
		target.GetAi().OnCreatureEvent(AiEventType.Attack, attacker);

	/// <summary><b>Striking the spawner brings the nest onto whoever struck it.</b></summary>
	[Fact]
	public void StrikingTheSpawnerBringsTheNest()
	{
		var (harness, spawner, worker, raider) = Nest();
		using BossAiHarness _h = harness;
		Assert.Null(worker.GetTarget());

		Strike(spawner, raider);

		Assert.Same(raider, worker.GetTarget());
		Assert.Equal(100, worker.GetAggroList().GetHate(raider));
	}

	/// <summary>Every blow calls again, which is how retail keeps the nest on the right player.</summary>
	[Fact]
	public void EveryBlowCallsAgain()
	{
		var (harness, spawner, worker, raider) = Nest();
		using BossAiHarness _h = harness;

		Strike(spawner, raider);
		Strike(spawner, raider);
		Strike(spawner, raider);

		Assert.Equal(300, worker.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And only within twenty-five metres.</b> Retail gives this family three ranges and the
	/// spawner has the middle one.
	/// </summary>
	[Fact]
	public void AndOnlyWithinTwentyFiveMetres()
	{
		var (harness, spawner, worker, raider) = Nest();
		using BossAiHarness _h = harness;

		Npc distant = harness.Spawn(Seeker, 330f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(spawner, distant);

		Strike(spawner, raider);

		Assert.Equal(100, worker.GetAggroList().GetHate(raider));
		Assert.Null(distant.GetTarget());
	}

	/// <summary>
	/// <b>A hundred is a claim and one is a glance.</b> The klaws commit to whoever struck the spawner;
	/// the kerubs join and are moved by the next thing that happens. It is the only difference between
	/// the two nests in retail's data.
	/// </summary>
	[Fact]
	public void AHundredIsAClaimAndOneIsAGlance()
	{
		var (harness, spawner, worker, raider) = Nest();
		using BossAiHarness _h = harness;

		Npc kerub = harness.Spawn(Kerub, 311f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(spawner, kerub);

		Strike(spawner, raider);

		Assert.Equal(100, worker.GetAggroList().GetHate(raider));
		Assert.Equal(1, kerub.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The klawspawn shares the call.</b> Retail binds <c>BroadAtt_MR</c> to both it and the
	/// spawner, and it runs a Java-parity class of its own — so the call had to be added there as well
	/// as here, and this is what says so.
	/// </summary>
	[Fact]
	public void TheKlawspawnSharesTheCall()
	{
		using BossAiHarness harness = NewHarness();
		Npc klawspawn = harness.Spawn(Klawspawn, 300f, 300f, 200f);
		Npc worker = harness.Spawn(Worker, 310f, 300f, 200f);
		Player raider = harness.SpawnPlayer(299f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(klawspawn, worker);

		Strike(klawspawn, raider);

		Assert.Equal(100, worker.GetAggroList().GetHate(raider));
	}
}
