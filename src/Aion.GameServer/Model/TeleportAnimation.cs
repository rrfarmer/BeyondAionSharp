namespace Aion.GameServer.Model;

public enum TeleportAnimation : byte
{
	// Java parity: model/animations/TeleportAnimation IDs consumed by SM_TELEPORT_LOC.
	None = 0,
	FadeOutBeam = 1,
	FadeOut = 2,
	JumpIn = 3,
	JumpInStatue = 4,
	JumpInGate = 8,
	Battleground = 0,
}
