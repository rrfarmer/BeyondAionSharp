namespace Aion.GameServer.Controllers.Movement;

/// <summary>Java parity: controllers/movement/MovementMask (Mr. Poke, Neon). UPPER_CASE names + byte values match Java.</summary>
public static class MovementMask
{
	public const byte IMMEDIATE = 0x00;
	public const byte GLIDE = 0x04;
	public const byte FALL = 0x08;
	public const byte VEHICLE = 0x10;
	public const byte ABSOLUTE = 0x20;
	public const byte MANUAL = 0x40;
	public const byte POSITION = 0x80;
	public const byte NPC_WALK_SLOW = 0xEA;
	public const byte NPC_WALK_FAST = 0xE8;
	public const byte NPC_RUN_SLOW = 0xE4;
	public const byte NPC_RUN_FAST = 0xE2;
	public const byte NPC_STARTMOVE = 0xE0;

	public static bool Has(byte mask, byte flag)
	{
		// Java parity: controllers/movement/MovementMask bit checks in CM_MOVE and SM_MOVE.
		return (mask & flag) == flag;
	}

	public static bool HasManualPosition(byte mask)
	{
		// Java parity: (type & POSITION) == POSITION && (type & MANUAL) == MANUAL.
		return Has(mask, POSITION) && Has(mask, MANUAL);
	}
}
