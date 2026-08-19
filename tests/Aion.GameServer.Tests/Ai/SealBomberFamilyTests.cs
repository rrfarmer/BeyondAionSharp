using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The scene bombers, four of which were left as ordinary monsters.
/// </summary>
/// <remarks>
/// Retail binds one pattern, <c>IDSeal_Scene_13_Bomber</c>, to the bombers of scenes 13, 14 and 15 in
/// both factions — six npcs, one behaviour. This port gave the class to scene 13 and left scenes 14 and
/// 15 on <c>aggressive_no_loot</c>, so two thirds of the family walked up to a door and did nothing.
/// <para>
/// Found by <c>audit_odd_ai.py</c>, written after Yamennes' first gate turned out to be carrying a
/// teleporter's AI. <b>The split is by scene, not by faction</b> — which is why it reads as deliberate
/// until the devnames are lined up.
/// </para>
/// <para>
/// <b>Safe to extend because the class is position-based:</b> it looks for gate npcs within fifteen
/// metres of itself rather than naming a scene's door, so a scene-14 bomber acts on scene 14's door
/// without knowing anything about it.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SealBomberFamilyTests
{
	/// <summary>Every npc retail binds to the scene-13 bomber pattern, both factions, scenes 13–15.</summary>
	public static TheoryData<int> Bombers() => new TheoryData<int>
	{
		209711, 209715, 209719,   // Elyos, scenes 13, 14, 15
		209776, 209780, 209784,   // Asmodian, scenes 13, 14, 15
	};

	/// <summary>
	/// <b>Every bomber in the family destroys doors.</b>
	/// </summary>
	/// <remarks>
	/// A template pin rather than a behavioural one: what went wrong was the <c>ai</c> attribute, and a
	/// behavioural pin would need a door npc and a skill cast that never reaches a readable queue.
	/// </remarks>
	[Theory]
	[MemberData(nameof(Bombers))]
	public void EveryBomberInTheFamilyDestroysDoors(int npcId)
	{
		using BossAiHarness harness = BossAiHarness.For(301390000).WithWorldSize(256).Build();

		Assert.Equal("orissan_door_destroyer", DataManager.NPC_DATA.GetNpcTemplate(npcId)?.ai);
	}
}
