using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="RangerGuardPetAI"/>, translated from the retail <c>BGuard_RhAPet*</c> family
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Twenty patterns, twenty-seven pets, one shape. What is pinned is the shape and that the trap is
/// the pet's own, since the level brackets are the only thing that differs.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class RangerGuardPetAiTests
{
	private const int Reshanta = 400010000;

	/// <summary>One pet and its trap, and another pair from a different level bracket.</summary>
	private const int Pet = 207824;
	private const int PetsTrap = 294740;
	private const int OtherPet = 295143;
	private const int OtherPetsTrap = 295142;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(RangerGuardPetAI), typeof(TrapNpcAI), typeof(AggressiveNpcAI)).Build();

	/// <summary>A pet nobody touches lays nothing — the whole thing hangs off being attacked.</summary>
	[Fact]
	public void AnUntouchedPetLaysNothing()
	{
		using BossAiHarness harness = NewHarness();
		Npc pet = harness.Spawn(Pet, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(60));

		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == PetsTrap));
		Assert.True(pet.IsSpawned(), "it should still be standing until something engages it");
	}

	/// <summary>Attack it and it lays its trap on you and is gone in the same breath.</summary>
	[Fact]
	public void AttackedItLaysItsTrapAndLeaves()
	{
		using BossAiHarness harness = NewHarness();
		Npc pet = harness.Spawn(Pet, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(pet, player);
		harness.Engage(pet, player);

		Npc trap = harness.LiveNpcs().First(n => n.GetNpcId() == PetsTrap);
		Assert.False(pet.IsSpawned(), "the pet leaves as soon as it has laid the trap");

		// On the player, not where the pet stood — twenty metres apart makes that unambiguous.
		Assert.True(Math.Abs(trap.GetX() - player.GetX()) < Math.Abs(trap.GetX() - 300f),
			$"the trap at {trap.GetX():F1} should be on the player at {player.GetX():F1}");
	}

	/// <summary>Each bracket lays its own trap, which is the only thing the twenty patterns differ in.</summary>
	[Fact]
	public void EachPetLaysItsOwnBracketsTrap()
	{
		using BossAiHarness harness = NewHarness();
		Npc pet = harness.Spawn(OtherPet, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(pet, player);
		harness.Engage(pet, player);

		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == OtherPetsTrap));
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == PetsTrap));
	}

	/// <summary>Walking away makes it leave rather than follow — it is furniture, not a fighter.</summary>
	[Fact]
	public void LeavingTheFightTakesThePetWithIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc pet = harness.Spawn(Pet, 300f, 300f, 200f);

		pet.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.False(pet.IsSpawned());
	}
}
