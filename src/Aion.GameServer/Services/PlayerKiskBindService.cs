using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerKiskBindService
{
	public static PlayerKiskBindResult Bind(Player player, PlayerKiskRuntimeState kisk)
	{
		// Java parity: services/KiskService.onBind owner/member mutation subset.
		if (player.BoundKiskObjectId == kisk.ObjectId || kisk.CurrentMemberIds.Contains(player.ObjectId))
			return PlayerKiskBindResult.AlreadyRegistered();

		if (kisk.CurrentMemberCount >= kisk.MaxMembers)
			return PlayerKiskBindResult.Full();

		if (!kisk.AddMember(player.ObjectId))
			return PlayerKiskBindResult.AlreadyRegistered();

		player.BoundKiskObjectId = kisk.ObjectId;
		return PlayerKiskBindResult.Bound();
	}
}

public sealed record PlayerKiskBindResult(PlayerKiskBindStatus Status)
{
	public bool IsBound => Status == PlayerKiskBindStatus.Bound;

	public static PlayerKiskBindResult Bound()
	{
		return new PlayerKiskBindResult(PlayerKiskBindStatus.Bound);
	}

	public static PlayerKiskBindResult AlreadyRegistered()
	{
		return new PlayerKiskBindResult(PlayerKiskBindStatus.AlreadyRegistered);
	}

	public static PlayerKiskBindResult Full()
	{
		return new PlayerKiskBindResult(PlayerKiskBindStatus.Full);
	}
}

public enum PlayerKiskBindStatus
{
	Bound,
	AlreadyRegistered,
	Full,
}
