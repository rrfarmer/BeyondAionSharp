using Aion.GameServer.Utils.Time;

namespace Aion.GameServer.Model.Account;

/// <summary>
/// Java parity: model/account/PassportsList (ViAl, SVDNESS).
/// </summary>
public class PassportsList
{
	private readonly List<Passport> passports;

	public PassportsList()
	{
		passports = new List<Passport>();
	}

	public void AddPassport(Passport passport)
	{
		passports.Add(passport);
	}

	public void RemovePassport(Passport passport)
	{
		passports.Remove(passport);
	}

	public Passport? GetPassport(int passportId, int timestamp)
	{
		foreach (Passport passport in this.passports)
		{
			if (passport.GetId() == passportId && new DateTimeOffset(passport.GetArriveDate()).ToUnixTimeMilliseconds() / 1000 == timestamp)
			{
				return passport;
			}
		}
		return null;
	}

	public bool IsPassportPresent(int passportId)
	{
		foreach (Passport pp in this.passports)
		{
			if (pp.GetId() == passportId)
			{
				return true;
			}
		}
		return false;
	}

	public List<Passport> GetAllPassports()
	{
		return passports;
	}

	public bool HasPassportForDay(int passportId, DateOnly attendDay)
	{
		return passports
			.Where(pp => pp.GetId() == passportId)
			.Select(pp => DateOnly.FromDateTime(ServerTime.AtDate(new DateTimeOffset(pp.GetArriveDate())).DateTime))
			.Any(d => d.Equals(attendDay));
	}
}
