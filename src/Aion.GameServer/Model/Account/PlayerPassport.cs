namespace Aion.GameServer.Model.Account;

// Java parity: model/account/Passport.RewardStatus ids are serialized by SM_ATREIAN_PASSPORT.
public enum PlayerPassportRewardStatus
{
	Upcoming = 0,
	Available = 1,
	Taken = 2,
	Expired = 3,
}

public sealed record PlayerPassport(
	int PassportId,
	bool Rewarded,
	DateTime ArriveDate,
	bool FakeStamp = false)
{
	public PlayerPassportRewardStatus RewardStatus
		=> FakeStamp
			? Rewarded ? PlayerPassportRewardStatus.Taken : PlayerPassportRewardStatus.Upcoming
			: Rewarded ? PlayerPassportRewardStatus.Taken : PlayerPassportRewardStatus.Available;

	public int ArriveEpochSeconds => unchecked((int)new DateTimeOffset(NormalizeDate(ArriveDate)).ToUnixTimeSeconds());

	private static DateTime NormalizeDate(DateTime value)
		=> new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Kind);
}
