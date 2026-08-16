using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="ElementalWaveAI"/> and Frostmane Lestin's half of it, translated from retail
/// patterns <c>ND2_ElementalSu2</c> and <c>ND2_PnF</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The second encounter found with this shape, after Queen Modor's pillar trio: a boss that places a
/// wave and then names the player it wants them on. Both run through <see cref="SummonOrder"/>.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ElementalWaveAiTests
{
	private const int Beluslan = 220040000;

	private const int Lestin = 212875;
	private const int FirstWave = 280489;
	private const int SecondWave = 280490;
	private const int FaithfulServant = 280333;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Beluslan).WithWorldSize(2048)
			.WithAi(typeof(FrostmaneLestinAI), typeof(ElementalWaveAI), typeof(AggressiveNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// The whole chain, driven through Lestin himself: he crosses into 66–90, places four elementals
	/// and names his quarry, and the wave arrives already on that player.
	/// </summary>
	/// <remarks>
	/// <b>Two things here are load-bearing, and both were found by a mutation surviving.</b>
	/// <para>
	/// The quarry stands <b>forty-five metres</b> out. These elementals are aggressive and spawn within
	/// fifteen metres of Lestin, so a quarry beside him is one they find by themselves — asserting the
	/// target then passes whether or not the order was ever sent. Forty-five is outside what they can
	/// see and inside his fifty-metre broadcast.
	/// </para>
	/// <para>
	/// The listener is a <b>stand-in placed before the fight</b> rather than one of the four he
	/// summons. That is belt-and-braces rather than necessity: the four <em>do</em> hear the order —
	/// measured, not assumed — because our spawn path puts a summon in its spawner's known list before
	/// the next action of the same branch runs. Asserting on a listener that was already there pins
	/// the same fact without depending on that ordering, which is a property of our engine rather than
	/// of the pattern. See docs/retail-ai-fidelity.md, where the same ordering blocks RM-56c.
	/// </para>
	/// <para>
	/// Asserts the target rather than the hate: the exact single point retail adds is pinned in
	/// <see cref="DanuarSummonOrderAiTests"/>, where the summon is isolated.
	/// </para>
	/// </remarks>
	[Fact]
	public void HisWaveArrivesOnThePlayerHeNames()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Lestin, 300f, 300f, 200f);
		Player quarry = harness.SpawnPlayer(345f, 300f, 200f);
		Npc standIn = harness.Spawn(FirstWave, 304f, 300f, 200f);
		// Known to the boss so it hears him, and deliberately *not* to the quarry: making those two
		// known to each other lets an aggressive elemental find the player on its own, and the pin
		// then passes whether or not the order named anybody.
		BossAiHarness.MakeMutuallyKnown(boss, standIn);
		harness.Engage(boss, quarry);

		BossAiHarness.SetExactPercent(boss, 80);
		for (int i = 0; i < 12; i++)
		{
			BossAiHarness.Rehate(boss, quarry);
			BossAiHarness.KeepAlive(quarry);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		// Four placed, plus the stand-in that was already there.
		Assert.Equal(5, Count(harness, FirstWave));
		Assert.Same(quarry, standIn.GetTarget());
	}

	/// <summary>
	/// <b>Every summoning rung sends it, not just the first.</b> Retail puts the broadcast on all
	/// three, so a raid that pushes him through two bands gets both waves assigned — a port that wired
	/// only the opening rung would look right for the first thirty seconds of the fight.
	/// </summary>
	[Fact]
	public void TheSecondBandsWaveIsNamedAsWell()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Lestin, 300f, 300f, 200f);
		Player quarry = harness.SpawnPlayer(345f, 300f, 200f);
		Npc standIn = harness.Spawn(SecondWave, 304f, 300f, 200f);
		// Known to the boss so it hears him, and deliberately *not* to the quarry: making those two
		// known to each other lets an aggressive elemental find the player on its own, and the pin
		// then passes whether or not the order named anybody.
		BossAiHarness.MakeMutuallyKnown(boss, standIn);
		harness.Engage(boss, quarry);

		BossAiHarness.SetExactPercent(boss, 50);
		for (int i = 0; i < 12; i++)
		{
			BossAiHarness.Rehate(boss, quarry);
			BossAiHarness.KeepAlive(quarry);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		Assert.Equal(5, Count(harness, SecondWave));
		Assert.Same(quarry, standIn.GetTarget());
	}

	/// <summary>A wave takes the order and nothing else, as the Danuar summons do.</summary>
	[Fact]
	public void AWaveAnswersOnlyItsOwnMessage()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Lestin, 300f, 300f, 200f);
		Npc summon = harness.Spawn(FirstWave, 303f, 300f, 200f);
		Player named = harness.SpawnPlayer(306f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, summon);
		BossAiHarness.MakeMutuallyKnown(summon, named);

		NpcMessageBus.Broadcast(boss, ElementalWaveAI.OrderMessage + 1, named, ElementalWaveAI.OrderRange);
		Assert.Equal(0, summon.GetAggroList().GetHate(named));

		NpcMessageBus.Broadcast(boss, ElementalWaveAI.OrderMessage, named, ElementalWaveAI.OrderRange);
		Assert.Equal(1, summon.GetAggroList().GetHate(named));
	}

	/// <summary>
	/// The fire boss's servants share the pattern and answer the same message — they simply have no
	/// sender yet, because <c>ND2_ElementalSu</c> is untranslated. Pinned so the listener half is not
	/// quietly narrowed to Lestin's waves later.
	/// </summary>
	[Fact]
	public void TheFireBossesServantsShareTheListener()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Lestin, 300f, 300f, 200f);
		Npc servant = harness.Spawn(FaithfulServant, 303f, 300f, 200f);
		Player named = harness.SpawnPlayer(306f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, servant);
		BossAiHarness.MakeMutuallyKnown(servant, named);

		NpcMessageBus.Broadcast(boss, ElementalWaveAI.OrderMessage, named, ElementalWaveAI.OrderRange);

		Assert.Same(named, servant.GetTarget());
	}

	/// <summary>Retail's fifty metres, pinned against a literal — see the Danuar equivalent.</summary>
	[Fact]
	public void TheOrdersRangeIsRetailsFifty()
	{
		Assert.Equal(50f, ElementalWaveAI.OrderRange);
	}
}
