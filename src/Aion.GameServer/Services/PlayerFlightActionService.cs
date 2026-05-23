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
		bool ignoreFlightCooldown = false,
		int freeFlightAccessLevel = 1)
	{
		// Java parity: controllers/FlyController.startFly(canFly + cooldown + state side effects).
		var canFly = CanFly(player, freeFlightAccessLevel);
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

	public static PlayerFlightActionResult StartGliding(Player player, DateTimeOffset now)
	{
		// Java parity: controllers/FlyController.switchToGliding(canGlide + walking cooldown + state side effects).
		if (player.IsInGlidingState())
			return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.AlreadyGliding);

		if (!player.CanPerformMove())
			return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.CannotMove);

		var canGlide = CanGlide(player);
		if (!canGlide.Succeeded)
			return canGlide;

		var nowMillis = now.ToUnixTimeMilliseconds();
		if (player.FlyState == PlayerFlyState.None)
		{
			if (player.FlyReuseTimeMillis > nowMillis)
				return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.Cooldown);

			player.FlyReuseTimeMillis = nowMillis + FlyReuseTimeMillis;
		}

		player.StartGliding();
		return PlayerFlightActionResult.Success();
	}

	private static PlayerFlightActionResult CanFly(Player player, int freeFlightAccessLevel)
	{
		// Java parity: controllers/FlyController.canFly guard order.
		if (!IsDaeva(player))
			return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.NotDaeva, SmSystemMessage.GlideOnlyDaevaCan());

		if (!HasAccess(player, freeFlightAccessLevel) && (player.IsInsideNoFlyZone || !player.IsInsideFlyZone))
			return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.FlyingForbiddenHere, SmSystemMessage.FlyingForbiddenHere());

		if (player.IsAbnormalSet(PlayerAbnormalState.NoFly))
			return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.NoFlyAbnormal, SmSystemMessage.CantFlyNowDueToNoFly());

		if (player.TransformForbidsFlight)
			return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.TransformForbidden, SmSystemMessage.FlyCannotFlyPolymorphStatus());

		if (player.IsInState(PlayerCreatureState.PrivateShop))
			return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.PrivateStore);

		return PlayerFlightActionResult.Success();
	}

	private static PlayerFlightActionResult CanGlide(Player player)
	{
		// Java parity: controllers/FlyController.canGlide guard order.
		if (!IsDaeva(player))
			return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.NotDaeva, SmSystemMessage.GlideOnlyDaevaCan());

		if (player.TransformForbidsFlight)
			return PlayerFlightActionResult.Failed(PlayerFlightActionStatus.TransformForbidden, SmSystemMessage.GlideCannotGlidePolymorphStatus());

		return PlayerFlightActionResult.Success();
	}

	private static bool IsDaeva(Player player)
	{
		// Java parity: PlayerCommonData.isDaeva rejects only starting classes in the current C# model.
		return player.PlayerClass is not ("WARRIOR" or "SCOUT" or "MAGE" or "PRIEST" or "ENGINEER" or "ARTIST");
	}

	private static bool HasAccess(Player player, int accessLevel)
	{
		// Java parity: model/gameobjects/player/Player.hasAccess.
		return player.AccessLevel >= accessLevel;
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
	FlyingForbiddenHere,
	NoFlyAbnormal,
	TransformForbidden,
	PrivateStore,
	Cooldown,
	AlreadyGliding,
	CannotMove,
}
