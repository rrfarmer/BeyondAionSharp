using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the Sauro Supply Base alarm — <see cref="CombatAlarm"/> on
/// <see cref="BrigadeGeneralShebaAI"/> and <see cref="GuardCaptainAhuradim"/>, answered by
/// <see cref="ShebanBladesmanAI"/> and <see cref="ShebanAmbusherAI"/>. Retail patterns
/// <c>IDVritra_Base_Boss1</c>, <c>Boss2</c>, <c>…_As_IU_Sum2</c> and <c>…_As_Hide</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Both bosses are Java ports rather than patterns, so the alarm is an addition to them. What these
/// pin is that it goes out once a fight, that it names the boss's target rather than whoever swung,
/// and that both guard kinds answer it with the weights retail gives them.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SauroBaseAlarmTests
{
	private const int SauroSupplyBase = 301220000;

	private const int Sheba = 230858;
	private const int Ahuradim = 230857;
	private const int Bladesman = 233286;
	private const int Ambusher = 233277;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(SauroSupplyBase).WithWorldSize(2048)
			.WithAi(typeof(BrigadeGeneralShebaAI), typeof(GuardCaptainAhuradim),
				typeof(ShebanBladesmanAI), typeof(ShebanAmbusherAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	/// <summary>The boss, a guard of each kind beside him, and a player forty metres off.</summary>
	private static (BossAiHarness, Npc, Npc, Npc, Player) Post(int bossId)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(bossId, 300f, 300f, 200f);
		Npc bladesman = harness.Spawn(Bladesman, 315f, 300f, 200f);
		Npc ambusher = harness.Spawn(Ambusher, 316f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 260f, 200f);

		BossAiHarness.MakeMutuallyKnown(boss, bladesman);
		BossAiHarness.MakeMutuallyKnown(boss, ambusher);
		BossAiHarness.MakeMutuallyKnown(bladesman, player);
		BossAiHarness.MakeMutuallyKnown(ambusher, player);

		return (harness, boss, bladesman, ambusher, player);
	}

	/// <summary><b>Pulling Sheba brings both kinds of guard onto the player he is fighting.</b></summary>
	[Fact]
	public void PullingShebaBringsBothKindsOfGuard()
	{
		var (harness, boss, bladesman, ambusher, player) = Post(Sheba);
		using BossAiHarness _h = harness;
		Assert.Null(bladesman.GetTarget());
		Assert.Null(ambusher.GetTarget());

		harness.Engage(boss, player);

		Assert.Same(player, bladesman.GetTarget());
		Assert.Same(player, ambusher.GetTarget());
	}

	/// <summary>And Ahuradim's alarm is the same one.</summary>
	[Fact]
	public void AndAhuradimRaisesTheSameAlarm()
	{
		var (harness, boss, bladesman, ambusher, player) = Post(Ahuradim);
		using BossAiHarness _h = harness;

		harness.Engage(boss, player);

		Assert.Same(player, bladesman.GetTarget());
		Assert.Same(player, ambusher.GetTarget());
	}

	/// <summary>
	/// <b>Once a fight.</b> <c>HandleAttack</c> fires on every swing, so without the latch the alarm
	/// would go out several times a second — a guard arriving later hears nothing.
	/// </summary>
	[Fact]
	public void TheAlarmGoesOutOnceAFight()
	{
		var (harness, boss, bladesman, ambusher, player) = Post(Sheba);
		using BossAiHarness _h = harness;

		harness.Engage(boss, player);

		Npc latecomer = harness.Spawn(Bladesman, 317f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(latecomer, player);
		for (int i = 0; i < 20; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		Assert.Null(latecomer.GetTarget());
	}

	/// <summary>And it re-arms when the fight ends, so a second pull raises it again.</summary>
	[Fact]
	public void AndItReArmsWhenTheFightEnds()
	{
		var (harness, boss, bladesman, ambusher, player) = Post(Sheba);
		using BossAiHarness _h = harness;
		harness.Engage(boss, player);

		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Npc latecomer = harness.Spawn(Bladesman, 317f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(latecomer, player);
		harness.Engage(boss, player);

		Assert.Same(player, latecomer.GetTarget());
	}

	/// <summary>
	/// <b>The guards are weighted differently, and retail is explicit about it.</b> A bladesman brings
	/// three thousand hate to the order and an ambusher one — the only thing separating the two kinds
	/// in the data.
	/// </summary>
	[Fact]
	public void ABladesmanCommitsHarderThanAnAmbusher()
	{
		var (harness, boss, bladesman, ambusher, player) = Post(Sheba);
		using BossAiHarness _h = harness;

		harness.Engage(boss, player);

		// Two thousand from somebody else: enough to outweigh an ambusher's order, not a bladesman's.
		Player rival = harness.SpawnPlayer(316f, 301f, 200f);
		BossAiHarness.MakeMutuallyKnown(bladesman, rival);
		BossAiHarness.MakeMutuallyKnown(ambusher, rival);
		bladesman.GetAggroList().AddHate(rival, 2000);
		ambusher.GetAggroList().AddHate(rival, 2000);

		Assert.Same(player, bladesman.GetAggroList().GetTarget(
			Aion.GameServer.Controllers.Attack.AggroTarget.MOST_HATED));
		Assert.Same(rival, ambusher.GetAggroList().GetTarget(
			Aion.GameServer.Controllers.Attack.AggroTarget.MOST_HATED));
	}
}
