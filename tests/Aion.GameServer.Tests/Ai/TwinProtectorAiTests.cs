using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="TwinProtectorAI"/>'s hellfire field, translated from retail patterns
/// <c>IDSeal_Twin_P</c>, <c>_P_Failed</c>, <c>IDSeal_Twin_M</c> and <c>_M_Failed</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Found by the shared-<c>ai_name</c> audit: all four protectors share the class, the two sides'
/// patterns name different NPCs, and neither field was ever placed.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TwinProtectorAiTests
{
	/// <summary>The Seal of Destruction.</summary>
	private const int SealOfDestruction = 301300000;

	private const int LavaProtector = 236227;
	private const int HeatventProtector = 236228;
	private const int FountlessLavaProtector = 236225;

	private const int LavaField = 855626;
	private const int HeatventField = 855712;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(SealOfDestruction).WithWorldSize(2048)
			.WithAi(typeof(TwinProtectorAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI),
				typeof(AggressiveNoLootNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Each side's field is chosen by the same parity the adds already used.</summary>
	[Fact]
	public void EachSideOpensWithItsOwnField()
	{
		Assert.Equal(LavaField, TwinProtectorAI.FieldFor(LavaProtector));
		Assert.Equal(LavaField, TwinProtectorAI.FieldFor(FountlessLavaProtector));
		Assert.Equal(HeatventField, TwinProtectorAI.FieldFor(HeatventProtector));
	}

	/// <summary>The lava protector puts its field on the lava mark as it wakes.</summary>
	[Fact]
	public void TheLavaProtectorPlacesItsFieldOnWaking()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(LavaProtector, 520f, 200f, 1682f);

		Npc field = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == LavaField));
		Assert.Equal(530.5f, field.GetX(), 1);
		Assert.Equal(212f, field.GetY(), 1);
		Assert.Equal(0, Count(harness, HeatventField));
	}

	/// <summary>And the heatvent one puts a different NPC on a different mark.</summary>
	[Fact]
	public void TheHeatventProtectorPlacesTheOtherField()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(HeatventProtector, 520f, 200f, 1682f);

		Npc field = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == HeatventField));
		Assert.Equal(531.4f, field.GetX(), 1);
		Assert.Equal(151f, field.GetY(), 1);
		Assert.Equal(0, Count(harness, LavaField));
	}

	/// <summary>The failed variant opens with one too — all four patterns have the branch.</summary>
	[Fact]
	public void TheFountlessVariantPlacesOneAsWell()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(FountlessLavaProtector, 520f, 200f, 1682f);

		Assert.Equal(1, Count(harness, LavaField));
	}

	/// <summary>
	/// Killing the protector clears it. This class cleared its spawns on despawning and on going
	/// home, but not on dying, where retail clears both groups.
	/// </summary>
	[Fact]
	public void DyingClearsTheField()
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(LavaProtector, 520f, 200f, 1682f);
		Assert.Equal(1, Count(harness, LavaField));

		protector.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Count(harness, LavaField));
	}
}
