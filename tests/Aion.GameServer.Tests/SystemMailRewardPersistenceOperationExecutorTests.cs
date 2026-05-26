using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class SystemMailRewardPersistenceOperationExecutorTests
{
	[Fact]
	public async Task ExecuteAsync_StoresSystemMailLetterThroughMailRepository()
	{
		var repository = new RecordingMailRepository();
		var executor = new SystemMailRewardPersistenceOperationExecutor(repository);
		var operation = CreatePlan(SystemMailRecipientRuntimeState.Offline)
			.Operations
			.Single(operation => operation.Kind == SystemMailRewardPersistenceOperationKind.StoreLetter);

		var result = await executor.ExecuteAsync(operation);

		Assert.True(result);
		Assert.Equal(9001, repository.StoredSystemMail?.Id);
		Assert.Null(repository.StoredAttachedItem);
		Assert.Null(repository.OfflineMailboxCounter);
	}

	[Fact]
	public async Task ExecuteAsync_StoresSystemMailAttachedItemThroughMailRepository()
	{
		var repository = new RecordingMailRepository();
		var executor = new SystemMailRewardPersistenceOperationExecutor(repository);
		var operation = CreatePlan(SystemMailRecipientRuntimeState.Offline)
			.Operations
			.Single(operation => operation.Kind == SystemMailRewardPersistenceOperationKind.StoreAttachedItem);

		var result = await executor.ExecuteAsync(operation);

		Assert.True(result);
		Assert.Equal(9101, repository.StoredAttachedItem?.ObjectId);
		Assert.Equal(4701, repository.StoredAttachedItemRecipientId);
		Assert.Null(repository.OfflineMailboxCounter);
	}

	[Fact]
	public async Task ExecuteAsync_UpdatesOfflineMailboxCounterThroughMailRepository()
	{
		var repository = new RecordingMailRepository();
		var executor = new SystemMailRewardPersistenceOperationExecutor(repository);
		var operation = CreatePlan(SystemMailRecipientRuntimeState.Offline, mailboxLetters: 4)
			.Operations
			.Single(operation => operation.Kind == SystemMailRewardPersistenceOperationKind.UpdateOfflineMailboxCounter);

		var result = await executor.ExecuteAsync(operation);

		Assert.True(result);
		Assert.Equal(("Mailreward", 5), repository.OfflineMailboxCounter);
	}

	[Fact]
	public async Task ExecuteAsync_UsesConnectionRegistryForOnlineMailboxFanoutOnlyOnce()
	{
		var repository = new RecordingMailRepository();
		var registry = new RecordingConnectionRegistry();
		var executor = new SystemMailRewardPersistenceOperationExecutor(repository, registry);
		var plan = CreatePlan(SystemMailRecipientRuntimeState.Online(Player.MailboxExpressState));
		var onlineOperations = plan.Operations
			.Where(operation => operation.Kind is
				SystemMailRewardPersistenceOperationKind.PutLetterToOnlineMailbox or
				SystemMailRewardPersistenceOperationKind.SendMailboxStatePacket or
				SystemMailRewardPersistenceOperationKind.SendMailListPackets or
				SystemMailRewardPersistenceOperationKind.SendPostmanNotify)
			.ToArray();

		foreach (var operation in onlineOperations)
			Assert.True(await executor.ExecuteAsync(operation));

		Assert.Single(registry.MailNotifications);
		Assert.Equal((4701, 9001), registry.MailNotifications.Single());
	}

	private static SystemMailRewardPersistencePlan CreatePlan(
		SystemMailRecipientRuntimeState recipientState,
		int mailboxLetters = 0)
	{
		var player = new Player
		{
			ObjectId = 4701,
			AccountId = 3301,
			Name = "Mailreward",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Mailbox = Enumerable.Range(1, mailboxLetters)
				.Select(index => new PlayerMail(index, 4701, "sender", "title", "message", true, 0, 0, 0, 0, DateTime.UnixEpoch))
				.ToArray(),
		};
		var descriptor = new CustomLevelRewardDescriptor(
			new CustomLevelRewardItem(186000242, 15),
			CustomLevelRewardDescriptorStatus.PlannedSystemMail,
			"BonusPackService.addPlayerCustomReward -> SystemMailService.sendMail",
			TemplateRace: null);
		var mailPlan = SystemMailRewardPlanService.CreatePlan(
			player,
			descriptor,
			mailObjectId: 9001,
			attachedItemObjectId: 9101,
			DateTime.UnixEpoch,
			new ItemTemplateTable(
			[
				new ItemTemplateSummary(186000242, "Item 186000242", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 100, 0, 0),
			]));

		return SystemMailRewardPersistencePlanService.CreatePlan(mailPlan, recipientState);
	}

	private sealed class RecordingMailRepository : IMailRepository
	{
		public PlayerMail? StoredSystemMail { get; private set; }

		public InventoryItem? StoredAttachedItem { get; private set; }

		public int? StoredAttachedItemRecipientId { get; private set; }

		public (string RecipientName, int MailboxLetters)? OfflineMailboxCounter { get; private set; }

		public Task<MailRecipientInfo?> LoadRecipientAsync(string recipientName, CancellationToken cancellationToken = default)
		{
			return Task.FromResult<MailRecipientInfo?>(null);
		}

		public Task<bool> IsBlockedByRecipientAsync(int recipientObjectId, int senderObjectId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(false);
		}

		public Task<bool> StoreSentMailAsync(PlayerMail mail, int? senderKinahObjectId, long senderKinahCount, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(false);
		}

		public Task<bool> StoreSentItemMailAsync(
			PlayerMail mail,
			int senderKinahObjectId,
			long senderKinahCount,
			InventoryItem attachedItem,
			int? reducedSenderItemObjectId,
			long reducedSenderItemCount,
			IReadOnlyList<InventoryItem> senderItemUpdates,
			IReadOnlyList<int> senderItemDeletes,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(false);
		}

		public Task<bool> StoreSystemMailLetterAsync(PlayerMail mail, CancellationToken cancellationToken = default)
		{
			StoredSystemMail = mail;
			return Task.FromResult(true);
		}

		public Task<bool> StoreSystemMailAttachedItemAsync(
			InventoryItem attachedItem,
			int recipientObjectId,
			CancellationToken cancellationToken = default)
		{
			StoredAttachedItem = attachedItem;
			StoredAttachedItemRecipientId = recipientObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> UpdateOfflineMailboxCounterAsync(
			string recipientName,
			int mailboxLetters,
			CancellationToken cancellationToken = default)
		{
			OfflineMailboxCounter = (recipientName, mailboxLetters);
			return Task.FromResult(true);
		}

		public Task MarkMailReadAsync(int letterId, CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}

		public Task ClearAttachedItemAsync(int letterId, int itemObjectId, int playerObjectId, CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}

		public Task ClearAttachedKinahAsync(int letterId, CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}

		public Task DeleteLettersAsync(IReadOnlyList<int> letterIds, CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}
	}

	private sealed class RecordingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<(int RecipientObjectId, int MailId)> MailNotifications { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			return Task.FromResult(false);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			MailNotifications.Add((recipientObjectId, mail.Id));
			return Task.FromResult(true);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}
}
