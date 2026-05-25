using Aion.GameServer.Configuration;

namespace Aion.GameServer.Services;

public static class NpcFactionDailyResetService
{
	private const int ResetHour = 9;

	public static int GetNextResetEpochSeconds(DateTimeOffset now, GameServerOptions options)
	{
		return GetNextResetEpochSeconds(now, options.Core.GetTimeZone());
	}

	public static int GetNextResetEpochSeconds(DateTimeOffset now, TimeZoneInfo serverTimeZone)
	{
		// Java parity breadcrumb: NpcFactions.getNextTime uses ServerTime.now()
		// and advances at hour >= 9, so exactly 09:00 belongs to tomorrow.
		var serverNow = TimeZoneInfo.ConvertTime(now, serverTimeZone);
		var resetDate = serverNow.Hour >= ResetHour
			? serverNow.Date.AddDays(1)
			: serverNow.Date;
		var localReset = new DateTime(
			resetDate.Year,
			resetDate.Month,
			resetDate.Day,
			ResetHour,
			0,
			0,
			DateTimeKind.Unspecified);
		return (int) new DateTimeOffset(localReset, serverTimeZone.GetUtcOffset(localReset)).ToUnixTimeSeconds();
	}
}
