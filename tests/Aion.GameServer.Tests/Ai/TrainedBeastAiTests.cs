using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the trained beasts and their breeders, translated from retail patterns
/// <c>Lizardman_BeastB</c> and <c>Lizardman_BeastKA</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class TrainedBeastAiTests
{
	private const int DraupnirCave = 300030000;

	private const int TrainedMonitor = 213396;
	private const int TrainedTipolid = 213980;
	private const int BakarmaBreeder = 213398;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DraupnirCave).WithWorldSize(2048)
			.WithAi(typeof(TrainedBeastAI), typeof(BakarmaBreederAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>A beast fighting a raider, with its breeder close enough to hear it.</summary>
	private static (BossAiHarness, Npc, Npc, Player) Pen(int beastId = TrainedMonitor)
	{
		BossAiHarness harness = NewHarness();
		Npc beast = harness.Spawn(beastId, 300f, 300f, 200f);
		Npc breeder = harness.Spawn(BakarmaBreeder, 305f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(beast, breeder);
		harness.Engage(beast, raider);
		return (harness, beast, breeder, raider);
	}

	/// <summary>
	/// <b>At a quarter health the beast calls its breeder, once.</b> A trained animal that is losing
	/// shouts for the person who trained it.
	/// </summary>
	[Theory]
	[InlineData(TrainedMonitor)]
	[InlineData(TrainedTipolid)]
	public void AtAQuarterTheBeastCallsItsBreeder(int beastId)
	{
		var (harness, beast, breeder, raider) = Pen(beastId);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(beast, 40);
		beast.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		Assert.Equal(0, breeder.GetAggroList().GetHate(raider));

		BossAiHarness.SetExactPercent(beast, 20);
		beast.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(1, breeder.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>One point is a glance, not a claim.</b> Enough to bring the breeder into the fight, nowhere
	/// near enough to take a player off whoever they were already fighting — the vasharti watch's
	/// distinction, and retail's own <c>point_to_add</c>.
	/// </summary>
	[Fact]
	public void OnePointIsAGlanceNotAClaim()
	{
		var (harness, beast, breeder, raider) = Pen();
		using BossAiHarness _h = harness;

		Player other = harness.SpawnPlayer(307f, 300f, 200f, race: Race.ASMODIANS);
		harness.Engage(breeder, other);
		int held = breeder.GetAggroList().GetHate(other);

		BossAiHarness.SetExactPercent(beast, 20);
		beast.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.True(breeder.GetAggroList().GetHate(raider) < held,
			"one point took the breeder off the player it was already fighting");
	}

	/// <summary>
	/// <b>The melee branch names the attacker and the spell branch names the caster</b>, which for a
	/// beast focused by two players are two different people. A single "name my target" would send the
	/// breeder after whoever the beast happened to be holding instead.
	/// </summary>
	[Fact]
	public void TheSpellBranchNamesTheCasterNotTheTarget()
	{
		var (harness, beast, breeder, raider) = Pen();
		using BossAiHarness _h = harness;

		Player caster = harness.SpawnPlayer(298f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(breeder, beast);

		BossAiHarness.SetExactPercent(beast, 20);
		beast.GetAi().OnCreatureEvent(AiEventType.Spelled, caster);

		Assert.Equal(1, breeder.GetAggroList().GetHate(caster));
		Assert.Equal(0, breeder.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And the flag is shared, so a beast calls once however it was hurt.</b>
	/// </summary>
	[Fact]
	public void AndTheFlagIsShared()
	{
		var (harness, beast, breeder, raider) = Pen();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(beast, 20);
		beast.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		Assert.Equal(1, breeder.GetAggroList().GetHate(raider));

		for (int i = 0; i < 3; i++)
			beast.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(1, breeder.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And only within ten metres</b>, which is retail's range — so a beast calls its own breeder.
	/// </summary>
	[Fact]
	public void AndOnlyWithinTenMetres()
	{
		var (harness, beast, breeder, raider) = Pen();
		using BossAiHarness _h = harness;

		Npc distant = harness.Spawn(BakarmaBreeder, 330f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(beast, distant);

		BossAiHarness.SetExactPercent(beast, 20);
		beast.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(1, breeder.GetAggroList().GetHate(raider));
		Assert.Equal(0, distant.GetAggroList().GetHate(raider));
	}

	/// <summary><b>The message number is retail's, not ours.</b></summary>
	[Fact]
	public void TheMessageNumberIsRetails()
	{
		Assert.Equal(3297, TrainedBeastAI.ThisOne);
	}
}
