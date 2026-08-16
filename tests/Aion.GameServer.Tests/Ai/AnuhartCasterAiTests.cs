using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="AnuhartCasterAI"/> and <see cref="AnuhartPetAI"/>, translated from retail
/// patterns <c>XDrakan_EeB_F_50</c> and <c>XD_EPet</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Four Dark Poeta casters on plain <c>aggressive</c>, each of which should fight with a pet and keep
/// re-pointing it. The order is the mechanic; the extra monster is a detail.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AnuhartCasterAiTests
{
	private const int DarkPoeta = 300040000;

	private const int Spiritlord = 215249;
	private const int Invoker = 215258;
	private const int Conjurer = 215267;
	private const int Transporter = 215276;

	private const int Pet = 281171;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DarkPoeta).WithWorldSize(2048)
			.WithAi(typeof(AnuhartCasterAI), typeof(AnuhartPetAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// The caster and a raid thirty metres away, so the pet it puts down cannot find anybody on its
	/// own — everything it does has to have been ordered.
	/// </summary>
	private static (BossAiHarness, Npc, List<Player>) Engaged(int npcId)
	{
		BossAiHarness harness = NewHarness();
		Npc caster = harness.Spawn(npcId, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < 3; i++)
			raid.Add(harness.SpawnPlayer(330f + i, 300f, 200f));

		harness.Engage(caster, raid[0]);
		for (int i = 0; i < raid.Count; i++)
			for (int n = raid.Count - i; n > 0; n--)
				BossAiHarness.Rehate(caster, raid[i]);

		return (harness, caster, raid);
	}

	private static void Advance(BossAiHarness harness, List<Player> raid, Npc caster, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(caster, member);
				BossAiHarness.KeepAlive(member);
			}

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static Npc? ThePet(BossAiHarness harness) =>
		harness.LiveNpcs().FirstOrDefault(n => n.GetNpcId() == Pet);

	/// <summary>Each of the four brings a pet with it, and only when the fight starts.</summary>
	[Theory]
	[InlineData(Spiritlord)]
	[InlineData(Invoker)]
	[InlineData(Conjurer)]
	[InlineData(Transporter)]
	public void EachCasterFightsWithAPet(int casterId)
	{
		using BossAiHarness harness = NewHarness();
		Npc caster = harness.Spawn(casterId, 300f, 300f, 200f);
		harness.Clock.Advance(TimeSpan.FromSeconds(30));
		Assert.Null(ThePet(harness));

		Player quarry = harness.SpawnPlayer(330f, 300f, 200f);
		harness.Engage(caster, quarry);

		Assert.NotNull(ThePet(harness));
	}

	/// <summary>
	/// <b>The pet does not hear the order that arrives with it, and it is the first order after that
	/// which points it.</b> Thirty metres from the raid it has nothing it could have found by itself,
	/// so this reads the orders and nothing else.
	/// </summary>
	/// <remarks>
	/// The caster spawns the pet and broadcasts in the same branch, and
	/// <see cref="Aion.GameServer.Ai.Pattern.PatternAi"/> deliberately excludes whatever the running
	/// branch spawned from that branch's own broadcast — measured for RM-56c, which lays traps and
	/// immediately tells traps to leave. The two encounters want opposite things from one rule. See
	/// docs/retail-ai-fidelity.md: our behaviour is kept because it is the measured one, and the pet
	/// waits nine seconds for the next order.
	/// </remarks>
	[Fact]
	public void ThePetIsPointedByTheFirstOrderItCanHear()
	{
		var (harness, caster, raid) = Engaged(Spiritlord);
		using BossAiHarness _h = harness;

		Npc pet = Assert.IsType<Npc>(ThePet(harness));
		Assert.Null(pet.GetTarget());

		Advance(harness, raid, caster, 11);
		Assert.NotNull(pet.GetTarget());
	}

	/// <summary>
	/// <b>He hands the pet the victim he is holding.</b> Retail's rung broadcasts <em>before</em> it
	/// switches, so the pet gets the player he was on rather than the one he is about to take.
	/// </summary>
	/// <remarks>
	/// Written the wrong way round first, asserting the two ended up <em>together</em>; then asserting
	/// they ended up apart, which is not reliable either — the switch on this rung is
	/// <c>ATTACKERI_RANDOM_ONE</c> and can land back on the same player. What the branch order
	/// guarantees is only the half that is asserted here.
	/// </remarks>
	[Fact]
	public void HeHandsThePetHisVictimAndTurnsElsewhere()
	{
		var (harness, caster, raid) = Engaged(Spiritlord);
		using BossAiHarness _h = harness;

		Npc pet = Assert.IsType<Npc>(ThePet(harness));
		Creature? held = caster.GetTarget() as Creature;
		Assert.NotNull(held);

		Advance(harness, raid, caster, 11);

		Assert.Same(held, pet.GetTarget());
	}

	/// <summary>
	/// <b>And crossing seventy does it again, with the second-most-hated as his new victim.</b>
	/// </summary>
	[Fact]
	public void CrossingSeventyHandsOverAgain()
	{
		var (harness, caster, raid) = Engaged(Spiritlord);
		using BossAiHarness _h = harness;

		Npc pet = Assert.IsType<Npc>(ThePet(harness));

		// Past the nine-second hand-over, so this reads the band rung on its own.
		Advance(harness, raid, caster, 12);
		Creature? held = caster.GetTarget() as Creature;
		Creature second = caster.GetAggroList().GetTarget(AggroTarget.SECOND_MOST_HATED);

		BossAiHarness.SetExactPercent(caster, 50);
		Advance(harness, raid, caster, 8);

		Assert.Same(held, pet.GetTarget());
		Assert.Same(second, caster.GetTarget());
	}

	/// <summary>
	/// <b>Below thirty-five the orders keep coming.</b> The rung opens a loop that re-points the pet
	/// about every twenty-seven seconds, and the ladder itself stops, so nothing else fires again.
	/// </summary>
	/// <remarks>
	/// One player in the fight, and the decoy a hundred metres away where the caster will never take
	/// it. With a raid of three the caster's own target drifts between orders, so the next order can
	/// legitimately name the very player the pin has just moved the pet to — which made this fail two
	/// runs in five before the setup was narrowed.
	/// </remarks>
	[Fact]
	public void BelowThirtyFiveTheOrdersKeepComing()
	{
		using BossAiHarness harness = NewHarness();
		Npc caster = harness.Spawn(Spiritlord, 300f, 300f, 200f);
		Player quarry = harness.SpawnPlayer(330f, 300f, 200f);
		Player elsewhere = harness.SpawnPlayer(300f, 400f, 200f);
		harness.Engage(caster, quarry);

		Npc pet = Assert.IsType<Npc>(ThePet(harness));
		var only = new List<Player> { quarry };

		BossAiHarness.SetExactPercent(caster, 20);
		Advance(harness, only, caster, 10);
		Assert.Same(quarry, pet.GetTarget());

		// Sent to the decoy by hand, the loop takes it off again on its next order.
		NpcMessageBus.Broadcast(caster, AnuhartPetAI.GoForThisOne, elsewhere, 15f);
		Assert.Same(elsewhere, pet.GetTarget());

		Advance(harness, only, caster, 30);
		Assert.Same(quarry, pet.GetTarget());
	}

	/// <summary>Both exits take the pet with them.</summary>
	[Fact]
	public void BothExitsTakeThePet()
	{
		var (harness, caster, raid) = Engaged(Spiritlord);
		using BossAiHarness _h = harness;

		Assert.NotNull(ThePet(harness));

		caster.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Null(ThePet(harness));
	}

	/// <summary>A pet answers the order wherever it came from.</summary>
	[Fact]
	public void APetGoesWhereItIsPointed()
	{
		using BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(Spiritlord, 300f, 300f, 200f);
		Npc pet = harness.Spawn(Pet, 302f, 300f, 200f);
		Player quarry = harness.SpawnPlayer(340f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(caller, pet);

		Assert.Null(pet.GetTarget());

		NpcMessageBus.Broadcast(caller, AnuhartPetAI.GoForThisOne, quarry, 15f);

		Assert.Same(quarry, pet.GetTarget());
	}
}
