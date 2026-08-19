using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Orissan's glacier crystals, which stayed in the room after he fell.
/// </summary>
/// <remarks>
/// Every one of his wake-state patterns ends <c>on_killed_by_user</c> and <c>on_killed_by_npc</c> with
/// <c>broadcast_message 22737</c> at range 100, and the crystals answer it by removing themselves.
/// <b>Nothing in this port sent it</b>, so they were left standing — live, hostile furniture in a room
/// the raid had already won.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class OrissanCrystalDismissTests
{
	private const int DrakenspireDepths = 301500000;

	/// <summary>Orissan half-woken at level 3, and two of the crystals he leaves about.</summary>
	private const int Orissan = 236234;
	private const int Crystal = 855699;
	private const int OtherCrystal = 855700;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DrakenspireDepths).WithWorldSize(2048)
			.WithAi(typeof(OrissanAI), typeof(OrissansSummonAI), typeof(AggressiveNpcAI),
				typeof(AggressiveNoLootNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Crystals(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == Crystal || n.GetNpcId() == OtherCrystal);

	/// <summary>
	/// <b>His death clears the crystals.</b>
	/// </summary>
	[Fact]
	public void HisDeathClearsTheCrystals()
	{
		using BossAiHarness harness = NewHarness();
		Npc orissan = harness.Spawn(Orissan, 500f, 500f, 200f);
		Npc first = harness.Spawn(Crystal, 505f, 500f, 200f);
		Npc second = harness.Spawn(OtherCrystal, 495f, 500f, 200f);
		BossAiHarness.MakeMutuallyKnown(orissan, first);
		BossAiHarness.MakeMutuallyKnown(orissan, second);
		Player raider = harness.SpawnPlayer(504f, 500f, 200f);
		Assert.Equal(2, Crystals(harness));

		BossAiHarness.Kill(orissan, raider);

		Assert.Equal(0, Crystals(harness));
	}

	/// <summary>
	/// <b>A crystal out of earshot is left alone.</b>
	/// </summary>
	/// <remarks>
	/// Retail gives the broadcast a hundred-metre range rather than making it global, and this pin is
	/// what keeps that a range rather than a formality: a translation using <c>float.MaxValue</c> would
	/// pass every other assertion here.
	/// </remarks>
	[Fact]
	public void ACrystalOutOfEarshotIsLeftAlone()
	{
		using BossAiHarness harness = NewHarness();
		Npc orissan = harness.Spawn(Orissan, 500f, 500f, 200f);
		Npc far = harness.Spawn(Crystal, 900f, 500f, 200f);
		BossAiHarness.MakeMutuallyKnown(orissan, far);
		Player raider = harness.SpawnPlayer(504f, 500f, 200f);

		BossAiHarness.Kill(orissan, raider);

		Assert.Equal(1, Crystals(harness));
	}
}
