namespace Aion.GameServer.Model.GameObjects;

public sealed class PlayerSettings
{
	public const int DenyFriendRequests = 16;

	public byte[]? UiSettings { get; init; }

	public byte[]? Shortcuts { get; init; }

	public byte[]? HouseBuddies { get; init; }

	public int Deny { get; init; }

	public int Display { get; init; }

	public bool DeniesFriendRequests()
	{
		// Java parity: model/gameobjects/player/PlayerSettings.isInDeniedStatus(DeniedStatus.FRIEND).
		return IsInDeniedStatus(DenyFriendRequests);
	}

	public bool IsInDeniedStatus(int deniedStatus)
	{
		return (Deny & deniedStatus) == deniedStatus;
	}
}
