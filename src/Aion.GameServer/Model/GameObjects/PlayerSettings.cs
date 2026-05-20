namespace Aion.GameServer.Model.GameObjects;

public sealed class PlayerSettings
{
	public byte[]? UiSettings { get; init; }

	public byte[]? Shortcuts { get; init; }

	public byte[]? HouseBuddies { get; init; }
}
