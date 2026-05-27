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
	string JavaSource,
	bool IsLive);

public static class PlayerDeathStateTransitionService
{
	public static PlayerDeathStateTransitionResult Apply(Player player)
	{
		// Java parity breadcrumb:
		// PlayerController.onDie records isFlyingBeforeDeath before it clears FLYING/GLIDING.
		// CreatureController.onDie then uses that flag to set FLOATING_CORPSE instead of DEAD.
		var previousCreatureState = player.CreatureState;
		var previousFlyState = player.FlyState;
		var previousFlyingBeforeDeath = player.IsFlyingBeforeDeath;
		var wasFlyingAtDeath = player.IsInState(PlayerCreatureState.Flying);
		var steps = new List<PlayerDeathStateTransitionStep>
		{
			PlayerDeathStateTransitionStep.CheckFlyingBeforeDeath,
		};

		if (wasFlyingAtDeath)
		{
			player.IsFlyingBeforeDeath = true;
			steps.Add(PlayerDeathStateTransitionStep.SetFlyingBeforeDeathFlag);
		}

		player.IsInRideMode = false;
		player.RideInfo = null;
		player.SetCreatureState(PlayerCreatureState.Resting, enabled: false);
		player.SetCreatureState(PlayerCreatureState.FloatingCorpse, enabled: false);
		steps.Add(PlayerDeathStateTransitionStep.ClearRideAndRestingState);
		steps.Add(PlayerDeathStateTransitionStep.ClearExistingFloatingCorpseState);

		player.SetCreatureState(PlayerCreatureState.Flying, enabled: false);
		player.SetCreatureState(PlayerCreatureState.Gliding, enabled: false);
		player.UnsetFlyState(PlayerFlyState.Flying);
		player.UnsetFlyState(PlayerFlyState.Gliding);
		steps.Add(PlayerDeathStateTransitionStep.ClearFlyingAndGlidingCreatureState);
		steps.Add(PlayerDeathStateTransitionStep.ClearFlyingAndGlidingFlyState);

		if (player.IsFlyingBeforeDeath)
		{
			player.SetCreatureState(PlayerCreatureState.Active, enabled: false);
			player.SetCreatureState(PlayerCreatureState.FloatingCorpse, enabled: true);
			steps.Add(PlayerDeathStateTransitionStep.ClearActiveState);
			steps.Add(PlayerDeathStateTransitionStep.SetFloatingCorpseState);
		}
		else
		{
			player.SetCreatureState(PlayerCreatureState.Dead, enabled: true);
			steps.Add(PlayerDeathStateTransitionStep.SetDeadState);
		}

		return new PlayerDeathStateTransitionResult(
			player.IsFlyingBeforeDeath
				? PlayerDeathStateTransitionStatus.FloatingCorpseApplied
				: PlayerDeathStateTransitionStatus.DeadStateApplied,
			player.ObjectId,
			previousCreatureState,
			player.CreatureState,
			previousFlyState,
			player.FlyState,
			previousFlyingBeforeDeath,
			player.IsFlyingBeforeDeath,
			wasFlyingAtDeath,
			steps,
			"com.aionemu.gameserver.controllers.PlayerController.onDie -> setIsFlyingBeforeDeath when FLYING, clear ride/rest/flying/gliding; CreatureController.onDie -> if isFlyingBeforeDeath set FLOATING_CORPSE else DEAD",
			IsLive: true);
	}
}
