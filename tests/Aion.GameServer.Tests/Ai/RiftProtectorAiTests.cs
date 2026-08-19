using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The eight guardian heads that watch the level-65 rifts, two of which were never wired up.
/// </summary>
/// <remarks>
/// All eight run one retail pattern, <c>LF5_StrongGuard_Li_Boss_65_Af</c> — four Dark and four Light,
/// one per rift — and <b>one of each four was left on <c>aggressive</c></b>: Captain Wigthor (219874)
/// and the First Ironlightian Deity General (236022). The set is otherwise perfectly symmetric, which
/// is what makes the two stand out once anything looks.
/// <para>
/// <c>RiftProtectorAI</c> does one thing: it holds these bosses at <b>a tenth</b> of the health their
/// template gives them. So the two on <c>aggressive</c> were not merely missing a mechanic — they were
/// standing at their rifts with <b>ten times</b> the health the other six have.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class RiftProtectorAiTests
{
	private const int Inggison = 210050000;

	/// <summary>The six that were bound, and the two that were not.</summary>
	public static TheoryData<int> EveryGuardianHead => new TheoryData<int>
	{
		219887, 219906, 219913, 236035, 236048, 236055,
		219874, 236022,
	};

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Inggison).WithWorldSize(2048)
			.WithAi(typeof(RiftProtectorAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>Every one of the eight stands at a tenth of its template health.</b>
	/// </summary>
	/// <remarks>
	/// Written across all eight rather than only the two that changed, because the thing worth holding
	/// is that the set stays symmetric — the next npc added to this pattern should fail here if it is
	/// left off the template binding, which is exactly how these two survived.
	/// </remarks>
	[Theory]
	[MemberData(nameof(EveryGuardianHead))]
	public void EveryGuardianHeadKeepsATenthOfItsTemplateHealth(int npcId)
	{
		using BossAiHarness harness = NewHarness();
		Npc head = harness.Spawn(npcId, 300f, 300f, 200f);

		int template = head.GetObjectTemplate().GetStatsTemplate().GetMaxHp();
		int actual = head.GetLifeStats().GetMaxHp();

		Assert.True(actual < template,
			$"{npcId} stands at its full template health ({actual}), so it is not on rift_protector");
		Assert.InRange(actual, (int)(template * 0.09f), (int)(template * 0.11f) + 1);
	}
}
