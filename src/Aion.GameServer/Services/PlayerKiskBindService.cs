using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerKiskBindService
{
	public static PlayerKiskBindResult Bind(Player player, PlayerKiskRuntimeState kisk, PlayerKiskRuntimeState? previousKisk = null)
	{
		// Java parity: services/KiskService.onBind owner/member mutation subset.
		if (player.BoundKiskObjectId == kisk.ObjectId || kisk.CurrentMemberIds.Contains(player.ObjectId))
			return PlayerKiskBindResult.AlreadyRegistered();

		if (kisk.CurrentMemberCount >= kisk.MaxMembers)
			return PlayerKiskBindResult.Full();

		int? removedOldKiskObjectId = null;
		if (previousKisk != null && previousKisk.ObjectId != kisk.ObjectId && previousKisk.RemoveMember(player.ObjectId))
			removedOldKiskObjectId = previousKisk.ObjectId;

		if (!kisk.AddMember(player.ObjectId))
			return PlayerKiskBindResult.AlreadyRegistered();

		player.BoundKiskObjectId = kisk.ObjectId;
		return PlayerKiskBindResult.Bound(removedOldKiskObjectId);
	}
}

public sealed record PlayerKiskBindResult(PlayerKiskBindStatus Status, int? RemovedOldKiskObjectId = null)
{
	public bool IsBound => Status == PlayerKiskBindStatus.Bound;

	public static PlayerKiskBindResult Bound(int? removedOldKiskObjectId = null)
	{
		return new PlayerKiskBindResult(PlayerKiskBindStatus.Bound, removedOldKiskObjectId);
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
