using Aion.Commons.Threading;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Tests;

/// <summary>
/// Tests for the GameScheduler periodic task execution.
/// </summary>
public class GameSchedulerTests : IAsyncDisposable
{
	private readonly ILogger _logger;
	private readonly GameScheduler _scheduler;

	public GameSchedulerTests()
	{
		var factory = LoggerFactory.Create(builder => builder.AddConsole());
		_logger = factory.CreateLogger<GameSchedulerTests>();
		_scheduler = new GameScheduler(_logger);
	}

	[Fact]
	public async Task ScheduleAtFixedRate_ExecutesTaskPeriodically()
	{
		var executionCount = 0;

		_scheduler.ScheduleAtFixedRate(
			"test-task",
			async () =>
			{
				Interlocked.Increment(ref executionCount);
				await Task.CompletedTask;
			},
			initialDelay: TimeSpan.Zero,
			period: TimeSpan.FromMilliseconds(50)
		);

		// Wait for at least 2 executions
		await Task.Delay(200);
		await _scheduler.ShutdownAsync();

		// Should have executed at least twice (0ms, 50ms, 100ms, 150ms...)
		Assert.True(executionCount >= 2);
	}

	[Fact]
	public async Task ScheduleOnce_ExecutesTaskExactlyOnce()
	{
		var executionCount = 0;

		_scheduler.ScheduleOnce(
			"one-time-task",
			async () =>
			{
				Interlocked.Increment(ref executionCount);
				await Task.CompletedTask;
			},
			delay: TimeSpan.FromMilliseconds(50)
		);

		await Task.Delay(200);

		// Should have executed exactly once
		Assert.Equal(1, executionCount);
	}

	[Fact]
	public async Task ScheduleOnce_TaskRemovedAfterExecution()
	{
		_scheduler.ScheduleOnce("one-time-task", async () => await Task.CompletedTask, delay: TimeSpan.FromMilliseconds(50));

		Assert.Equal(1, _scheduler.GetActiveTaskCount());

		await Task.Delay(200);

		// Task should be removed after execution
		Assert.Equal(0, _scheduler.GetActiveTaskCount());
	}

	[Fact]
	public void CancelTask_RemovesScheduledTask()
	{
		var executionCount = 0;

		_scheduler.ScheduleAtFixedRate(
			"cancel-me",
			async () =>
			{
				Interlocked.Increment(ref executionCount);
				await Task.CompletedTask;
			},
			initialDelay: TimeSpan.FromMilliseconds(100),
			period: TimeSpan.FromMilliseconds(50)
		);

		// Cancel before first execution
		var removed = _scheduler.CancelTask("cancel-me");

		Assert.True(removed);
		Assert.Equal(0, _scheduler.GetActiveTaskCount());
	}

	[Fact]
	public void CancelTask_ReturnsfalseForUnknownTask()
	{
		var removed = _scheduler.CancelTask("unknown-task");
		Assert.False(removed);
	}

	[Fact]
	public void DuplicateSchedule_ThrowsInvalidOperationException()
	{
		_scheduler.ScheduleAtFixedRate("duplicate", async () => await Task.CompletedTask, TimeSpan.Zero, TimeSpan.FromMilliseconds(50));

		Assert.Throws<InvalidOperationException>(() =>
		{
			_scheduler.ScheduleAtFixedRate("duplicate", async () => await Task.CompletedTask, TimeSpan.Zero, TimeSpan.FromMilliseconds(50));
		});
	}

	[Fact]
	public void GetTaskInfo_ReturnsTaskDetails()
	{
		_scheduler.ScheduleAtFixedRate("info-task", async () => await Task.CompletedTask, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

		var info = _scheduler.GetTaskInfo("info-task");

		Assert.NotNull(info);
		Assert.Equal("info-task", info!.Name);
		Assert.Equal(TimeSpan.FromMilliseconds(100), info.Period);
		Assert.True(info.IsPeriodic);
	}

	[Fact]
	public void GetTaskInfo_ReturnsNullForUnknownTask()
	{
		var info = _scheduler.GetTaskInfo("unknown");
		Assert.Null(info);
	}

	[Fact]
	public void GetActiveTaskCount_ReturnsCorrectCount()
	{
		Assert.Equal(0, _scheduler.GetActiveTaskCount());

		_scheduler.ScheduleAtFixedRate("task1", async () => await Task.CompletedTask, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

		Assert.Equal(1, _scheduler.GetActiveTaskCount());

		_scheduler.ScheduleAtFixedRate("task2", async () => await Task.CompletedTask, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

		Assert.Equal(2, _scheduler.GetActiveTaskCount());
	}

	[Fact]
	public async Task ShutdownAsync_CancelsAllTasks()
	{
		var task1Executed = false;
		var task2Executed = false;

		_scheduler.ScheduleAtFixedRate(
			"task1",
			async () =>
			{
				task1Executed = true;
				await Task.CompletedTask;
			},
			TimeSpan.Zero,
			TimeSpan.FromMilliseconds(50)
		);

		_scheduler.ScheduleAtFixedRate(
			"task2",
			async () =>
			{
				task2Executed = true;
				await Task.CompletedTask;
			},
			TimeSpan.Zero,
			TimeSpan.FromMilliseconds(50)
		);

		await Task.Delay(100);
		await _scheduler.ShutdownAsync(TimeSpan.FromSeconds(2));

		// Both should have executed at least once
		Assert.True(task1Executed);
		Assert.True(task2Executed);

		// All tasks should be removed
		Assert.Equal(0, _scheduler.GetActiveTaskCount());
	}

	public async ValueTask DisposeAsync()
	{
		await _scheduler.DisposeAsync();
	}
}
