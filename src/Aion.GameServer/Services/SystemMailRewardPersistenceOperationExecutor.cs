using Aion.GameServer.Data;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public sealed class SystemMailRewardPersistenceOperationExecutor : ISystemMailRewardPersistenceOperationExecutor
{
	private readonly IMailRepository _mailRepository;
	private readonly IGameClientConnectionRegistry? _connectionRegistry;

	public SystemMailRewardPersistenceOperationExecutor(
		IMailRepository mailRepository,
		IGameClientConnectionRegistry? connectionRegistry = null)
	{
		_mailRepository = mailRepository;
		_connectionRegistry = connectionRegistry;
	}

	public Task<bool> ExecuteAsync(SystemMailRewardPersistenceOperation operation, CancellationToken cancellationToken = default)
	{
		// Java parity: services/mail/SystemMailService.sendMail delegates DAO writes before updateRecipientMailbox fanout.
		return operation.Kind switch
		{
			SystemMailRewardPersistenceOperationKind.StoreLetter => StoreLetterAsync(operation, cancellationToken),
			SystemMailRewardPersistenceOperationKind.StoreAttachedItem => StoreAttachedItemAsync(operation, cancellationToken),
			SystemMailRewardPersistenceOperationKind.UpdateOfflineMailboxCounter => UpdateOfflineMailboxCounterAsync(operation, cancellationToken),
			SystemMailRewardPersistenceOperationKind.PutLetterToOnlineMailbox => NotifyOnlineRecipientAsync(operation),
			SystemMailRewardPersistenceOperationKind.SendMailboxStatePacket
				or SystemMailRewardPersistenceOperationKind.SendMailListPackets
				or SystemMailRewardPersistenceOperationKind.SendPostmanNotify => Task.FromResult(true),
			_ => Task.FromResult(false),
		};
	}

	private Task<bool> StoreLetterAsync(
		SystemMailRewardPersistenceOperation operation,
		CancellationToken cancellationToken)
	{
		return operation.MailPayload == null
			? Task.FromResult(false)
			: _mailRepository.StoreSystemMailLetterAsync(operation.MailPayload, cancellationToken);
	}

	private Task<bool> StoreAttachedItemAsync(
		SystemMailRewardPersistenceOperation operation,
		CancellationToken cancellationToken)
	{
		if (operation.AttachedItemPayload == null || !operation.RecipientObjectId.HasValue)
			return Task.FromResult(false);

		return _mailRepository.StoreSystemMailAttachedItemAsync(
			operation.AttachedItemPayload,
			operation.RecipientObjectId.Value,
			cancellationToken);
	}

	private Task<bool> UpdateOfflineMailboxCounterAsync(
		SystemMailRewardPersistenceOperation operation,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(operation.RecipientName) || !operation.MailboxLettersAfterOperation.HasValue)
			return Task.FromResult(false);

		return _mailRepository.UpdateOfflineMailboxCounterAsync(
			operation.RecipientName,
			operation.MailboxLettersAfterOperation.Value,
			cancellationToken);
	}

	private Task<bool> NotifyOnlineRecipientAsync(SystemMailRewardPersistenceOperation operation)
	{
		if (_connectionRegistry == null || operation.MailPayload == null || !operation.RecipientObjectId.HasValue)
			return Task.FromResult(false);

		// The registry method performs the Java online branch as one packet fanout unit:
		// mailbox insertion, SM_MAIL_SERVICE, optional mail-list refresh, and optional STR_POSTMAN_NOTIFY.
		return _connectionRegistry.NotifyMailReceivedAsync(operation.RecipientObjectId.Value, operation.MailPayload);
	}
}
