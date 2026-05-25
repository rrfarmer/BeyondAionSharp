using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CustomLevelRewardPlanServiceTests
{
	[Fact]
	public void CreateBonusPackPlan_StagesJavaBonusPackMailRewards()
	{
		var player = CreatePlayer(level: 65, mailboxLetters: 91);

		var plan = CustomLevelRewardPlanService.CreateBonusPackPlan(
			player,
			receivedPlayerId: 0,
			storeReceivingPlayerSucceeded: true);

		Assert.Equal(CustomLevelRewardPackKind.Bonus, plan.Kind);
		Assert.Equal(CustomLevelRewardPlanStatus.Planned, plan.Status);
		Assert.True(plan.Applied);
		Assert.Equal(9, plan.Descriptors.Count);
		Assert.Equal(
		[
			new CustomLevelRewardItem(186000242, 15),
			new CustomLevelRewardItem(186000130, 6500),
			new CustomLevelRewardItem(186000051, 5),
			new CustomLevelRewardItem(166020003, 15),
			new CustomLevelRewardItem(186000236, 250),
			new CustomLevelRewardItem(186000237, 4500),
			new CustomLevelRewardItem(186000409, 150),
			new CustomLevelRewardItem(188052562, 5),
			new CustomLevelRewardItem(190100051, 1),
		], plan.Descriptors.Select(descriptor => descriptor.Reward));
		Assert.All(plan.Descriptors, descriptor =>
		{
			Assert.Equal(CustomLevelRewardDescriptorStatus.PlannedSystemMail, descriptor.Status);
			Assert.False(descriptor.IsLive);
			Assert.Equal("Beyond Aion", descriptor.Sender);
			Assert.Equal("Bonus Pack", descriptor.Title);
			Assert.Equal("EXPRESS", descriptor.LetterType);
			Assert.Contains("first character", descriptor.Body, StringComparison.Ordinal);
			Assert.Contains("BonusPackService.addPlayerCustomReward", descriptor.JavaSource, StringComparison.Ordinal);
		});
	}

	[Fact]
	public void CreateBonusPackPlan_RecordsJavaGuardBranches()
	{
		var player = CreatePlayer(level: 64);
		var fullMailbox = CreatePlayer(level: 65, mailboxLetters: 92);
		var received = CreatePlayer(level: 65);
		var storeFailed = CreatePlayer(level: 65);

		Assert.Equal(CustomLevelRewardPlanStatus.SkippedWrongLevel,
			CustomLevelRewardPlanService.CreateBonusPackPlan(player, receivedPlayerId: 0, storeReceivingPlayerSucceeded: true).Status);
		Assert.Equal(CustomLevelRewardPlanStatus.SkippedMailboxLimit,
			CustomLevelRewardPlanService.CreateBonusPackPlan(fullMailbox, receivedPlayerId: 0, storeReceivingPlayerSucceeded: true).Status);
		Assert.Equal(CustomLevelRewardPlanStatus.SkippedAlreadyReceived,
			CustomLevelRewardPlanService.CreateBonusPackPlan(received, receivedPlayerId: 4701, storeReceivingPlayerSucceeded: true).Status);
		Assert.Equal(CustomLevelRewardPlanStatus.SkippedStoreFailed,
			CustomLevelRewardPlanService.CreateBonusPackPlan(storeFailed, receivedPlayerId: 0, storeReceivingPlayerSucceeded: false).Status);
		Assert.Equal(CustomLevelRewardPlanStatus.MissingPlayer,
			CustomLevelRewardPlanService.CreateBonusPackPlan(null, receivedPlayerId: 0, storeReceivingPlayerSucceeded: true).Status);
	}

	[Fact]
	public void CreateFactionPackPlan_StagesWindowAndOppositeRaceTemplateFiltering()
	{
		var player = CreatePlayer(level: 65, race: "ASMODIANS");
		var itemTemplates = new ItemTemplateTable(
		[
			CreateTemplate(186000236, "PC_ALL"),
			CreateTemplate(162002030, "ELYOS"),
			CreateTemplate(162000023, "PC_ALL"),
			CreateTemplate(166000195, "PC_ALL"),
			CreateTemplate(169630007, "PC_ALL"),
			CreateTemplate(188053526, "PC_ALL"),
		]);

		var plan = CustomLevelRewardPlanService.CreateFactionPackPlan(
			player,
			accountCreationLocalTime: new DateTime(2022, 6, 18, 0, 0, 0),
			receivedPlayerId: 0,
			storeReceivingPlayerSucceeded: true,
			itemTemplates);

		Assert.Equal(CustomLevelRewardPackKind.Faction, plan.Kind);
		Assert.Equal(CustomLevelRewardPlanStatus.Planned, plan.Status);
		Assert.Equal(6, plan.Descriptors.Count);
		Assert.Equal(5, plan.Descriptors.Count(descriptor => descriptor.Status == CustomLevelRewardDescriptorStatus.PlannedSystemMail));
		var skipped = Assert.Single(plan.Descriptors, descriptor => descriptor.Status == CustomLevelRewardDescriptorStatus.SkippedOppositeRaceItem);
		Assert.Equal(new CustomLevelRewardItem(162002030, 250), skipped.Reward);
		Assert.Equal("ELYOS", skipped.TemplateRace);
		Assert.All(plan.Descriptors, descriptor =>
		{
			Assert.Equal("Faction Pack", descriptor.Title);
			Assert.Equal("EXPRESS", descriptor.LetterType);
			Assert.Contains("FactionPackService", descriptor.JavaSource, StringComparison.Ordinal);
		});
	}

	[Fact]
	public void CreateFactionPackPlan_RecordsCreationWindowDaoAndCapacityBranches()
	{
		var elyos = CreatePlayer(level: 65, race: "ELYOS");
		var asmodian = CreatePlayer(level: 65, race: "ASMODIANS");
		var fullMailbox = CreatePlayer(level: 65, race: "ELYOS", mailboxLetters: 95);

		Assert.Equal(CustomLevelRewardPlanStatus.SkippedCreationBeforeWindow,
			CustomLevelRewardPlanService.CreateFactionPackPlan(
				elyos,
				new DateTime(2020, 9, 13, 23, 59, 59),
				receivedPlayerId: 0,
				storeReceivingPlayerSucceeded: true).Status);
		Assert.Equal(CustomLevelRewardPlanStatus.SkippedCreationAfterWindow,
			CustomLevelRewardPlanService.CreateFactionPackPlan(
				asmodian,
				new DateTime(2022, 7, 20, 0, 0, 0),
				receivedPlayerId: 0,
				storeReceivingPlayerSucceeded: true).Status);
		Assert.Equal(CustomLevelRewardPlanStatus.SkippedMailboxLimit,
			CustomLevelRewardPlanService.CreateFactionPackPlan(
				fullMailbox,
				new DateTime(2020, 9, 14, 0, 0, 0),
				receivedPlayerId: 0,
				storeReceivingPlayerSucceeded: true).Status);
		Assert.Equal(CustomLevelRewardPlanStatus.SkippedAlreadyReceived,
			CustomLevelRewardPlanService.CreateFactionPackPlan(
				elyos,
				new DateTime(2020, 9, 14, 0, 0, 0),
				receivedPlayerId: 4701,
				storeReceivingPlayerSucceeded: true).Status);
		Assert.Equal(CustomLevelRewardPlanStatus.SkippedStoreFailed,
			CustomLevelRewardPlanService.CreateFactionPackPlan(
				elyos,
				new DateTime(2020, 9, 14, 0, 0, 0),
				receivedPlayerId: 0,
				storeReceivingPlayerSucceeded: false).Status);
	}

	private static Player CreatePlayer(int level, string race = "ELYOS", int mailboxLetters = 0)
	{
		return new Player
		{
			ObjectId = 4701,
			AccountId = 3301,
			Name = "Customreward",
			Race = race,
			PlayerClass = "RANGER",
			Level = level,
			Mailbox = Enumerable.Range(1, mailboxLetters)
				.Select(index => new PlayerMail(index, 4701, "sender", "title", "message", true, 0, 0, 0, 0, DateTime.UnixEpoch))
				.ToArray(),
		};
	}

	private static ItemTemplateSummary CreateTemplate(int itemId, string race)
	{
		return new ItemTemplateSummary(itemId, $"Item {itemId}", 0, 0, 1, "NONE", "NORMAL", "COMMON", race, 100, 0, 0);
	}
}
