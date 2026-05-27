using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerDeathStateTransitionServiceTests
{
	[Fact]
	public void Apply_FlyingPlayerSetsFlyingBeforeDeathAndFloatingCorpseLikeJava()
	{
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			CreatureState = PlayerCreatureState.Active | PlayerCreatureState.Flying | PlayerCreatureState.Gliding | PlayerCreatureState.Resting,
			FlyState = PlayerFlyState.Flying | PlayerFlyState.Gliding,
			IsInRideMode = true,
			RideInfo = new PlayerRideInfo(1, 2, 3, 4, 5, 6),
		};

		var result = PlayerDeathStateTransitionService.Apply(player);

		Assert.Equal(PlayerDeathStateTransitionStatus.FloatingCorpseApplied, result.Status);
		Assert.True(result.WasFlyingAtDeath);
		Assert.False(result.PreviousFlyingBeforeDeath);
		Assert.True(result.CurrentFlyingBeforeDeath);
		Assert.True(player.IsFlyingBeforeDeath);
		Assert.False(player.IsInRideMode);
		Assert.Null(player.RideInfo);
		Assert.False(player.IsInState(PlayerCreatureState.Active));
		Assert.False(player.IsInState(PlayerCreatureState.Flying));
		Assert.False(player.IsInState(PlayerCreatureState.Gliding));
		Assert.False(player.IsInState(PlayerCreatureState.Resting));
		Assert.True(player.IsInState(PlayerCreatureState.FloatingCorpse));
		Assert.False(player.IsInState(PlayerCreatureState.Dead));
		Assert.False(player.IsInFlyingState());
		Assert.False(player.IsInGlidingState());
		Assert.Contains(PlayerDeathStateTransitionStep.SetFlyingBeforeDeathFlag, result.Steps);
		Assert.Contains(PlayerDeathStateTransitionStep.SetFloatingCorpseState, result.Steps);
		var playerControllerPhase = Assert.Single(result.PhasePlans, phase => phase.Phase == PlayerDeathStateTransitionPhase.PlayerControllerPreSuperCleanup);
		var creatureControllerPhase = Assert.Single(result.PhasePlans, phase => phase.Phase == PlayerDeathStateTransitionPhase.CreatureControllerDeathStateSelection);
		AssertOrdered(
			playerControllerPhase.Steps,
			PlayerDeathStateTransitionStep.CheckFlyingBeforeDeath,
			PlayerDeathStateTransitionStep.SetFlyingBeforeDeathFlag,
			PlayerDeathStateTransitionStep.ClearRideAndRestingState,
			PlayerDeathStateTransitionStep.ClearExistingFloatingCorpseState,
			PlayerDeathStateTransitionStep.ClearFlyingAndGlidingCreatureState,
			PlayerDeathStateTransitionStep.ClearFlyingAndGlidingFlyState);
		AssertOrdered(
			creatureControllerPhase.Steps,
			PlayerDeathStateTransitionStep.ClearActiveState,
			PlayerDeathStateTransitionStep.SetFloatingCorpseState);
		Assert.Contains("PlayerController.onDie", playerControllerPhase.JavaSource);
		Assert.Contains("CreatureController.onDie", creatureControllerPhase.JavaSource);
		Assert.Contains("CreatureController.onDie", result.JavaSource);
		Assert.True(result.IsLive);
	}

	[Fact]
	public void Apply_NonFlyingPlayerSetsDeadState()
	{
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			CreatureState = PlayerCreatureState.Active | PlayerCreatureState.WalkMode,
			FlyState = PlayerFlyState.None,
		};

		var result = PlayerDeathStateTransitionService.Apply(player);

		Assert.Equal(PlayerDeathStateTransitionStatus.DeadStateApplied, result.Status);
		Assert.False(result.WasFlyingAtDeath);
		Assert.False(player.IsFlyingBeforeDeath);
		Assert.True(player.IsInState(PlayerCreatureState.Dead));
		Assert.True(player.IsInState(PlayerCreatureState.WalkMode));
		Assert.False(player.IsInState(PlayerCreatureState.FloatingCorpse));
		Assert.DoesNotContain(PlayerDeathStateTransitionStep.SetFlyingBeforeDeathFlag, result.Steps);
		Assert.Contains(PlayerDeathStateTransitionStep.SetDeadState, result.Steps);
		var playerControllerPhase = Assert.Single(result.PhasePlans, phase => phase.Phase == PlayerDeathStateTransitionPhase.PlayerControllerPreSuperCleanup);
		var creatureControllerPhase = Assert.Single(result.PhasePlans, phase => phase.Phase == PlayerDeathStateTransitionPhase.CreatureControllerDeathStateSelection);
		Assert.DoesNotContain(PlayerDeathStateTransitionStep.SetFlyingBeforeDeathFlag, playerControllerPhase.Steps);
		Assert.Equal(new[] { PlayerDeathStateTransitionStep.SetDeadState }, creatureControllerPhase.Steps);
	}

	[Fact]
	public void Apply_PreviouslyFlyingBeforeDeathUsesFloatingCorpseEvenAfterFlyingStateWasCleared()
	{
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			CreatureState = PlayerCreatureState.Active | PlayerCreatureState.WalkMode,
			IsFlyingBeforeDeath = true,
		};

		var result = PlayerDeathStateTransitionService.Apply(player);

		Assert.Equal(PlayerDeathStateTransitionStatus.FloatingCorpseApplied, result.Status);
		Assert.False(result.WasFlyingAtDeath);
		Assert.True(result.PreviousFlyingBeforeDeath);
		Assert.True(result.CurrentFlyingBeforeDeath);
		Assert.False(player.IsInState(PlayerCreatureState.Active));
		Assert.True(player.IsInState(PlayerCreatureState.FloatingCorpse));
		Assert.True(player.IsInState(PlayerCreatureState.WalkMode));
		Assert.DoesNotContain(PlayerDeathStateTransitionStep.SetFlyingBeforeDeathFlag, result.Steps);
		var creatureControllerPhase = Assert.Single(result.PhasePlans, phase => phase.Phase == PlayerDeathStateTransitionPhase.CreatureControllerDeathStateSelection);
		Assert.Equal(
			new[] { PlayerDeathStateTransitionStep.ClearActiveState, PlayerDeathStateTransitionStep.SetFloatingCorpseState },
			creatureControllerPhase.Steps);
	}

	private static void AssertOrdered(IReadOnlyList<PlayerDeathStateTransitionStep> actual, params PlayerDeathStateTransitionStep[] expected)
	{
		var previousIndex = -1;
		foreach (var step in expected)
		{
			var currentIndex = Array.IndexOf(actual.ToArray(), step);
			Assert.True(currentIndex > previousIndex, $"Expected {step} after index {previousIndex}, actual order: {string.Join(", ", actual)}");
			previousIndex = currentIndex;
		}
	}

	private const int PlayerObjectId = 1001;
}
