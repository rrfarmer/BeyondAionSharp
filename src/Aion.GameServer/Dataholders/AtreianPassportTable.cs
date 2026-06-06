using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class AtreianPassportTable
{
	private const int ExpiryDaysAfterLastReward = 14;
	private readonly IReadOnlyDictionary<int, AtreianPassportSummary> _passportsById;

	public AtreianPassportTable(IReadOnlyList<AtreianPassportSummary> passports)
	{
		Passports = passports;
		// Java parity: AtreianPassportData.afterUnmarshal Map.put indexes by id and lets later rows replace earlier ones.
		var byId = new Dictionary<int, AtreianPassportSummary>();
		foreach (var passport in passports)
			byId[passport.Id] = passport;
		_passportsById = new ReadOnlyDictionary<int, AtreianPassportSummary>(byId);
		ExpireDate = CalculateExpireDate(passports);
	}

	public IReadOnlyList<AtreianPassportSummary> Passports { get; }

	public int Count => Passports.Count;

	public DateTime? ExpireDate { get; }

	public AtreianPassportSummary? GetAtreianPassportId(int id)
	{
		return _passportsById.GetValueOrDefault(id);
	}

	public bool IsDisabled(DateTime checkDateTime)
	{
		// Java parity: AtreianPassportService.isAtreianPassportDisabled(LocalDateTime).
		return ExpireDate.HasValue && checkDateTime > ExpireDate.Value;
	}

	private static DateTime? CalculateExpireDate(IReadOnlyList<AtreianPassportSummary> passports)
	{
		// Java parity: AtreianPassportService.findLastRewardTime/calculatePassportExpireDate.
		var lastRewardTime = passports
			.Where(passport => passport.AttendType is "DAILY" or "CUMULATIVE")
			.Select(passport => (DateTime?)passport.PeriodEnd)
			.DefaultIfEmpty()
			.Max();
		if (!lastRewardTime.HasValue)
			return null;

		return lastRewardTime.Value.Date
			.AddDays(1)
			.AddTicks(-1)
			.AddDays(ExpiryDaysAfterLastReward);
	}
}

public sealed record AtreianPassportSummary(
	int Id,
	bool Active,
	DateTime PeriodStart,
	DateTime PeriodEnd,
	string AttendType,
	int AttendNum,
	int RewardItemId,
	int RewardItemCount,
	int RewardExpireMinutes,
	int RewardPermitLevel);
