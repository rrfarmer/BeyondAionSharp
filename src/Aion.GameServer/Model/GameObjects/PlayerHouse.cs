namespace Aion.GameServer.Model.GameObjects;

// Java parity: model/house/House login fields consumed by SM_HOUSE_OWNER_INFO.
public sealed record PlayerHouse(
	int ObjectId,
	int AddressId,
	int BuildingId,
	DateTime? AcquiredTime,
	DateTime? NextPay,
	bool IsInactive)
{
	public int GetGraceSeconds(Func<DateTime>? clock = null)
	{
		if (!IsInactive)
			return -1;

		var now = clock?.Invoke() ?? DateTime.Now;
		var graceEnd = (AcquiredTime ?? now).AddDays(14);
		return Math.Max(0, (int)(graceEnd - now).TotalSeconds);
	}
}
