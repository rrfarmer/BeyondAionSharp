using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the npc health that was a placeholder (see docs/retail-ai-fidelity.md).
/// </summary>
/// <remarks>
/// <b>4,241 npcs carried 200 HP or less where retail gives them a real pool</b>, and the worst of them
/// were not small: a fortress guardian head at 128 against retail's 290,150,400, fortress doors at 113
/// against 168,000,000. A siege against a boss with 128 HP is over before it starts.
/// <para>
/// It surfaced by accident — a fortress killer kept dying inside the first tick of a pin that was
/// trying to measure something else entirely, and the reason turned out to be 140 max HP against a
/// garrison chief's 32,215.
/// </para>
/// <para>
/// These pins name a handful of the worst by id. They are not a substitute for
/// <c>audit_npc_health.py</c>, which compares all 62,592 — they are here so a future import that
/// re-flattens the same values fails loudly instead of quietly making every abyss boss killable in one
/// hit.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class NpcHealthTests
{
	private const int Reshanta = 400010000;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(FortressKillerAI), typeof(ArtifactProtectorAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>An abyss boss has a boss's health.</b> Retail's own <c>max_hp</c> for each, out of
	/// <c>npcs.xml</c>.
	/// </summary>
	/// <remarks>
	/// <b>Compared proportionally, not exactly.</b> A guardian chief written as 156,553,560 arrives as
	/// 156,553,568 — eight out in a hundred and fifty-six million, which is the stat pipeline's float
	/// arithmetic and not the data. An exact assertion here would be pinning the rounding.
	/// </remarks>
	[Theory]
	[InlineData(855261, 290_150_400)]   // BLDF5_Fortress_GuardianHead
	[InlineData(297270, 156_553_560)]   // BGAB1_LGuardianChief
	[InlineData(297354, 168_000_000)]   // BGAB1_Door_Li_4_lv65_BigHP
	[InlineData(251160, 3_377_604)]     // AB1_DrGuard_Artifact_Killer
	[InlineData(235543, 327_127)]       // LDF4_Advance_SS_A_Killer_Dr_01
	public void AnAbyssBossHasABossHealth(int npcId, int expected)
	{
		using BossAiHarness harness = NewHarness();
		Npc npc = harness.Spawn(npcId, 300f, 300f, 200f);

		int actual = npc.GetLifeStats().GetMaxHp();

		Assert.InRange(actual, (int)(expected * 0.999), (int)(expected * 1.001));
	}

	/// <summary>
	/// <b>And it is not a token.</b> The placeholders clustered at a hundred-odd; nothing of this rank
	/// should be within a thousand of that, and the arithmetic says so without naming a number twice.
	/// </summary>
	[Fact]
	public void AndItIsNotAToken()
	{
		using BossAiHarness harness = NewHarness();

		foreach (int npcId in new[] { 855261, 297270, 297354, 251160, 235543 })
		{
			Npc npc = harness.Spawn(npcId, 300f + npcId % 11, 300f, 200f);
			Assert.True(npc.GetLifeStats().GetMaxHp() > 1000,
				$"npc {npcId} is back to a placeholder health of {npc.GetLifeStats().GetMaxHp()}");
		}
	}
}
