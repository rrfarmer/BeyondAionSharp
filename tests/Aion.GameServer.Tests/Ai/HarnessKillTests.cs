using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// That <see cref="BossAiHarness.Kill"/> actually reaches the dying NPC's death handling.
/// </summary>
/// <remarks>
/// For a long time it did not, and nothing said so. <c>NpcController.OnDie</c> calls <c>DoReward()</c>
/// before it raises <c>AiEventType.Died</c>, inside a <c>try</c> whose <c>catch</c> only logs; <c>Kill</c>
/// recorded the killer's damage first, so the reward path ran for real, walked into the XP table, the
/// drop registry and the housing service — none of which this harness stands up — threw, and was
/// swallowed **along with the death event**.
/// <para>
/// The effect was that <b>every <c>on_die</c> branch of every pattern class, and every hand-written
/// <c>HandleDied</c>, was unreachable from a test</b>, while <c>Kill</c>'s own documentation said the AI
/// event ran in server order. It was found only because a new encounter's death spawn would not appear,
/// and confirmed by reproducing it against a class written months earlier.
/// </para>
/// <para>
/// This pin exists so it cannot come back quietly. It deliberately uses
/// <see cref="QueenAlukinaAI"/> — a hand-written <c>HandleDied</c> that predates the pattern DSL — so it
/// covers the seam rather than one class's translation.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class HarnessKillTests
{
	private const int EmpyreanCrucible = 300300000;

	/// <summary>Queen Alukina of the Crucible, whose seven blobbles burst from <c>HandleDied</c>.</summary>
	private const int AlukinaEmp = 217590;
	private const int AzureBlobble = 280713;
	private const int BlobblesOnDeath = 7;

	/// <summary>
	/// <b>Killing an NPC runs its death handling.</b>
	/// </summary>
	[Fact]
	public void KillingAnNpcRunsItsDeathHandling()
	{
		using BossAiHarness harness = BossAiHarness.For(EmpyreanCrucible).WithWorldSize(2048)
			.WithAi(typeof(QueenAlukinaAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc queen = harness.Spawn(AlukinaEmp, 400f, 400f, 200f);
		Player player = harness.SpawnPlayer(404f, 400f, 200f);
		harness.Engage(queen, player);

		BossAiHarness.Kill(queen, player);

		Assert.Equal(BlobblesOnDeath, harness.LiveNpcs().Count(n => n.GetNpcId() == AzureBlobble));
	}

	/// <summary>
	/// <b>And the NPC is actually dead afterwards.</b>
	/// </summary>
	/// <remarks>
	/// The pin above would still pass if <c>Kill</c> raised the AI event and did nothing else, which is
	/// precisely the shortcut that was rejected when fixing this — it would have made death branches
	/// testable while quietly making <c>Kill</c> a lie.
	/// </remarks>
	[Fact]
	public void AndTheNpcIsDeadAfterwards()
	{
		using BossAiHarness harness = BossAiHarness.For(EmpyreanCrucible).WithWorldSize(2048)
			.WithAi(typeof(QueenAlukinaAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc queen = harness.Spawn(AlukinaEmp, 400f, 400f, 200f);
		Player player = harness.SpawnPlayer(404f, 400f, 200f);
		harness.Engage(queen, player);

		BossAiHarness.Kill(queen, player);

		Assert.True(queen.IsDead(), "Kill left the NPC alive");
		Assert.Equal(0, queen.GetLifeStats().GetCurrentHp());
	}
}
