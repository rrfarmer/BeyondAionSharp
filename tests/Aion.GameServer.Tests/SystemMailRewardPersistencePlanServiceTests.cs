using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SystemMailRewardPersistencePlanServiceTests
{
	[Fact]
	public void CreatePlan_StagesOfflineItemMailPersistenceInJavaFailureOrder()
	{
		var mailPlan = CreateMailPlan(mailboxLetters: 3);

		var plan = SystemMailRewardPersistencePlanService.CreatePlan(
			mailPlan,
			SystemMailRecipientRuntimeState.Offline);

		Assert.Equal(SystemMailRewardPersistencePlanStatus.Planned, plan.Status);
		Assert.False(plan.IsLive);
		Assert.Equal(
			[
				SystemMailRewardPersistenceOperationKind.StoreLetter,
				SystemMailRewardPersistenceOperationKind.StoreAttachedItem,
				SystemMailRewardPersistenceOperationKind.UpdateOfflineMailboxCounter,
			],
			plan.Operations.Select(operation => operation.Kind).ToArray());

		var storeLetter = plan.Operations[0];
		Assert.True(storeLetter.StopsOnFailure);
		Assert.Equal(SystemMailRewardPersistencePlanService.JavaMailInsertSql, storeLetter.Sql);
		Assert.Equal(
			[
				"mail_unique_id",
				"mail_recipient_id",
				"sender_name",
				"mail_title",
				"mail_message",
				"unread",
				"attached_item_id",
				"attached_kinah_count",
				"express",
				"recieved_time",
			],
			storeLetter.ParameterOrder);
		Assert.Equal(9001, storeLetter.MailObjectId);
		Assert.Equal(4701, storeLetter.RecipientObjectId);
		Assert.Equal(9101, storeLetter.AttachedItemObjectId);

		var storeItem = plan.Operations[1];
		Assert.True(storeItem.StopsOnFailure);
		Assert.Equal(SystemMailRewardPersistencePlanService.JavaInventoryInsertSql, storeItem.Sql);
		Assert.Equal("item_unique_id", storeItem.ParameterOrder[0]);
		Assert.Equal("rnd_plume_bonus", storeItem.ParameterOrder[^1]);
		Assert.Equal(9101, storeItem.AttachedItemObjectId);

		var updateCounter = plan.Operations[2];
		Assert.Equal(SystemMailRewardPersistencePlanService.JavaOfflineMailboxCounterSql, updateCounter.Sql);
		Assert.Equal(["mailbox_letters", "name"], updateCounter.ParameterOrder);
		Assert.Equal("Mailreward", updateCounter.RecipientName);
		Assert.Equal(4, updateCounter.MailboxLettersAfterOperation);
	}

	[Fact]
	public void CreatePlan_StagesOnlineExpressMailboxFanoutAfterPersistence()
	{
		var mailPlan = CreateMailPlan(mailboxLetters: 7);

		var plan = SystemMailRewardPersistencePlanService.CreatePlan(
			mailPlan,
			SystemMailRecipientRuntimeState.Online(Player.MailboxExpressState));

		Assert.Equal(
			[
				SystemMailRewardPersistenceOperationKind.StoreLetter,
				SystemMailRewardPersistenceOperationKind.StoreAttachedItem,
				SystemMailRewardPersistenceOperationKind.PutLetterToOnlineMailbox,
				SystemMailRewardPersistenceOperationKind.SendMailboxStatePacket,
				SystemMailRewardPersistenceOperationKind.SendMailListPackets,
				SystemMailRewardPersistenceOperationKind.SendPostmanNotify,
			],
			plan.Operations.Select(operation => operation.Kind).ToArray());

		Assert.Equal(8, plan.Operations[2].MailboxLettersAfterOperation);
		Assert.True(plan.Operations[4].ExpressOnly);
		Assert.Equal(4701, plan.Operations[5].RecipientObjectId);
		Assert.Contains("STR_POSTMAN_NOTIFY", plan.Operations[5].JavaArtifact, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_SkipsUnplannedMailAndDoesNotStageDaoWork()
	{
		var player = CreatePlayer();
		var descriptor = new CustomLevelRewardDescriptor(
			new CustomLevelRewardItem(186000242, 0),
			CustomLevelRewardDescriptorStatus.PlannedSystemMail,
			"BonusPackService.addPlayerCustomReward -> SystemMailService.sendMail",
			TemplateRace: null);
		var mailPlan = SystemMailRewardPlanService.CreatePlan(
			player,
			descriptor,
			mailObjectId: 9001,
			attachedItemObjectId: 9101,
			DateTime.UnixEpoch,
			CreateTemplates(186000242));

		var persistencePlan = SystemMailRewardPersistencePlanService.CreatePlan(
			mailPlan,
			SystemMailRecipientRuntimeState.Offline);

		Assert.Equal(SystemMailRewardPersistencePlanStatus.SkippedMailNotPlanned, persistencePlan.Status);
		Assert.False(persistencePlan.Applied);
		Assert.Empty(persistencePlan.Operations);
		Assert.Contains("skipped before DAO", persistencePlan.JavaSource, StringComparison.Ordinal);
	}

	private static SystemMailRewardPlan CreateMailPlan(int mailboxLetters)
	{
		var player = CreatePlayer(mailboxLetters);
		var descriptor = new CustomLevelRewardDescriptor(
			new CustomLevelRewardItem(186000242, 15),
			CustomLevelRewardDescriptorStatus.PlannedSystemMail,
			"BonusPackService.addPlayerCustomReward -> SystemMailService.sendMail",
			TemplateRace: null);

		return SystemMailRewardPlanService.CreatePlan(
			player,
			descriptor,
			mailObjectId: 9001,
			attachedItemObjectId: 9101,
			new DateTime(2026, 5, 25, 9, 0, 0),
			CreateTemplates(186000242));
	}

	private static Player CreatePlayer(int mailboxLetters = 0)
	{
		return new Player
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
	}

	private static ItemTemplateTable CreateTemplates(params int[] itemIds)
	{
		return new ItemTemplateTable(itemIds
			.Select(itemId => new ItemTemplateSummary(itemId, $"Item {itemId}", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 100, 0, 0))
			.ToArray());
	}
}
