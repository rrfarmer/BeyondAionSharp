namespace Aion.GameServer.Model.GameObjects;

// Java parity: model/house/House login fields consumed by SM_HOUSE_OWNER_INFO.
public sealed record PlayerHouse(
	int ObjectId,
	int AddressId,
	int BuildingId,
	DateTime? AcquiredTime,
	DateTime? NextPay,
	bool IsInactive,
	byte DoorState = PlayerHouse.DoorOpen,
	bool ShowOwnerName = true,
	string? SignNotice = null)
{
	public const int SignNoticeMaxLength = 64;
	public const byte DoorOpen = 1;
	public const byte DoorClosedExceptFriends = 2;
	public const byte DoorClosed = 3;

	public int GetGraceSeconds(Func<DateTime>? clock = null)
	{
		if (!IsInactive)
			return -1;

		var now = clock?.Invoke() ?? DateTime.Now;
		var graceEnd = (AcquiredTime ?? now).AddDays(14);
		return Math.Max(0, (int)(graceEnd - now).TotalSeconds);
	}

	public static bool IsKnownDoorState(byte doorState)
	{
		// Java parity: model/house/HouseDoorState.get(byte).
		return doorState is DoorOpen or DoorClosedExceptFriends or DoorClosed;
	}

	public static bool GetShowOwnerNameFromSettings(int settings)
	{
		// Java parity: model/house/House.setPermissionsFromDB.
		return (settings & 0xFF) == 1;
	}

	public static byte GetDoorStateFromSettings(int settings)
	{
		// Java parity: model/house/House.setPermissionsFromDB falls back to resetDoorState on invalid values.
		var doorState = (byte)(settings >> 8);
		return IsKnownDoorState(doorState) ? doorState : DoorOpen;
	}

	public static int CreateSettings(byte doorState, bool showOwnerName)
	{
		// Java parity: model/house/House.getPermissionsForDB.
		return (showOwnerName ? 1 : 0) | (doorState << 8);
	}
}
