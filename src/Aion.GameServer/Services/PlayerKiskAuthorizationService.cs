using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerKiskAuthorizationService
{
	public static PlayerKiskBindAuthorization ValidateBind(Player player, PlayerKiskRuntimeState kisk)
	{
		// Java parity: model/gameobjects/Kisk.canBind checks duplicate/full state before use-mask permissions.
		if (player.BoundKiskObjectId == kisk.ObjectId || kisk.CurrentMemberIds.Contains(player.ObjectId))
			return PlayerKiskBindAuthorization.AlreadyRegistered();

		if (kisk.CurrentMemberCount >= kisk.MaxMembers)
			return PlayerKiskBindAuthorization.Full();

		return IsUseAllowed(player, kisk)
			? PlayerKiskBindAuthorization.Allowed()
			: PlayerKiskBindAuthorization.NoAuthority();
	}

	public static bool IsUseAllowed(Player player, PlayerKiskRuntimeState kisk)
	{
		// Java parity: model/gameobjects/Kisk.isUseAllowed.
		return kisk.UseMask switch
		{
			0 => true,
			1 => SameRace(player.Race, kisk.OwnerRace),
			2 => player.ObjectId == kisk.OwnerObjectId || (kisk.OwnerLegionId != 0 && player.LegionId == kisk.OwnerLegionId),
			3 => player.ObjectId == kisk.OwnerObjectId,
			4 => player.ObjectId == kisk.OwnerObjectId,
			5 => player.ObjectId == kisk.OwnerObjectId,
			_ => false,
		};
	}

	private static bool SameRace(string left, string right)
	{
		return !string.IsNullOrWhiteSpace(left)
			&& string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
	}
}

public sealed record PlayerKiskBindAuthorization(PlayerKiskBindAuthorizationStatus Status)
{
	public bool IsAllowed => Status == PlayerKiskBindAuthorizationStatus.Allowed;

	public static PlayerKiskBindAuthorization Allowed()
	{
		return new PlayerKiskBindAuthorization(PlayerKiskBindAuthorizationStatus.Allowed);
	}

	public static PlayerKiskBindAuthorization AlreadyRegistered()
	{
		return new PlayerKiskBindAuthorization(PlayerKiskBindAuthorizationStatus.AlreadyRegistered);
	}

	public static PlayerKiskBindAuthorization Full()
	{
		return new PlayerKiskBindAuthorization(PlayerKiskBindAuthorizationStatus.Full);
	}

	public static PlayerKiskBindAuthorization NoAuthority()
	{
		return new PlayerKiskBindAuthorization(PlayerKiskBindAuthorizationStatus.NoAuthority);
	}
}

public enum PlayerKiskBindAuthorizationStatus
{
	Allowed,
	AlreadyRegistered,
	Full,
	NoAuthority,
}
