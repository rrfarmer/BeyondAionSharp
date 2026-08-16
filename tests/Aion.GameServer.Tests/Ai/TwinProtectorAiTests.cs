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

	/// <summary><c>BIDSeal_Twin_M_Sum_Tornado</c> — the heatvent side's wave, and only its.</summary>
	private const int HeatventWave = 855625;

	/// <summary><c>BIDSeal_Twin_P_Sum_65_Ae</c> — the lava side's.</summary>
	private const int LavaWave = 855621;

	/// <summary>Retail's <c>hatepoints_to_add</c> on every <c>spawn_on_multi_target</c> branch.</summary>
	private const int OnArrival = 1000;

	/// <summary>The cast the wave hangs off in this class.</summary>
	private const int RagingHellfire = 21644;

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
	/// <b>The hellfire wave is the side's own, and it was not.</b> Both of the heatvent pattern's
	/// <c>spawn_on_multi_target</c> branches call <c>BIDSeal_Twin_M_Sum_Tornado</c> and both of the
	/// lava pattern's call <c>BIDSeal_Twin_P_Sum_65_Ae</c>; this class had the tornado hardcoded for
	/// all four protectors, so the lava side summoned the other side's wave.
	/// </summary>
	[Fact]
	public void EachSideCallsItsOwnWave()
	{
		Assert.Equal(HeatventWave, TwinProtectorAI.WaveFor(HeatventProtector));
		Assert.Equal(LavaWave, TwinProtectorAI.WaveFor(LavaProtector));
		Assert.Equal(LavaWave, TwinProtectorAI.WaveFor(FountlessLavaProtector));
	}

	/// <summary>
	/// Stated separately because it is the bug rather than a consequence of it: the tornado is a
	/// heatvent NPC and no lava protector should ever produce one.
	/// </summary>
	[Fact]
	public void NoLavaProtectorCallsTheTornado()
	{
		Assert.NotEqual(HeatventWave, TwinProtectorAI.WaveFor(LavaProtector));
		Assert.NotEqual(HeatventWave, TwinProtectorAI.WaveFor(FountlessLavaProtector));
	}

	/// <summary>
	/// <b>A wave arrives already fighting whoever it landed on.</b> Retail carries
	/// <c>hatepoints_to_add=1000</c> on every one of those branches; without it the adds stand where
	/// they were put until someone walks into them, which is a materially easier fight.
	/// </summary>
	[Fact]
	public void AWaveArrivesAlreadyFightingItsTarget()
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(HeatventProtector, 520f, 200f, 1682f);
		var player = harness.SpawnPlayer(524f, 200f, 1682f);
		harness.Engage(protector, player);

		// Drive the hellfire cast the wave hangs off, as the phase ladder does.
		protector.GetAi().OnEndUseSkill(
			Aion.GameServer.Dataholders.DataManager.SKILL_DATA.GetSkillTemplate(RagingHellfire), 1);

		Npc wave = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == HeatventWave));
		Assert.Equal(OnArrival, wave.GetAggroList().GetHate(player));
		Assert.Same(player, wave.GetTarget());
	}

	/// <summary>
	/// <b>The live half of the side pin, and it is the one that matters.</b> Asserting
	/// <see cref="TwinProtectorAI.WaveFor"/> alone passes while the call site still hardcodes the
	/// tornado — putting the shipped bug back survived a mutation sweep against every other pin here.
	/// This drives a lava protector's hellfire and looks at what actually appears.
	/// </summary>
	[Theory]
	[InlineData(LavaProtector, LavaWave, HeatventWave)]
	[InlineData(FountlessLavaProtector, LavaWave, HeatventWave)]
	[InlineData(HeatventProtector, HeatventWave, LavaWave)]
	public void TheWaveThatAppearsIsThisSidesOwn(int protectorId, int expected, int theOtherSides)
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(protectorId, 520f, 200f, 1682f);
		var player = harness.SpawnPlayer(524f, 200f, 1682f);
		harness.Engage(protector, player);

		protector.GetAi().OnEndUseSkill(
			Aion.GameServer.Dataholders.DataManager.SKILL_DATA.GetSkillTemplate(RagingHellfire), 1);

		Assert.Equal(1, Count(harness, expected));
		Assert.Equal(0, Count(harness, theOtherSides));
	}

	/// <summary>
	/// The phase ladder's own wave is side-split too, and was already right — but nothing guarded it,
	/// so flattening it survived a mutation sweep. Pinned here for the same reason as the hellfire
	/// wave: in a two-sided fight every summon is a place a hardcoded id can hide.
	/// </summary>
	[Theory]
	[InlineData(HeatventProtector, 855622, 855621)]
	[InlineData(LavaProtector, 855621, 855622)]
	public void ThePhaseLaddersWaveIsThisSidesOwn(int protectorId, int expected, int theOtherSides)
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(protectorId, 520f, 200f, 1682f);
		var player = harness.SpawnPlayer(524f, 200f, 1682f);
		harness.Engage(protector, player);

		// The ladder advances one phase per swing, so 65 and 40 come first and 25 is the third.
		BossAiHarness.SetExactPercent(protector, 25);
		for (int i = 0; i < 3; i++)
			protector.GetAi().OnCreatureEvent(AiEventType.Attack, player);

		Assert.True(Count(harness, expected) > 0, "this side's wave should have arrived");
		Assert.Equal(0, Count(harness, theOtherSides));
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
