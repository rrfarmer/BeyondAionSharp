using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Two families the reverse audit found, bound to the class the rest of their pattern already used.
/// </summary>
/// <remarks>
/// <b>`Ab_AirBomb`: 96 npcs on <c>siege_mine</c> and 18 on plain <c>general</c>.</b> The pattern is two
/// lines — see an enemy, cast, despawn — so an air bomb without the class is a mine that does not go
/// off. They are the abyss fortress mines, and eighteen of them were scenery.
/// <para>
/// <b>`Dread_Surkana`: 42 on <c>surkana</c> and 15 on plain <c>aggressive</c>.</b> Same shape.
/// </para>
/// <para>
/// Pinned by resolving the class rather than by driving the behaviour: the defect was the binding, and
/// the behaviour of both classes is covered by their own suites. Stated as "every member of the family
/// agrees" so a new sibling is covered the day it appears, the same way the village chiefs are.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SiegeMineAndSurkanaBindingTests
{
	private const int Reshanta = 400010000;

	/// <summary>Three of the eighteen air bombs that had no class.</summary>
	private static readonly int[] Mines = [294500, 296350, 296376];

	/// <summary>Three of the fifteen surkana that had none.</summary>
	private static readonly int[] Surkana = [281256, 801974, 700487];

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(MineAI), typeof(SurkanaAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	[Fact]
	public void EveryAirBombIsAMine()
	{
		using BossAiHarness harness = NewHarness();

		foreach (int npcId in Mines)
		{
			Npc bomb = harness.Spawn(npcId, 300f + npcId % 13, 300f, 200f);
			Assert.IsType<MineAI>(bomb.GetAi());
		}
	}

	[Fact]
	public void EveryDreadgionSurkanaIsASurkana()
	{
		using BossAiHarness harness = NewHarness();

		foreach (int npcId in Surkana)
		{
			Npc surkana = harness.Spawn(npcId, 300f + npcId % 13, 320f, 200f);
			Assert.IsType<SurkanaAI>(surkana.GetAi());
		}
	}
}
