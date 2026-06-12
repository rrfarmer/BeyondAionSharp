namespace Aion.GameServer.Controllers.Movement;

public static class GlideFlag
{
	public const byte None = 0x00;
	public const byte WeakUpwind = 0x10;
	public const byte MediumUpwind = 0x20;
	public const byte StrongUpwind = WeakUpwind + MediumUpwind;
	public const byte Geyser = 0x80;
	public const byte NONE = None;
	public const byte WEAK_UPWIND = WeakUpwind;
	public const byte MEDIUM_UPWIND = MediumUpwind;
	public const byte STRONG_UPWIND = StrongUpwind;
	public const byte GEYSER = Geyser;
}
