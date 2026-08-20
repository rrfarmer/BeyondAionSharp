using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Kaldor's nineteen village chiefs come in three race variants, and all three are the same npc.
/// </summary>
/// <remarks>
/// Retail names them <c>LDF5_Village_chiefNN_L</c>, <c>_D</c> and <c>_DR</c> — Elyos-held, Asmodian-held
/// and balaur-held versions of one village's chief, all bound to the same retail pattern. **The balaur
/// variant of every one of the nineteen ran plain <c>aggressive</c>** while its two siblings ran
/// <c>simple_abyssguard</c>: a village whose chief behaves differently depending on who holds it.
/// <para>
/// <b>This pin exists because the audit that found it pointed at the wrong class.</b>
/// <c>audit_odd_ai.py --reverse</c> compares an npc against every sibling on its retail pattern, and
/// that pattern also carries two other trios — <c>LDF5_chief_vNN_*</c> and
/// <c>LDF5_Fortress_Chief_VNN_*</c> — which run <c>base_protector</c>. Six of nine siblings therefore
/// said <c>base_protector</c>, and binding to the majority would have given the balaur chief a class its
/// own two race-variants do not have.
/// </para>
/// <para>
/// The invariant that actually holds is the narrower one pinned here: <b>the three race variants of one
/// village chief agree with each other</b>. It is stated as a rule rather than as nineteen ids so that a
/// twentieth village, or a fourth variant, is covered the day it appears.
/// </para>
/// <para>
/// <b>None of the fifty-seven has a spawn point,</b> in Kaldor or anywhere else — they are part of the
/// 289 that map owes, recorded in the world-spawn sweep. The binding is correct and currently
/// unreachable, which is why this pin asks the template loader rather than the harness: it is the
/// binding that was wrong, and the binding is what can be checked today.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class VillageChiefBindingTests
{
	/// <summary>Retail's own naming: the Elyos-held, Asmodian-held and balaur-held chief.</summary>
	private static readonly string[] Variants = ["L", "D", "DR"];

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(600090000).WithWorldSize(4096)
			.WithAi(typeof(AbyssGuardSimpleAI), typeof(BaseProtectorAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	[Fact]
	public void EveryVillageChiefAgreesWithItsOwnRaceVariants()
	{
		using BossAiHarness harness = NewHarness();
		var disagreed = new List<string>();

		for (int village = 1; village <= 19; village++)
		{
			string? first = null;
			foreach (string variant in Variants)
			{
				Npc chief = harness.Spawn(ChiefIds.For(village, variant),
					300f + village, 300f, 200f);
				string ai = chief.GetAi().GetType().Name;

				first ??= ai;
				if (ai != first)
					disagreed.Add($"village {village:00}: {variant} is {ai}, but L is {first}");
			}
		}

		Assert.Empty(disagreed);
	}

	/// <summary>
	/// <b>And they agree on the class their behaviour needs</b>, not merely with each other — three
	/// variants all left on <c>aggressive</c> would satisfy the pin above and still be wrong.
	/// </summary>
	[Fact]
	public void AndTheyAgreeOnTheClassTheirBehaviourNeeds()
	{
		using BossAiHarness harness = NewHarness();

		for (int village = 1; village <= 19; village++)
		{
			Npc chief = harness.Spawn(ChiefIds.For(village, "DR"), 300f + village, 320f, 200f);
			Assert.IsType<AbyssGuardSimpleAI>(chief.GetAi());
		}
	}

	/// <summary>
	/// The ids, in retail's own order. Kept as a table rather than derived, because the numbering is
	/// only <em>mostly</em> regular and a derived id that lands on the wrong npc would pass quietly.
	/// </summary>
	private static class ChiefIds
	{
		private static readonly int[] Elyos =
		[
			277069, 277072, 277075, 277078, 277081, 277084, 277087, 277090, 277093, 277096,
			277099, 277102, 277105, 277108, 277111, 277114, 277117, 277120, 277123,
		];

		public static int For(int village, string variant) => variant switch
		{
			"L" => Elyos[village - 1],
			"D" => Elyos[village - 1] + 1,
			_ => Elyos[village - 1] + 2,
		};
	}
}
