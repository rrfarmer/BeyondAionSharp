using Aion.GameServer.Configuration;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class HouseMaintenanceTimingServiceTests
{
	[Fact]
	public void GetNextRunAfter_UsesConfiguredMaintenanceCron()
	{
		var service = new HouseMaintenanceTimingService(
			new GameServerOptions { Housing = new GameServerHousingOptions { MaintenanceTime = "0 30 3 ? * WED" } });

		var nextRun = service.GetNextRunAfter(new DateTime(2026, 5, 19, 18, 0, 0, DateTimeKind.Local));

		Assert.Equal(new DateTime(2026, 5, 20, 3, 30, 0, DateTimeKind.Local), nextRun);
	}

	[Fact]
	public void CalculateImpoundDate_UsesFirstMaintenanceAtOrAfterTwoWeekDueDate()
	{
		var service = new HouseMaintenanceTimingService(new GameServerOptions());
		var paidUntil = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Local);
		var now = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Local);

		var impoundDate = service.CalculateImpoundDate(paidUntil, now);
		var alreadyOverdueImpoundDate = service.CalculateImpoundDate(paidUntil, paidUntil.AddDays(15));

		Assert.Equal(new DateTime(2026, 1, 19, 0, 0, 0, DateTimeKind.Local), impoundDate);
		Assert.Equal(paidUntil.AddDays(15), alreadyOverdueImpoundDate);
	}

	[Fact]
	public void GetMaintenanceMailSenderName_SelectsJavaOverdueStage()
	{
		var service = new HouseMaintenanceTimingService(new GameServerOptions());
		var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local);

		Assert.Null(service.GetMaintenanceMailSenderName(now.AddDays(15), now));
		Assert.Equal(HouseMaintenanceTimingService.FirstOverdueMailSender, service.GetMaintenanceMailSenderName(now.AddDays(14), now));
		Assert.Equal(HouseMaintenanceTimingService.FirstOverdueMailSender, service.GetMaintenanceMailSenderName(now.AddDays(8), now));
		Assert.Equal(HouseMaintenanceTimingService.SecondOverdueMailSender, service.GetMaintenanceMailSenderName(now.AddDays(7), now));
		Assert.Equal(HouseMaintenanceTimingService.SecondOverdueMailSender, service.GetMaintenanceMailSenderName(now.AddDays(1), now));
		Assert.Equal(HouseMaintenanceTimingService.FinalOverdueMailSender, service.GetMaintenanceMailSenderName(now, now));
		Assert.Equal(HouseMaintenanceTimingService.FinalOverdueMailSender, service.GetMaintenanceMailSenderName(now.AddDays(-1), now));
	}
}
