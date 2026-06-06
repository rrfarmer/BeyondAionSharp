using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class AtreianPassportTable
{
	private readonly IReadOnlyDictionary<int, AtreianPassportSummary> _passportsById;

	public AtreianPassportTable(IReadOnlyList<AtreianPassportSummary> passports)
	{
		Passports = passports;
		// Java parity: AtreianPassportData.afterUnmarshal Map.put indexes by id and lets later rows replace earlier ones.
		var byId = new Dictionary<int, AtreianPassportSummary>();
		foreach (var passport in passports)
			byId[passport.Id] = passport;
		_passportsById = new ReadOnlyDictionary<int, AtreianPassportSummary>(byId);
	}

	public IReadOnlyList<AtreianPassportSummary> Passports { get; }

	public int Count => Passports.Count;

	public AtreianPassportSummary? GetAtreianPassportId(int id)
	{
		return _passportsById.GetValueOrDefault(id);
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
