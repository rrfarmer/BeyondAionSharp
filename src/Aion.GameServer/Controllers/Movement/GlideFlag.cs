namespace Aion.GameServer.Controllers.Movement;

public static class GlideFlag
{
	public const byte None = 0x00;
	public const byte WeakUpwind = 0x10;
	public const byte MediumUpwind = 0x20;
	public const byte StrongUpwind = WeakUpwind + MediumUpwind;
	public const byte Geyser = 0x80;
}
