using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// That our tribe relations reach <c>IsEnemy</c>, for tribes checked against retail's own table.
/// </summary>
/// <remarks>
/// Tribe is the third place a mechanic can be missing, after the <c>ai</c> binding and the spawn point:
/// an npc with the right class, the right pattern and a spawn still does nothing if it is at war with
/// nobody. Two entries have now stopped at that wall.
/// <para>
/// <c>tools/client-extract/audit_tribe_relations.py</c> compares our file against the client's
/// <c>npc_tribe_relation.xml</c>. <b>Every tribe our npcs use is declared</b>; the 237 retail declares
/// and we do not are for npcs this port has no template for. These pins exist because the audit's first
/// answer was the opposite — its parser assumed every <c>&lt;tribe&gt;</c> had a closing tag, and 29 use
/// the self-closing form, so a non-greedy body swallowed the tribe after each one and hid it. It
/// reported 787 npcs with no relations and "added" 27 tribes the file already had.
/// </para>
/// <para>
/// Asserted through spawned npcs rather than <c>DataManager.TRIBE_RELATIONS_DATA</c>, because the
/// relation only matters if it survives into <c>IsEnemy</c> — which is what refused the hate in both
/// entries that ran into this wall.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TribeRelationsPortTests
{
	private const int Inggison = 210050000;

	/// <summary>A divine trico: <c>TRICON</c>, whose retail aggro list names both guard tribes.</summary>
	private const int Trico = 214471;

	/// <summary>A tigric fighter: <c>TAURIC</c>, same shape.</summary>
	private const int Tigric = 210971;

	/// <summary>"Oz", on <c>GUARD</c> — a tribe both files have always declared.</summary>
	private const int Guard = 203081;

	/// <summary><b>A monster tribe is at war with the guards retail's table names.</b></summary>
	[Fact]
	public void AMonsterTribeIsHostileToTheGuardsRetailNames()
	{
		using BossAiHarness harness = BossAiHarness.For(Inggison).WithWorldSize(4096)
			.WithAi(typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc trico = harness.Spawn(Trico, 300f, 300f, 200f);
		Npc tigric = harness.Spawn(Tigric, 305f, 300f, 200f);
		Npc guard = harness.Spawn(Guard, 310f, 300f, 200f);

		Assert.True(trico.IsEnemy(guard));
		Assert.True(tigric.IsEnemy(guard));
	}

	/// <summary>
	/// <b>And the two are not at war with each other.</b> Retail gives neither an aggro entry naming the
	/// other. This is the pin that would break if anyone "fixed" the undeclared-pair question by making
	/// everything undeclared hostile — which is the open question the village chiefs left behind.
	/// </summary>
	[Fact]
	public void AndTwoMonsterTribesAreNotAtWarWithEachOther()
	{
		using BossAiHarness harness = BossAiHarness.For(Inggison).WithWorldSize(4096)
			.WithAi(typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc trico = harness.Spawn(Trico, 300f, 300f, 200f);
		Npc tigric = harness.Spawn(Tigric, 305f, 300f, 200f);

		Assert.False(trico.IsEnemy(tigric));
	}
}
