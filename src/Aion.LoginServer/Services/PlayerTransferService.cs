using System.Collections.Concurrent;
using Aion.LoginServer.Data;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network.GameServer;
using Aion.LoginServer.Network.GameServer.ServerPackets;
using Microsoft.Extensions.Logging;

namespace Aion.LoginServer.Services;

public interface IPlayerTransferService
{
	Task VerifyNewTasksAsync(CancellationToken cancellationToken = default);

	Task RequestTransferAsync(int taskId, string name, byte[] db, CancellationToken cancellationToken = default);

	Task OnErrorAsync(int taskId, string reason, CancellationToken cancellationToken = default);

	Task OnOkAsync(int taskId, CancellationToken cancellationToken = default);

	Task OnTaskStopAsync(int taskId, string reason, CancellationToken cancellationToken = default);
}

public sealed class PlayerTransferService : IPlayerTransferService
{
	private readonly ConcurrentDictionary<int, PlayerTransferRequest> _transfers = new();
	private readonly ConcurrentDictionary<int, PlayerTransferTask> _tasks = new();
	private readonly IPlayerTransferRepository _repository;
	private readonly IGameServerRegistry _gameServerRegistry;
	private readonly IAccountRepository _accountRepository;
	private readonly ILogger<PlayerTransferService> _logger;

	public PlayerTransferService(
		IPlayerTransferRepository repository,
		IGameServerRegistry gameServerRegistry,
		IAccountRepository accountRepository,
		ILogger<PlayerTransferService> logger)
	{
		_repository = repository;
		_gameServerRegistry = gameServerRegistry;
		_accountRepository = accountRepository;
		_logger = logger;
	}

	public async Task VerifyNewTasksAsync(CancellationToken cancellationToken = default)
	{
		var newTasks = await _repository.GetNewAsync(cancellationToken);
		_logger.LogInformation("PlayerTransfer perform task init. {Count} new tasks.", newTasks.Count);
		foreach (var task in newTasks)
		{
			var sourceServer = _gameServerRegistry.GetGameServer(task.SourceServerId);
			var targetServer = _gameServerRegistry.GetGameServer(task.TargetServerId);

			if (sourceServer == null || !sourceServer.IsOnline)
			{
				_logger.LogError("cannot perform transfer task #{TaskId} while source server is down #{ServerId}", task.Id, task.SourceServerId);
				continue;
			}

			if (targetServer == null || !targetServer.IsOnline)
			{
				_logger.LogError("cannot perform transfer task #{TaskId} while target server is down #{ServerId}", task.Id, task.TargetServerId);
				continue;
			}

			if (sourceServer.IsAccountOnGameServer(task.SourceAccountId))
			{
				_logger.LogError("cannot perform transfer task #{TaskId} while source account is online {AccountId}", task.Id, task.SourceAccountId);
				continue;
			}

			if (targetServer.IsAccountOnGameServer(task.TargetAccountId))
			{
				_logger.LogError("cannot perform transfer task #{TaskId} while target account is online {AccountId}", task.Id, task.TargetAccountId);
				continue;
			}

			task.Status = PlayerTransferTask.StatusActive;
			_tasks[task.Id] = task;
			await _repository.UpdateAsync(task, cancellationToken);
			await _gameServerRegistry.SendPacketToGameServerAsync(task.SourceServerId, new SmPlayerTransferResponse(PlayerTransferResultStatus.PerformAction, task));
		}
	}

	public async Task RequestTransferAsync(int taskId, string name, byte[] db, CancellationToken cancellationToken = default)
	{
		if (!_tasks.TryGetValue(taskId, out var task))
		{
			_logger.LogError("Player transfer task #{TaskId} is not active.", taskId);
			return;
		}

		var targetServer = _gameServerRegistry.GetGameServer(task.TargetServerId);
		var sourceServer = _gameServerRegistry.GetGameServer(task.SourceServerId);
		if (targetServer == null || !targetServer.IsOnline || sourceServer == null || !sourceServer.IsOnline)
		{
			_logger.LogError("Player transfer requests offline server for task #{TaskId}", taskId);
			return;
		}

		if (targetServer.IsAccountOnGameServer(task.TargetAccountId))
		{
			await _gameServerRegistry.SendPacketToGameServerAsync(
				task.SourceServerId,
				new SmPlayerTransferResponse(PlayerTransferResultStatus.Error, taskId, "transfer cant be performed while target account is online at server"));
			return;
		}

		if (_transfers.ContainsKey(taskId))
		{
			await _gameServerRegistry.SendPacketToGameServerAsync(
				task.SourceServerId,
				new SmPlayerTransferResponse(PlayerTransferResultStatus.Error, taskId, "transfer cant be performed while it is already active"));
			return;
		}

		var targetAccount = await _accountRepository.GetAccountByIdAsync(task.TargetAccountId, useExternalAuth: false, cancellationToken);
		var sourceAccount = await _accountRepository.GetAccountByIdAsync(task.SourceAccountId, useExternalAuth: false, cancellationToken);
		if (targetAccount == null || sourceAccount == null)
		{
			await _gameServerRegistry.SendPacketToGameServerAsync(task.SourceServerId, new SmPlayerTransferResponse(PlayerTransferResultStatus.Error, taskId, "transfer account load failed"));
			return;
		}

		var request = new PlayerTransferRequest
		{
			ServerId = task.SourceServerId,
			TargetServerId = task.TargetServerId,
			TargetAccountId = task.TargetAccountId,
			Db = db,
			Name = name,
			TargetAccount = targetAccount,
			Account = targetAccount,
			SourceAccount = sourceAccount,
			TaskId = taskId,
		};
		_transfers[taskId] = request;

		targetAccount.Activated = 0;
		sourceAccount.Activated = 0;
		await _accountRepository.UpdateAccountAsync(targetAccount, useExternalAuth: false, cancellationToken);
		await _accountRepository.UpdateAccountAsync(sourceAccount, useExternalAuth: false, cancellationToken);

		await _gameServerRegistry.SendPacketToGameServerAsync(task.TargetServerId, new SmPlayerTransferResponse(PlayerTransferResultStatus.SendInfo, request));
	}

	public async Task OnTaskStopAsync(int taskId, string reason, CancellationToken cancellationToken = default)
	{
		if (!_tasks.TryRemove(taskId, out var task))
			return;

		task.Status = PlayerTransferTask.StatusError;
		task.Comment = reason;
		await _repository.UpdateAsync(task, cancellationToken);
	}

	public async Task OnErrorAsync(int taskId, string reason, CancellationToken cancellationToken = default)
	{
		_transfers.TryRemove(taskId, out var request);
		if (!_tasks.TryRemove(taskId, out var task))
			return;

		task.Status = PlayerTransferTask.StatusError;
		task.Comment = reason;
		await _repository.UpdateAsync(task, cancellationToken);

		if (request != null)
		{
			await ReactivateAccountsAsync(request, cancellationToken);
			await _gameServerRegistry.SendPacketToGameServerAsync(request.TargetServerId, new SmPlayerTransferResponse(PlayerTransferResultStatus.Error, taskId, reason));
		}
	}

	public async Task OnOkAsync(int taskId, CancellationToken cancellationToken = default)
	{
		if (!_transfers.TryRemove(taskId, out var request))
			return;
		if (!_tasks.TryRemove(taskId, out var task))
			return;

		task.Status = PlayerTransferTask.StatusDone;
		task.Comment = "task done";
		await _repository.UpdateAsync(task, cancellationToken);
		await ReactivateAccountsAsync(request, cancellationToken);
		await _gameServerRegistry.SendPacketToGameServerAsync(request.ServerId, new SmPlayerTransferResponse(PlayerTransferResultStatus.Ok, request));
	}

	private async Task ReactivateAccountsAsync(PlayerTransferRequest request, CancellationToken cancellationToken)
	{
		if (request.Account != null)
		{
			request.Account.Activated = 1;
			await _accountRepository.UpdateAccountAsync(request.Account, useExternalAuth: false, cancellationToken);
		}

		if (request.SourceAccount != null)
		{
			request.SourceAccount.Activated = 1;
			await _accountRepository.UpdateAccountAsync(request.SourceAccount, useExternalAuth: false, cancellationToken);
		}
	}
}
