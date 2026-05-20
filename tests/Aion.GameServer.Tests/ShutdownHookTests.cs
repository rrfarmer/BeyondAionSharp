using Aion.GameServer.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class ShutdownHookTests
{
	[Fact]
	public async Task ShutdownHook_CountsDownAndStopsApplication()
	{
		var lifetime = new FakeApplicationLifetime();
		var hook = new ShutdownHook(lifetime, NullLogger<ShutdownHook>.Instance, TimeSpan.FromMilliseconds(10));

		hook.InitShutdown(exitCode: 2, delaySeconds: 2);

		Assert.True(hook.IsRunning);
		Assert.Equal(2, hook.ExitCode);
		await WaitUntilAsync(() => lifetime.StopCalls == 1);

		Assert.Equal(0, hook.RemainingSeconds);
	}

	[Fact]
	public void ShutdownHook_IgnoresNegativeDelay()
	{
		var lifetime = new FakeApplicationLifetime();
		var hook = new ShutdownHook(lifetime, NullLogger<ShutdownHook>.Instance, TimeSpan.FromMilliseconds(10));

		hook.InitShutdown(exitCode: 1, delaySeconds: -1);

		Assert.False(hook.IsRunning);
		Assert.Equal(0, lifetime.StopCalls);
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (!condition())
		{
			await Task.Delay(10, timeout.Token);
		}
	}

	private sealed class FakeApplicationLifetime : IHostApplicationLifetime
	{
		private readonly CancellationTokenSource _started = new();
		private readonly CancellationTokenSource _stopping = new();
		private readonly CancellationTokenSource _stopped = new();

		public CancellationToken ApplicationStarted => _started.Token;

		public CancellationToken ApplicationStopping => _stopping.Token;

		public CancellationToken ApplicationStopped => _stopped.Token;

		public int StopCalls { get; private set; }

		public void StopApplication()
		{
			StopCalls++;
			_stopping.Cancel();
		}
	}
}
