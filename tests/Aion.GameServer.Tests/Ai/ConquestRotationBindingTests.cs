using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The conquest rotation monsters, whose second faction never got the class.
/// </summary>
/// <remarks>
/// <b>Retail's <c>F4_Rotation_Normal_Monster</c> binds 152 npcs and 48 of them ran the class.</b> The
/// hundred and four that did not are the same npcs with a <c>_D</c> on the end of the name —
/// <c>LF4_Rotation_Normal_01_01_65_An</c> against <c>LF4_Rotation_Normal_01_01_65_An_D</c> — so one
/// faction's rotation worked and the other's monsters died without closing the loop.
/// <para>
/// That matters because this class's death rung is the loop: it leaves a time-reset npc where the
/// monster fell, and that npc broadcasts the message re-arming the spawner's eight-minute clock. A
/// monster without the class kills the rotation it belongs to.
/// </para>
/// <para>
/// 212 npcs across the three rotation patterns, taking the class from 112 to 324.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ConquestRotationBindingTests
{
	private const int Inggison = 210050000;

	/// <summary>Three of the <c>_D</c> variants that had no class.</summary>
	private static readonly int[] Rebound = [236530, 236531, 236532];

	[Fact]
	public void BothFactionsRotationMonstersRunTheClass()
	{
		using BossAiHarness harness = BossAiHarness.For(Inggison).WithWorldSize(4096)
			.WithAi(typeof(ConquestOfferingAggressiveAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

		foreach (int npcId in Rebound)
		{
			Npc monster = harness.Spawn(npcId, 300f + npcId % 17, 300f, 200f);
			Assert.IsType<ConquestOfferingAggressiveAI>(monster.GetAi());
		}
	}
}
