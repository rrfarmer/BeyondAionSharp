using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum PlayerDeathStateTransitionStatus
{
	DeadStateApplied,
	FloatingCorpseApplied,
}

public enum PlayerDeathStateTransitionStep
{
	CheckFlyingBeforeDeath,
	SetFlyingBeforeDeathFlag,
	ClearRideAndRestingState,
	ClearExistingFloatingCorpseState,
	ClearFlyingAndGlidingCreatureState,
	ClearFlyingAndGlidingFlyState,
	ClearActiveState,
	SetFloatingCorpseState,
	SetDeadState,
}

public enum PlayerDeathStateTransitionPhase
{
	PlayerControllerPreSuperCleanup,
	CreatureControllerDeathStateSelection,
}

public sealed record PlayerDeathStateTransitionPhasePlan(
	PlayerDeathStateTransitionPhase Phase,
	IReadOnlyList<PlayerDeathStateTransitionStep> Steps,
	string JavaSource
);

public sealed record PlayerDeathStateTransitionResult(
	PlayerDeathStateTransitionStatus Status,
	int PlayerObjectId,
	PlayerCreatureState PreviousCreatureState,
	PlayerCreatureState CurrentCreatureState,
	PlayerFlyState PreviousFlyState,
	PlayerFlyState CurrentFlyState,
	bool PreviousFlyingBeforeDeath,
	bool CurrentFlyingBeforeDeath,
	bool WasFlyingAtDeath,
	IReadOnlyList<PlayerDeathStateTransitionStep> Steps,
	IReadOnlyList<PlayerDeathStateTransitionPhasePlan> PhasePlans,
	string JavaSource,
	bool IsLive
);

public static class PlayerDeathStateTransitionService
{
	public static IReadOnlyList<PlayerDeathStateTransitionPhasePlan> CreatePhasePlans(Player player)
	{
		var wasFlyingAtDeath = player.IsInState(PlayerCreatureState.Flying);
		var wouldUseFloatingCorpse = player.IsFlyingBeforeDeath || wasFlyingAtDeath;
		var playerControllerSteps = new List<PlayerDeathStateTransitionStep> { PlayerDeathStateTransitionStep.CheckFlyingBeforeDeath };

		if (wasFlyingAtDeath)
		{
			playerControllerSteps.Add(PlayerDeathStateTransitionStep.SetFlyingBeforeDeathFlag);
		}

		playerControllerSteps.Add(PlayerDeathStateTransitionStep.ClearRideAndRestingState);
		playerControllerSteps.Add(PlayerDeathStateTransitionStep.ClearExistingFloatingCorpseState);
		playerControllerSteps.Add(PlayerDeathStateTransitionStep.ClearFlyingAndGlidingCreatureState);
		playerControllerSteps.Add(PlayerDeathStateTransitionStep.ClearFlyingAndGlidingFlyState);

		var creatureControllerSteps = wouldUseFloatingCorpse
			? new[] { PlayerDeathStateTransitionStep.ClearActiveState, PlayerDeathStateTransitionStep.SetFloatingCorpseState }
			: new[] { PlayerDeathStateTransitionStep.SetDeadState };

		return CreatePhasePlans(playerControllerSteps, creatureControllerSteps);
	}

	public static PlayerDeathStateTransitionResult Apply(Player player)
	{
		// Java parity:
		// PlayerController.onDie records isFlyingBeforeDeath before it clears FLYING/GLIDING.
		// CreatureController.onDie then uses that flag to set FLOATING_CORPSE instead of DEAD.
		var previousCreatureState = player.CreatureState;
		var previousFlyState = player.FlyState;
		var previousFlyingBeforeDeath = player.IsFlyingBeforeDeath;
		var wasFlyingAtDeath = player.IsInState(PlayerCreatureState.Flying);
		var playerControllerSteps = new List<PlayerDeathStateTransitionStep>();
		var creatureControllerSteps = new List<PlayerDeathStateTransitionStep>();
		var steps = new List<PlayerDeathStateTransitionStep> { PlayerDeathStateTransitionStep.CheckFlyingBeforeDeath };
		playerControllerSteps.Add(PlayerDeathStateTransitionStep.CheckFlyingBeforeDeath);

		if (wasFlyingAtDeath)
		{
			player.IsFlyingBeforeDeath = true;
			steps.Add(PlayerDeathStateTransitionStep.SetFlyingBeforeDeathFlag);
			playerControllerSteps.Add(PlayerDeathStateTransitionStep.SetFlyingBeforeDeathFlag);
		}

		player.IsInRideMode = false;
		player.RideInfo = null;
		player.SetCreatureState(PlayerCreatureState.Resting, enabled: false);
		player.SetCreatureState(PlayerCreatureState.FloatingCorpse, enabled: false);
		steps.Add(PlayerDeathStateTransitionStep.ClearRideAndRestingState);
		steps.Add(PlayerDeathStateTransitionStep.ClearExistingFloatingCorpseState);
		playerControllerSteps.Add(PlayerDeathStateTransitionStep.ClearRideAndRestingState);
		playerControllerSteps.Add(PlayerDeathStateTransitionStep.ClearExistingFloatingCorpseState);

		player.SetCreatureState(PlayerCreatureState.Flying, enabled: false);
		player.SetCreatureState(PlayerCreatureState.Gliding, enabled: false);
		player.UnsetFlyState(PlayerFlyState.Flying);
		player.UnsetFlyState(PlayerFlyState.Gliding);
		steps.Add(PlayerDeathStateTransitionStep.ClearFlyingAndGlidingCreatureState);
		steps.Add(PlayerDeathStateTransitionStep.ClearFlyingAndGlidingFlyState);
		playerControllerSteps.Add(PlayerDeathStateTransitionStep.ClearFlyingAndGlidingCreatureState);
		playerControllerSteps.Add(PlayerDeathStateTransitionStep.ClearFlyingAndGlidingFlyState);

		if (player.IsFlyingBeforeDeath)
		{
			player.SetCreatureState(PlayerCreatureState.Active, enabled: false);
			player.SetCreatureState(PlayerCreatureState.FloatingCorpse, enabled: true);
			steps.Add(PlayerDeathStateTransitionStep.ClearActiveState);
			steps.Add(PlayerDeathStateTransitionStep.SetFloatingCorpseState);
			creatureControllerSteps.Add(PlayerDeathStateTransitionStep.ClearActiveState);
			creatureControllerSteps.Add(PlayerDeathStateTransitionStep.SetFloatingCorpseState);
		}
		else
		{
			player.SetCreatureState(PlayerCreatureState.Dead, enabled: true);
			steps.Add(PlayerDeathStateTransitionStep.SetDeadState);
			creatureControllerSteps.Add(PlayerDeathStateTransitionStep.SetDeadState);
		}

		return new PlayerDeathStateTransitionResult(
			player.IsFlyingBeforeDeath ? PlayerDeathStateTransitionStatus.FloatingCorpseApplied : PlayerDeathStateTransitionStatus.DeadStateApplied,
			player.ObjectId,
			previousCreatureState,
			player.CreatureState,
			previousFlyState,
			player.FlyState,
			previousFlyingBeforeDeath,
			player.IsFlyingBeforeDeath,
			wasFlyingAtDeath,
			steps,
			CreatePhasePlans(playerControllerSteps, creatureControllerSteps),
			"com.aionemu.gameserver.controllers.PlayerController.onDie -> setIsFlyingBeforeDeath when FLYING, clear ride/rest/flying/gliding; CreatureController.onDie -> if isFlyingBeforeDeath set FLOATING_CORPSE else DEAD",
			IsLive: true
		);
	}

	private static IReadOnlyList<PlayerDeathStateTransitionPhasePlan> CreatePhasePlans(
		IReadOnlyList<PlayerDeathStateTransitionStep> playerControllerSteps,
		IReadOnlyList<PlayerDeathStateTransitionStep> creatureControllerSteps
	)
	{
		return new[]
		{
			new PlayerDeathStateTransitionPhasePlan(
				PlayerDeathStateTransitionPhase.PlayerControllerPreSuperCleanup,
				playerControllerSteps.ToArray(),
				"com.aionemu.gameserver.controllers.PlayerController.onDie -> setIsFlyingBeforeDeath when FLYING; clear ride/rest/floating/flying/gliding before super.onDie"
			),
			new PlayerDeathStateTransitionPhasePlan(
				PlayerDeathStateTransitionPhase.CreatureControllerDeathStateSelection,
				creatureControllerSteps.ToArray(),
				"com.aionemu.gameserver.controllers.CreatureController.onDie -> if player.getIsFlyingBeforeDeath() set FLOATING_CORPSE else DEAD after abort/casting/effect cleanup"
			),
		};
	}
}
