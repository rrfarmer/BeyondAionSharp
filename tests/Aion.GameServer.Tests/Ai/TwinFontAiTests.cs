using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="TwinFontAI"/> and <see cref="TwinFailureDisplayAI"/>, translated from retail
/// patterns <c>IDSeal_Twin_P_Source</c>, <c>IDSeal_Twin_M_Source</c> and their
/// <c>_Change_Failed</c> announcers (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The time-over rescue: fail to kill the second twin inside fifteen seconds and your own side's
/// detachment arrives and destroys the font for you. The audit's <c>no speaker</c> verdict found it —
/// both halves were portable and the announcer was in no spawn file.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TwinFontAiTests
{
	private const int SealOfDestruction = 301300000;

	private const int LavaFont = 855708;
	private const int HeatventFont = 855709;
	private const int PhysicalDisplay = 855510;
	private const int MagicalDisplay = 855511;

	private const int ElyosLeader = 209688;
	private const int ElyosSoldier = 209689;
	private const int AsmodianLeader = 209753;
	private const int AsmodianSoldier = 209754;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(SealOfDestruction).WithWorldSize(2048)
			.WithAi(typeof(TwinFontAI), typeof(TwinFailureDisplayAI), typeof(AggressiveNpcAI),
				typeof(AggressiveNoLootNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Each font answers its own side's time-over, and an unlisted npc answers none.</summary>
	[Theory]
	[InlineData(LavaFont, TwinFontAI.PhysicalTimeOver)]
	[InlineData(HeatventFont, TwinFontAI.MagicalTimeOver)]
	[InlineData(123456, 0)]
	public void EachFontAnswersItsOwnTimeOver(int npcId, int expected)
	{
		Assert.Equal(expected, TwinFontAI.TimeOverFor(npcId));
	}

	/// <summary>And each display announces the side it belongs to.</summary>
	[Theory]
	[InlineData(PhysicalDisplay, TwinFontAI.PhysicalTimeOver)]
	[InlineData(MagicalDisplay, TwinFontAI.MagicalTimeOver)]
	[InlineData(856403, TwinFontAI.PhysicalTimeOver)]
	[InlineData(856404, TwinFontAI.MagicalTimeOver)]
	[InlineData(123456, 0)]
	public void EachDisplayAnnouncesItsOwnSide(int npcId, int expected)
	{
		Assert.Equal(expected, TwinFailureDisplayAI.AnnouncementFor(npcId));
	}

	/// <summary>
	/// <b>The detachment is the raid's, not the boss's.</b> Retail splits the branch on
	/// <c>is_race</c> and ships two of everything.
	/// </summary>
	[Theory]
	[InlineData(Race.ELYOS, ElyosLeader, ElyosSoldier)]
	[InlineData(Race.ASMODIANS, AsmodianLeader, AsmodianSoldier)]
	public void EachRaceGetsItsOwnDetachment(Race race, int leader, int soldier)
	{
		Assert.Equal((leader, soldier), TwinFontAI.DetachmentFor(race));
	}

	/// <summary>
	/// The whole chain: the display announces, and the font calls two soldiers and their leader down
	/// onto itself — all three already fighting it.
	/// </summary>
	[Fact]
	public void TheFontCallsThreeGuardsOntoItself()
	{
		using BossAiHarness harness = NewHarness();
		Npc font = harness.Spawn(HeatventFont, 520f, 200f, 1682f);
		harness.SpawnPlayer(524f, 200f, 1682f);
		Npc display = harness.Spawn(MagicalDisplay, 522f, 200f, 1682f);
		BossAiHarness.MakeMutuallyKnown(display, font);

		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(2, Count(harness, ElyosSoldier));
		Assert.Equal(1, Count(harness, ElyosLeader));

		foreach (Npc guard in harness.LiveNpcs().Where(n => n.GetNpcId() == ElyosSoldier))
			Assert.Same(font, guard.GetTarget());
	}

	/// <summary>
	/// <b>Once, however long the display keeps announcing.</b> It repeats every three seconds until
	/// dismissed, so without the flag a failed raid drowns in guards.
	/// </summary>
	[Fact]
	public void TheGuardsComeOnceHoweverOftenItIsAnnounced()
	{
		using BossAiHarness harness = NewHarness();
		Npc font = harness.Spawn(HeatventFont, 520f, 200f, 1682f);
		harness.SpawnPlayer(524f, 200f, 1682f);
		Npc display = harness.Spawn(MagicalDisplay, 522f, 200f, 1682f);
		BossAiHarness.MakeMutuallyKnown(display, font);

		harness.Clock.Advance(TimeSpan.FromSeconds(12));

		Assert.Equal(2, Count(harness, ElyosSoldier));
		Assert.Equal(1, Count(harness, ElyosLeader));
	}

	/// <summary>
	/// The wrong side's display does not move a font. Both stand in the same room when both twins
	/// have fallen, so the pairing has to be by side rather than by proximity.
	/// </summary>
	[Fact]
	public void APhysicalDisplayDoesNotCallTheHeatventFontsGuards()
	{
		using BossAiHarness harness = NewHarness();
		Npc font = harness.Spawn(HeatventFont, 520f, 200f, 1682f);
		harness.SpawnPlayer(524f, 200f, 1682f);
		Npc display = harness.Spawn(PhysicalDisplay, 522f, 200f, 1682f);
		BossAiHarness.MakeMutuallyKnown(display, font);

		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Assert.Equal(0, Count(harness, ElyosSoldier));
	}

	/// <summary>
	/// The display stops itself. Retail's dismissal is a message from NPCs this work has not reached,
	/// so it is given the twenty seconds its font needs — see the class remarks.
	/// </summary>
	[Fact]
	public void TheDisplayStopsAnnouncingAfterTwentySeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc display = harness.Spawn(MagicalDisplay, 522f, 200f, 1682f);

		harness.Clock.Advance(TimeSpan.FromSeconds(19));
		Assert.True(display.IsSpawned());

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.False(display.IsSpawned());
	}
}
