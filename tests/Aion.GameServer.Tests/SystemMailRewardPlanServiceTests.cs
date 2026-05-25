using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SystemMailRewardPlanServiceTests
{
	[Fact]
	public void CreatePlan_ShapesCustomRewardExpressMailWithoutSending()
	{
		var player = CreatePlayer();
		var descriptor = new CustomLevelRewardDescriptor(
			new CustomLevelRewardItem(186000242, 15),
			CustomLevelRewardDescriptorStatus.PlannedSystemMail,
			"BonusPackService.addPlayerCustomReward -> SystemMailService.sendMail",
			TemplateRace: null);

		var plan = SystemMailRewardPlanService.CreatePlan(
			player,
			descriptor,
			mailObjectId: 9001,
			attachedItemObjectId: 9101,
			new DateTime(2026, 5, 25, 9, 0, 0),
			CreateTemplates(186000242));

		Assert.Equal(SystemMailRewardPlanStatus.Planned, plan.Status);
		Assert.True(plan.Applied);
		Assert.False(plan.IsLive);
		Assert.Equal(player.ObjectId, plan.RecipientObjectId);
		var mail = Assert.IsType<PlayerMail>(plan.Mail);
		Assert.Equal(9001, mail.Id);
		Assert.Equal(player.ObjectId, mail.RecipientId);
		Assert.Equal("Beyond Aion", mail.SenderName);
		Assert.Equal("Bonus Pack", mail.Title);
		Assert.True(mail.IsUnreadExpress);
		Assert.Equal(9101, mail.AttachedItemObjectId);
		Assert.Equal(186000242, mail.AttachedItemTemplateId);
		Assert.Equal(0, mail.AttachedKinah);
		Assert.NotNull(mail.AttachedItem);
		Assert.Equal(9101, mail.AttachedItem.ObjectId);
		Assert.Equal(186000242, mail.AttachedItem.ItemId);
		Assert.Equal(15, mail.AttachedItem.Count);
		Assert.Equal(player.ObjectId, mail.AttachedItem.OwnerId);
		Assert.False(mail.AttachedItem.IsEquipped);
		Assert.Equal(SystemMailRewardPlanService.MailboxStorageId, mail.AttachedItem.Location);
		Assert.Equal(0, mail.AttachedItem.Slot);
		Assert.Empty(player.Mailbox);
	}

	[Fact]
	public void CreatePlan_ShapesStarterKitMailAndTruncatesJavaTitleAndMessageLimits()
	{
		var player = CreatePlayer();
		var descriptor = new StarterKitLevelChangeDescriptor(
			20,
			new StarterKitRewardItem(188054100, 1),
			StarterKitLevelChangeDescriptorStatus.PlannedSystemMail,
			"StarterKitService.onLevelUp -> SystemMailService.sendMail");
		var request = new SystemMailRewardRequest(
			descriptor.Sender,
			"1234567890123456789012345",
			new string('m', 1005),
			descriptor.Reward.ItemId,
			descriptor.Reward.Count,
			AttachedKinah: 0,
			descriptor.LetterType,
			descriptor.JavaSource);

		var plan = SystemMailRewardPlanService.CreatePlan(
			player,
			request,
			mailObjectId: 9002,
			attachedItemObjectId: 9102,
			DateTime.UnixEpoch,
			CreateTemplates(188054100));

		var mail = Assert.IsType<PlayerMail>(plan.Mail);
		Assert.Equal(20, mail.Title.Length);
		Assert.Equal("12345678901234567890", mail.Title);
		Assert.Equal(1000, mail.Message.Length);
		Assert.Equal(1, mail.LetterType);
		Assert.Contains("StarterKitService.onLevelUp", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_RecordsJavaSystemMailGuardBranches()
	{
		var player = CreatePlayer();
		var descriptor = new CustomLevelRewardDescriptor(
			new CustomLevelRewardItem(186000242, 15),
			CustomLevelRewardDescriptorStatus.PlannedSystemMail,
			"BonusPackService.addPlayerCustomReward -> SystemMailService.sendMail",
			TemplateRace: null);
		var badCountRequest = new SystemMailRewardRequest("Beyond Aion", "Bonus Pack", "Body", 186000242, 0, 0, "EXPRESS", descriptor.JavaSource);
		var longSenderRequest = new SystemMailRewardRequest("0123456789abcdefg", "Bonus Pack", "Body", 0, 0, 0, "EXPRESS", descriptor.JavaSource);
		var longSystemSenderRequest = longSenderRequest with { Sender = "$$0123456789abcdefg" };
		var longRecipient = CreatePlayer(name: "SeventeenLettersX");
		var fullMailbox = CreatePlayer(mailboxLetters: 200);

		Assert.Equal(
			SystemMailRewardPlanStatus.MissingRecipient,
			SystemMailRewardPlanService.CreatePlan(null, descriptor, 1, 2, DateTime.UnixEpoch, CreateTemplates(186000242)).Status);
		Assert.Equal(
			SystemMailRewardPlanStatus.InvalidAttachedItemCount,
			SystemMailRewardPlanService.CreatePlan(player, badCountRequest, 1, 2, DateTime.UnixEpoch, CreateTemplates(186000242)).Status);
		Assert.Equal(
			SystemMailRewardPlanStatus.MissingItemTemplate,
			SystemMailRewardPlanService.CreatePlan(player, descriptor, 1, 2, DateTime.UnixEpoch, itemTemplates: null).Status);
		Assert.Equal(
			SystemMailRewardPlanStatus.RecipientNameTooLong,
			SystemMailRewardPlanService.CreatePlan(longRecipient, descriptor, 1, 2, DateTime.UnixEpoch, CreateTemplates(186000242)).Status);
		Assert.Equal(
			SystemMailRewardPlanStatus.SenderNameTooLong,
			SystemMailRewardPlanService.CreatePlan(player, longSenderRequest, 1, 2, DateTime.UnixEpoch, itemTemplates: null).Status);
		Assert.Equal(
			SystemMailRewardPlanStatus.Planned,
			SystemMailRewardPlanService.CreatePlan(player, longSystemSenderRequest, 1, 0, DateTime.UnixEpoch, itemTemplates: null).Status);
		Assert.Equal(
			SystemMailRewardPlanStatus.RecipientMailboxFull,
			SystemMailRewardPlanService.CreatePlan(fullMailbox, descriptor, 1, 2, DateTime.UnixEpoch, CreateTemplates(186000242)).Status);
	}

	private static Player CreatePlayer(string name = "Mailreward", int mailboxLetters = 0)
	{
		return new Player
		{
			ObjectId = 4701,
			AccountId = 3301,
			Name = name,
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
