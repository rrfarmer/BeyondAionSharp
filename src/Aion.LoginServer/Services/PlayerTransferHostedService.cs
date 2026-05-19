using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aion.LoginServer.Services;

public sealed class PlayerTransferHostedService : IHostedService
{
	private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan Period = TimeSpan.FromMinutes(7);
	private readonly IPlayerTransferService _playerTransferService;
	private readonly ILogger<PlayerTransferHostedService> _logger;
	private readonly CancellationTokenSource _shutdownTokenSource = new();
	private Task? _loopTask;

	public PlayerTransferHostedService(IPlayerTransferService playerTransferService, ILogger<PlayerTransferHostedService> logger)
	{
		_playerTransferService = playerTransferService;
		_logger = logger;
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("PlayerTransferService will be initialized in 10 sec.");
		_loopTask = Task.Run(LoopAsync, cancellationToken);
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		await _shutdownTokenSource.CancelAsync();
		if (_loopTask != null)
			await Task.WhenAny(_loopTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
	}

	private async Task LoopAsync()
	{
		var cancellationToken = _shutdownTokenSource.Token;
		try
		{
			await Task.Delay(InitialDelay, cancellationToken);
			while (!cancellationToken.IsCancellationRequested)
			{
				await _playerTransferService.VerifyNewTasksAsync(cancellationToken);
				await Task.Delay(Period, cancellationToken);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}
}
