using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class JavaQuartzCronExpressionTests
{
	[Fact]
	public void TryParse_HandlesHourlyRiftSchedule()
	{
		Assert.True(JavaQuartzCronExpression.TryParse("0 0 * ? * *", out var expression));

		var nextAfterRun = expression.GetNextRunAfter(new DateTimeOffset(2026, 5, 22, 9, 0, 0, TimeSpan.Zero));
		var nextBeforeRun = expression.GetNextRunAfter(new DateTimeOffset(2026, 5, 22, 9, 59, 59, TimeSpan.Zero));

		Assert.Equal(new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero), nextAfterRun);
		Assert.Equal(new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero), nextBeforeRun);
	}

	[Fact]
	public void TryParse_HandlesCommaSeparatedQuartzDays()
	{
		Assert.True(JavaQuartzCronExpression.TryParse("0 0 18 ? * FRI,MON", out var expression));

		var fridayRun = expression.GetNextRunAfter(new DateTimeOffset(2026, 5, 22, 17, 59, 59, TimeSpan.Zero));
		var nextMondayRun = expression.GetNextRunAfter(new DateTimeOffset(2026, 5, 22, 18, 0, 0, TimeSpan.Zero));

		Assert.Equal(new DateTimeOffset(2026, 5, 22, 18, 0, 0, TimeSpan.Zero), fridayRun);
		Assert.Equal(new DateTimeOffset(2026, 5, 25, 18, 0, 0, TimeSpan.Zero), nextMondayRun);
	}

	[Fact]
	public void TryParse_RejectsUnsupportedExpressionShapes()
	{
		Assert.False(JavaQuartzCronExpression.TryParse("bad cron", out _));
		Assert.False(JavaQuartzCronExpression.TryParse("0 /5 * ? * *", out _));
		Assert.False(JavaQuartzCronExpression.TryParse("0 0 24 ? * *", out _));
	}
}
