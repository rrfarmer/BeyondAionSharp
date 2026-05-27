using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskScheduledTaskHandleAdapterTests
{
	[Fact]
	public async Task Cancel_ForwardsToScheduledTaskCancelAndMarksHandleDone()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var ran = false;
		var scheduledTask = threadPoolManager.Schedule(
			_ =>
			{
				ran = true;
				return ValueTask.CompletedTask;
			},
			TimeSpan.FromMinutes(5));
		var handle = new PlayerProtectionActiveTaskScheduledTaskHandleAdapter(scheduledTask);

		var canceled = handle.Cancel(mayInterruptIfRunning: false);
		await WaitForCompletionAsync(scheduledTask);

		Assert.True(canceled);
		Assert.True(handle.IsDone);
		Assert.False(ran);
		Assert.False(handle.Cancel(mayInterruptIfRunning: false));
	}

	[Fact]
	public async Task IsDone_ReflectsCompletedScheduledTask()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var ran = false;
		var scheduledTask = threadPoolManager.Schedule(
			_ =>
			{
				ran = true;
				return ValueTask.CompletedTask;
			},
			TimeSpan.Zero);
		var handle = new PlayerProtectionActiveTaskScheduledTaskHandleAdapter(scheduledTask);

		await WaitForCompletionAsync(scheduledTask);

		Assert.True(ran);
		Assert.True(handle.IsDone);
		Assert.False(handle.Cancel(mayInterruptIfRunning: false));
	}

	[Fact]
	public async Task Cancel_IgnoresMayInterruptFlagLikeJavaProtectionCancelFalseBoundary()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var scheduledTask = threadPoolManager.Schedule(
			_ => ValueTask.CompletedTask,
			TimeSpan.FromMinutes(5));
		var handle = new PlayerProtectionActiveTaskScheduledTaskHandleAdapter(scheduledTask);

		var canceled = handle.Cancel(mayInterruptIfRunning: true);
		await WaitForCompletionAsync(scheduledTask);

		Assert.True(canceled);
		Assert.True(handle.IsDone);
	}

	private static ThreadPoolManager CreateThreadPoolManager() =>
		new(NullLogger<ThreadPoolManager>.Instance);

	private static async Task WaitForCompletionAsync(ScheduledTask scheduledTask)
	{
		var completed = await Task.WhenAny(scheduledTask.Completion, Task.Delay(TimeSpan.FromSeconds(2)));
		Assert.Same(scheduledTask.Completion, completed);
		await scheduledTask.Completion;
	}
}
