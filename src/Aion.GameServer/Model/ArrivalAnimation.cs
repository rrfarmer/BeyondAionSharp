namespace Aion.GameServer.Model;

public enum ArrivalAnimation : byte
{
	// Java parity: model/animations/ArrivalAnimation IDs consumed by SM_PLAYER_INFO.
	None = 0,
	Landing = 2,
	FadeInBeam = 4,
	JumpOutCameraBehind = 10,
	JumpOutCameraFront = 11,
	LandingGlow = 18,
}
