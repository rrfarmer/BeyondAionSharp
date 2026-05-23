namespace Aion.GameServer.Model;

public enum ObjectDeleteAnimation : byte
{
	// Java parity: model/animations/ObjectDeleteAnimation IDs consumed by SM_DELETE and SM_PET.
	None = 0,
	FadeOut = 1,
	FadeOutBeam = 2,
	JumpIn = 11,
	Delayed = 19,
}
