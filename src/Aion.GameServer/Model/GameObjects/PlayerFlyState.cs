namespace Aion.GameServer.Model.GameObjects;

[Flags]
public enum PlayerFlyState
{
	None = 0,
	Flying = 1,
	Gliding = 1 << 1,
}
