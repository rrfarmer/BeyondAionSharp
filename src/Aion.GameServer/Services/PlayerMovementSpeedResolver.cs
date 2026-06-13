using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerMovementSpeedResolver
{
	private const float DefaultWalkSpeed = 1.5f;
	private const float DefaultRunSpeed = 6.0f;
	private const float DefaultFlySpeed = 9.0f;

	public static float ResolveKnownMovementSpeed(Player player)
	{
		// Java parity: PlayerClass.PlayerStatsTemplate supplies walk=1.5, run=6, fly=9 for ordinary players.
		if (!player.IsInRideMode || player.RideInfo == null)
		{
			if (player.IsInFlyingState())
				return DefaultFlySpeed;
			if (player.IsInState(PlayerCreatureState.Flying) && !player.IsInState(PlayerCreatureState.Resting))
				return 12.0f;
			return player.IsInState(PlayerCreatureState.WalkMode)
				? DefaultWalkSpeed
				: DefaultRunSpeed;
		}

		if (player.IsInState(PlayerCreatureState.Flying))
			return player.RideInfo.FlySpeed;

		return player.IsInSprintMode() && player.RideInfo.CanSprint()
			? player.RideInfo.SprintSpeed
			: player.RideInfo.MoveSpeed;
	}
}
