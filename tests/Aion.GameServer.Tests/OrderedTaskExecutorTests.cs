using Aion.GameServer.Utils.Concurrent;

namespace Aion.GameServer.Tests;

public class OrderedTaskExecutorTests
{
	[Fact]
	public async Task EnqueueAsync_RunsSameKeyInFifoOrder()
	{
		await using var executor = new OrderedTaskExecutor<string>();
		var observed = new List<int>();
		var tasks = Enumerable.Range(0, 5)
			.Select(i => executor.EnqueueAsync("connection-1", async _ =>
			{
				await Task.Delay(10);
				observed.Add(i);
			}))
			.ToArray();

		await Task.WhenAll(tasks);

		Assert.Equal([0, 1, 2, 3, 4], observed);
		Assert.Equal(0, executor.GetActiveQueueCount());
	}

	[Fact]
	public async Task EnqueueAsync_AllowsDifferentKeysToRunConcurrently()
	{
		await using var executor = new OrderedTaskExecutor<string>();
		var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var started = 0;

		var first = executor.EnqueueAsync("connection-1", async _ =>
		{
			Interlocked.Increment(ref started);
			await gate.Task;
		});
		var second = executor.EnqueueAsync("connection-2", async _ =>
		{
			Interlocked.Increment(ref started);
			await gate.Task;
		});

		await WaitUntilAsync(() => Volatile.Read(ref started) == 2);
		gate.SetResult();
		await Task.WhenAll(first, second);

		Assert.Equal(2, started);
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		var deadline = DateTime.UtcNow.AddSeconds(2);
		while (!condition())
		{
			if (DateTime.UtcNow >= deadline)
				throw new TimeoutException("Condition was not met before timeout.");

			await Task.Delay(10);
		}
	}
}
