using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins the npcs that were running a stock AI while a sibling on the same retail pattern already had
/// a translated class. See <c>tools/client-extract/audit_orphan_siblings.py</c> and
/// <c>docs/retail-ai-fidelity.md</c>.
/// </summary>
/// <remarks>
/// Retail ships an encounter as several npc ids — a normal-mode boss and a hard-mode one, an Elyos
/// copy and an Asmodian one, three difficulty variants of one room — all bound to one pattern.
/// Translate one and the others keep whatever their template said. Three live HERO copies of
/// Macunbello were fighting as plain monsters next to a complete translation of their own pattern.
/// <para>
/// Written as a spawn rather than a string comparison on the template: it is the registration path
/// that matters, and a mistyped <c>ai=</c> name resolves to nothing rather than failing loudly.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class OrphanSiblingTests
{
	public static TheoryData<int, System.Type> Repointed => new()
	{
		// IDCT_Boss_LichKing — three live copies of Macunbello beside MacunbelloAI.
		{ 216734, typeof(MacunbelloAI) },
		{ 216735, typeof(MacunbelloAI) },
		{ 216737, typeof(MacunbelloAI) },

		// The Danuar frost summons: each pattern has a normal id and an 85xxxx hard-mode twin.
		{ 284662, typeof(DanuarSummonOrderAI) },
		{ 284663, typeof(DanuarSummonOrderAI) },
		{ 284664, typeof(DanuarSummonOrderAI) },
		{ 856496, typeof(DanuarSummonOrderAI) },
		{ 856497, typeof(DanuarSummonOrderAI) },
		{ 856498, typeof(DanuarSummonOrderAI) },

		// IDCT_UnDrakanPr and the Dreadgion priest.
		{ 216203, typeof(DrakanPriestAI) },
		{ 216284, typeof(DrakanPriestAI) },
		{ 233351, typeof(DrakanPriestAI) },

		// The frost dealer and tank, each with its hard-mode twin.
		{ 284660, typeof(DanuarFrostDealerAI) },
		{ 856494, typeof(DanuarFrostDealerAI) },
		{ 284659, typeof(DanuarFrostTankAI) },
		{ 856493, typeof(DanuarFrostTankAI) },

		// Two reian prisoners on `general` beside their own translated class.
		{ 799659, typeof(ImprisonedReianAI) },
		{ 799664, typeof(ImprisonedReianAI) },

		{ 281909, typeof(UnstablePazuzuWormAI) },
		{ 236287, typeof(VashartiAssassinAI) },
		{ 213774, typeof(NidalberBalaurAI) },
		{ 216296, typeof(MonolithicAmbusherAI) },
	};

	/// <summary>Each of these spawns under the class its own retail pattern was translated into.</summary>
	[Theory]
	[MemberData(nameof(Repointed))]
	public void ASiblingRunsItsEncountersClass(int npcId, System.Type expected)
	{
		using BossAiHarness harness = BossAiHarness.For(210040000).WithWorldSize(1024)
			.WithAi(expected, typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

		Npc npc = harness.Spawn(npcId, 300f, 300f, 200f);

		Assert.IsType(expected, npc.GetAi());
	}
}
