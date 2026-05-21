using Aion.GameServer.Configuration;

namespace Aion.GameServer.Services;

public sealed class HouseMaintenanceTimingService
{
	public const string FirstOverdueMailSender = "$$HS_OVERDUE_1ST";
	public const string SecondOverdueMailSender = "$$HS_OVERDUE_2ND";
	public const string FinalOverdueMailSender = "$$HS_OVERDUE_3RD";
	private readonly JavaCronSchedule _maintenanceSchedule;
	private readonly TimeProvider _timeProvider;

	public HouseMaintenanceTimingService(GameServerOptions? options = null, TimeProvider? timeProvider = null)
	{
		_maintenanceSchedule = JavaCronSchedule.WeeklyOrDefault(options?.Housing.MaintenanceTime, DayOfWeek.Monday, 0);
		_timeProvider = timeProvider ?? TimeProvider.System;
	}

	public DateTime GetNextRun()
	{
		// Java parity: taskmanager/tasks/housing/MaintenanceTask.getNextRun.
		return GetNextRunAfter(GetNow().DateTime);
	}

	public DateTime GetNextRunAfter(DateTime date)
	{
		// Java parity: taskmanager/AbstractCronTask.getNextRunAfter for HousingConfig.HOUSE_MAINTENANCE_TIME.
		return _maintenanceSchedule.GetNextRunAfter(date);
	}

	public DateTime CalculateImpoundDate(DateTime housePaidUntil, DateTime? now = null)
	{
		// Java parity: taskmanager/tasks/housing/MaintenanceTask.calculateImpoundDate.
		var paymentDueDate = housePaidUntil.AddDays(14);
		var impoundDate = now ?? GetNow().DateTime;
		while (impoundDate < paymentDueDate)
			impoundDate = GetNextRunAfter(impoundDate);
		return impoundDate;
	}

	public string? GetMaintenanceMailSenderName(DateTime impoundDate, DateTime? now = null)
	{
		// Java parity: services/mail/MailFormatter.sendHouseMaintenanceMail overdue template selection.
		var daysUntilImpoundment = (long)((impoundDate - (now ?? GetNow().DateTime)).TotalDays);
		if (daysUntilImpoundment <= 0)
			return FinalOverdueMailSender;
		if (daysUntilImpoundment <= 7)
			return SecondOverdueMailSender;
		if (daysUntilImpoundment <= 14)
			return FirstOverdueMailSender;
		return null;
	}

	public long GetPaidWeeks(DateTime nextPay, DateTime? now = null)
	{
		// Java parity: ChronoUnit.WEEKS between ServerTime.now().with(LocalTime.MIDNIGHT) and nextPay.
		return (nextPay.Date - (now ?? GetNow().DateTime).Date).Days / 7;
	}

	private DateTimeOffset GetNow()
	{
		return _timeProvider.GetLocalNow();
	}
}
