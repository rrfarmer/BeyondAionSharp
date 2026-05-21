namespace Aion.GameServer.Controllers.Movement;

public static class MovementMask
{
	public const byte Immediate = 0x00;
	public const byte Glide = 0x04;
	public const byte Fall = 0x08;
	public const byte Vehicle = 0x10;
	public const byte Absolute = 0x20;
	public const byte Manual = 0x40;
	public const byte Position = 0x80;
	public const byte NpcWalkSlow = 0xEA;
	public const byte NpcWalkFast = 0xE8;
	public const byte NpcRunSlow = 0xE4;
	public const byte NpcRunFast = 0xE2;
	public const byte NpcStartMove = 0xE0;

	public static bool Has(byte mask, byte flag)
	{
		// Java parity: controllers/movement/MovementMask bit checks in CM_MOVE and SM_MOVE.
		return (mask & flag) == flag;
	}

	public static bool HasManualPosition(byte mask)
	{
		// Java parity: (type & POSITION) == POSITION && (type & MANUAL) == MANUAL.
		return Has(mask, Position) && Has(mask, Manual);
	}
}
