namespace Aion.GameServer.Model.GameObjects;

public sealed class PlayerSettings
{
	public const int DenyFriendRequests = 16;

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

	public bool IsInDeniedStatus(int deniedStatus)
	{
		return (Deny & deniedStatus) == deniedStatus;
	}
}
