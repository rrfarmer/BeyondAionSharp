using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="GateSquadAI"/>, translated from the retail <c>BGuard_*Gate*</c> family
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The other half of the abyss guards' mechanic: a <c>W</c> guard summons a warp gate, and the gate
/// is what puts the squad out. One mechanic across 69 gates, so what is pinned is the mechanic and
/// one gate of each shape — the common two-wave chain that removes itself, and the fortress-chief
/// chain that loops instead.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GateSquadAiTests
{
	private const int Reshanta = 400010000;

	/// <summary>An Elyos warp gate, <c>BGuard_LGate_L43</c> — two waves, then it leaves.</summary>
	private const int WarpGate = 207612;
	private const int FirstWaveFighter = 207613;
	private const int FirstWaveWizard = 207615;
	private const int SecondWavePriest = 207616;
	private const int SecondWaveRanger = 207614;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(GateSquadAI), typeof(GuardReinforcementAI), typeof(AbyssGuardSimpleAI), typeof(AbyssGuardReinforcementAI),
				typeof(AggressiveNpcAI)).Build();

	private static (BossAiHarness, Npc, Player) Attacked(int npcId)
	{
		BossAiHarness harness = NewHarness();
		Npc gate = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		harness.Engage(gate, player);
		return (harness, gate, player);
	}

	private static void Advance(BossAiHarness harness, Npc gate, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(gate, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// A gate nobody has touched puts nothing out. The whole chain hangs off being attacked, which is
	/// what makes a warp gate furniture until someone engages it.
	/// </summary>
	[Fact]
	public void AGateNobodyAttacksPutsNothingOut()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;
		Npc gate = harness.Spawn(WarpGate, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(120));

		Assert.Equal(0, Count(harness, FirstWaveFighter));
		Assert.True(gate.IsSpawned(), "an untouched gate should still be standing");
	}

	/// <summary>Ten seconds after it is attacked, the first wave steps out.</summary>
	[Fact]
	public void TheFirstWaveComesTenSecondsAfterItIsAttacked()
	{
		var (harness, gate, player) = Attacked(WarpGate);
		using BossAiHarness _h = harness;

		Advance(harness, gate, player, 8);
		Assert.Equal(0, Count(harness, FirstWaveFighter));

		Advance(harness, gate, player, 4);

		Assert.Equal(1, Count(harness, FirstWaveFighter));
		Assert.Equal(1, Count(harness, FirstWaveWizard));
	}

	/// <summary>
	/// The second wave is thirty seconds behind it and is a different three, which is why the chain is
	/// a table rather than "spawn the same squad twice".
	/// </summary>
	[Fact]
	public void TheSecondWaveIsThirtySecondsBehindAndDiffers()
	{
		var (harness, gate, player) = Attacked(WarpGate);
		using BossAiHarness _h = harness;

		Advance(harness, gate, player, 12);
		Assert.Equal(0, Count(harness, SecondWavePriest));

		Advance(harness, gate, player, 30);

		Assert.Equal(1, Count(harness, SecondWavePriest));
		Assert.Equal(1, Count(harness, SecondWaveRanger));

		// The wizard appears in both waves, so by now there are two of it.
		Assert.Equal(2, Count(harness, FirstWaveWizard));
	}

	/// <summary>And five seconds after the last wave the gate itself is gone.</summary>
	[Fact]
	public void ThenTheGateRemovesItself()
	{
		var (harness, gate, player) = Attacked(WarpGate);
		using BossAiHarness _h = harness;

		Advance(harness, gate, player, 42);
		Assert.True(gate.IsSpawned(), "it should still be standing when its last wave lands");

		Advance(harness, gate, player, 6);

		Assert.False(gate.IsSpawned());
	}

	/// <summary>
	/// Leaving the fight takes the squad and the gate with it — retail despawns the spawn group and
	/// then itself, so a reset does not strand a squad on the field.
	/// </summary>
	[Fact]
	public void LeavingTheFightClearsTheSquadAndTheGate()
	{
		var (harness, gate, player) = Attacked(WarpGate);
		using BossAiHarness _h = harness;
		Advance(harness, gate, player, 12);
		Assert.Equal(1, Count(harness, FirstWaveFighter));

		gate.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Equal(0, Count(harness, FirstWaveFighter));
		Assert.False(gate.IsSpawned());
	}

	/// <summary>A fortress-chief gate, <c>BGuard_LGateChiefF4A</c> — three waves, then round again.</summary>
	private const int ChiefGate = 296526;
	private const int Warguard = 296528;
	private const int Aetherguard = 296531;

	/// <summary>
	/// The chief gates do not leave. Their last wave arms the <b>first</b> one again, so they keep
	/// producing squads for as long as anybody is still fighting them.
	/// </summary>
	/// <remarks>
	/// Worth its own pin because the difference is invisible in the table without reading two columns
	/// together — a chain that loops has no despawn delay, and reading that as "no despawn step found"
	/// would have left these gates standing idle after three waves instead of cycling. A mutation that
	/// sent looping gates to the closing branch passed until this existed.
	/// </remarks>
	[Fact]
	public void AChiefGateLoopsInsteadOfLeaving()
	{
		var (harness, gate, player) = Attacked(ChiefGate);
		using BossAiHarness _h = harness;

		// Five seconds to the first wave, then forty to each of the next two.
		Advance(harness, gate, player, 90);
		int afterOneRound = Count(harness, Aetherguard);
		Assert.True(afterOneRound >= 2, $"the first round should have called at least two: {afterOneRound}");
		Assert.True(gate.IsSpawned(), "a chief gate does not remove itself");

		// Round again: the chain returns to its first wave rather than to a closing branch.
		Advance(harness, gate, player, 90);

		Assert.True(Count(harness, Aetherguard) > afterOneRound,
			"a looping gate should keep calling");
		Assert.True(Count(harness, Warguard) >= 2, "and the first wave should come round again");
		Assert.True(gate.IsSpawned());
	}
}
