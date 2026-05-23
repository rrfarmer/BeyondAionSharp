using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class PlayerFlightActionService
{
	private const long FlyReuseTimeMillis = 10_000;
	private const long FlyStartReuseAdjustmentMillis = 100;

	public static PlayerFlightActionResult StartFlying(
		Player player,
		DateTimeOffset now,
		bool ignoreFlightCooldown = false)
	{
		// Java parity: controllers/FlyController.startFly(canFly + cooldown + state side effects).
		var canFly = CanFly(player);
		if (!canFly.Succeeded)
			return canFly;

		var nowMillis = now.ToUnixTimeMilliseconds();
		if (!ignoreFlightCooldown)
		{
			if (player.FlyReuseTimeMillis > nowMillis)
				return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.Cooldown);

			player.FlyReuseTimeMillis = nowMillis + FlyReuseTimeMillis - FlyStartReuseAdjustmentMillis;
		}

		player.StartFlying();
		return PlayerFlightActionResult.Success();
	}

	private static PlayerFlightActionResult CanFly(Player player)
	{
		// Java parity: controllers/FlyController.canFly guard order.
		if (!IsDaeva(player))
			return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.NotDaeva, SmSystemMessage.GlideOnlyDaevaCan());

		if (player.IsAbnormalSet(PlayerAbnormalState.NoFly))
			return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.NoFlyAbnormal, SmSystemMessage.CantFlyNowDueToNoFly());

		if (player.TransformForbidsFlight)
			return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.TransformForbidden, SmSystemMessage.FlyCannotFlyPolymorphStatus());

		if (player.IsInState(PlayerCreatureState.PrivateShop))
			return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.PrivateStore);

		return PlayerFlightActionResult.Success();
	}

	private static bool IsDaeva(Player player)
	{
		// Java parity: PlayerCommonData.isDaeva rejects only starting classes in the current C# model.
		return player.PlayerClass is not ("WARRIOR" or "SCOUT" or "MAGE" or "PRIEST" or "ENGINEER" or "ARTIST");
	}
}

public sealed record PlayerFlightActionResult(
	PlayerFlightActionStatus Status,
	SmSystemMessage? SystemMessage)
{
	public bool Succeeded => Status == PlayerFlightActionStatus.Success;

	public static PlayerFlightActionResult Success()
	{
		return new PlayerFlightActionResult(PlayerFlightActionStatus.Success, null);
	}

	public static PlayerFlightActionResult Failed(PlayerFlightActionStatus status, SmSystemMessage? systemMessage = null)
	{
		return new PlayerFlightActionResult(status, systemMessage);
	}
}

public enum PlayerFlightActionStatus
{
	Success,
	NotDaeva,
	NoFlyAbnormal,
	TransformForbidden,
	PrivateStore,
	Cooldown,
}
