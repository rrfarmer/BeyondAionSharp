namespace Aion.GameServer.Model.GameObjects;

public sealed class PlayerSettings
{
	public const int DenyGroupRequests = 4;
	public const int DenyTradeRequests = 2;
	public const int DenyFriendRequests = 16;
	public const int DenyDuelRequests = 32;

	public byte[]? UiSettings { get; set; }

	public byte[]? Shortcuts { get; set; }

	public byte[]? HouseBuddies { get; set; }

	public int Deny { get; set; }

	public int Display { get; set; }

	public bool DeniesFriendRequests()
	{
		// Java parity: model/gameobjects/player/PlayerSettings.isInDeniedStatus(DeniedStatus.FRIEND).
		return IsInDeniedStatus(DenyFriendRequests);
	}

	public bool DeniesTradeRequests()
	{
		// Java parity: model/gameobjects/player/PlayerSettings.isInDeniedStatus(DeniedStatus.TRADE).
		return IsInDeniedStatus(DenyTradeRequests);
	}

	public bool DeniesGroupRequests()
	{
		// Java parity: model/gameobjects/player/PlayerSettings.isInDeniedStatus(DeniedStatus.GROUP).
		return IsInDeniedStatus(DenyGroupRequests);
	}

	public bool DeniesDuelRequests()
	{
		// Java parity: model/gameobjects/player/PlayerSettings.isInDeniedStatus(DeniedStatus.DUEL).
		return IsInDeniedStatus(DenyDuelRequests);
	}

	public bool IsInDeniedStatus(int deniedStatus)
	{
		return (Deny & deniedStatus) == deniedStatus;
	}
}
