using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the Catacombs bosses' threat rule, translated from retail patterns
/// <c>IDCT_Boss_TombsDrakan</c>, <c>_Hard</c>, <c>IDCT_Boss_ElementalFire_Hard</c>,
/// <c>IDCT_Boss_DeathKnight</c> and <c>_Hard</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class CatacombsBossAiTests
{
	private const int Catacombs = 300100000;

	private const int TarosLifebane = 216248;
	private const int TarosLifebaneHard = 216167;
	private const int CaptainLakhara = 216238;
	private const int Flarestorm = 216168;
	private const int Ahbana = 216239;
	private const int AhbanaHard = 216158;
	private const int Soulcaller = 216159;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Catacombs).WithWorldSize(2048)
			.WithAi(typeof(CatacombsBoss35kAI), typeof(CatacombsBoss30kAI), typeof(CatacombsBoss22kAI),
				typeof(CatacombsBoss5kAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// What one blow from this player adds to the boss's aggro list, over and above the blow itself.
	/// </summary>
	/// <remarks>
	/// A delta, and taken before anything else touches the pair — the village killers cost two commits
	/// to learn that a baseline read after the setup measures a branch that has already fired.
	/// </remarks>
	private static int OneBlowAdds(BossAiHarness harness, Npc boss, Player hitter)
	{
		int before = boss.GetAggroList().GetHate(hitter);
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, hitter);
		return boss.GetAggroList().GetHate(hitter) - before;
	}

	private static Player Templar(BossAiHarness harness)
	{
		Player player = harness.SpawnPlayer(305f, 300f, 200f);
		player.GetCommonData().SetPlayerClass(PlayerClass.TEMPLAR);
		return player;
	}

	/// <summary>
	/// <b>A templar's blow counts for thousands more, and nothing in the client says so.</b> This is
	/// retail's threat assistance for tanks: without it a Catacombs boss is held by whoever does the
	/// most damage.
	/// </summary>
	[Theory]
	[InlineData(TarosLifebane, 35_000)]
	[InlineData(Ahbana, 30_000)]
	[InlineData(CaptainLakhara, 22_000)]
	[InlineData(Flarestorm, 5_000)]
	[InlineData(TarosLifebaneHard, 5_000)]
	[InlineData(AhbanaHard, 5_000)]
	[InlineData(Soulcaller, 5_000)]
	public void ATemplarsBlowCountsForThousandsMore(int bossId, int expected)
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(bossId, 300f, 300f, 200f);
		Player templar = Templar(harness);
		BossAiHarness.MakeMutuallyKnown(boss, templar);

		Assert.Equal(expected, OneBlowAdds(harness, boss, templar));
	}

	/// <summary>
	/// <b>Every boss with two modes helps a templar less on hard.</b> Retail's numbers, not a slip: the
	/// weights are not ordered the way the difficulty is, and one shared constant for the instance
	/// would erase exactly this.
	/// </summary>
	[Theory]
	[InlineData(TarosLifebane, 35_000, TarosLifebaneHard, 5_000)]
	[InlineData(Ahbana, 30_000, AhbanaHard, 5_000)]
	public void HardModeHelpsTheTemplarLess(int normalId, int normalHate, int hardId, int hardHate)
	{
		using BossAiHarness harness = NewHarness();
		Npc normal = harness.Spawn(normalId, 300f, 300f, 200f);
		Npc hard = harness.Spawn(hardId, 320f, 300f, 200f);
		Player templar = Templar(harness);
		BossAiHarness.MakeMutuallyKnown(normal, templar);
		BossAiHarness.MakeMutuallyKnown(hard, templar);

		Assert.Equal(normalHate, OneBlowAdds(harness, normal, templar));
		Assert.Equal(hardHate, OneBlowAdds(harness, hard, templar));
		Assert.True(hardHate < normalHate, "hard mode did not help less");
	}

	/// <summary>
	/// <b>Every blow, not the first one only.</b> Retail puts no flag var on this branch, so the help
	/// accrues for as long as the templar keeps swinging — which is the whole point of it.
	/// </summary>
	[Fact]
	public void EveryBlowNotTheFirstOneOnly()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(CaptainLakhara, 300f, 300f, 200f);
		Player templar = Templar(harness);
		BossAiHarness.MakeMutuallyKnown(boss, templar);

		int before = boss.GetAggroList().GetHate(templar);
		for (int i = 0; i < 3; i++)
			boss.GetAi().OnCreatureEvent(AiEventType.Attack, templar);

		Assert.Equal(66_000, boss.GetAggroList().GetHate(templar) - before);
	}

	/// <summary>
	/// <b>And only a templar.</b> The guard is a class test; every other class gets nothing extra, which
	/// is what makes this threat assistance rather than a damage bonus.
	/// </summary>
	[Theory]
	[InlineData(PlayerClass.GLADIATOR)]
	[InlineData(PlayerClass.CLERIC)]
	[InlineData(PlayerClass.SORCERER)]
	[InlineData(PlayerClass.ASSASSIN)]
	public void AndOnlyATemplar(PlayerClass other)
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(CaptainLakhara, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(305f, 300f, 200f);
		player.GetCommonData().SetPlayerClass(other);
		BossAiHarness.MakeMutuallyKnown(boss, player);

		Assert.Equal(0, OneBlowAdds(harness, boss, player));
	}
}
