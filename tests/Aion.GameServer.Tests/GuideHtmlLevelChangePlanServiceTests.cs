using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class GuideHtmlLevelChangePlanServiceTests
{
	[Fact]
	public void CreatePlan_StagesJavaGuideTemplateOrderAndPersistenceIntent()
	{
		var player = CreatePlayer();
		var templates = new[]
		{
			new GuideHtmlTemplateSummary("General level 10", 10, null, null),
			new GuideHtmlTemplateSummary("Race level 10", 10, null, "ELYOS"),
			new GuideHtmlTemplateSummary("Class level 10", 10, "RANGER", null),
			new GuideHtmlTemplateSummary("Class race level 10", 10, "RANGER", "ELYOS"),
			new GuideHtmlTemplateSummary("Inactive class race level 10", 10, "RANGER", "ELYOS", IsActivated: false),
			new GuideHtmlTemplateSummary("Class race level 11", 11, "RANGER", "ELYOS", RewardCount: 1, Surveys: [new GuideHtmlSurveySummary(186000001, 2)]),
			new GuideHtmlTemplateSummary("Wrong race", 10, "RANGER", "ASMODIANS"),
			new GuideHtmlTemplateSummary("Wrong class", 10, "ASSASSIN", "ELYOS"),
		};

		var plan = GuideHtmlLevelChangePlanService.CreatePlan(
			player,
			guidesEnabled: true,
			isSpawned: true,
			fromLevel: 10,
			toLevel: 11,
			templates);

		Assert.Equal(GuideHtmlLevelChangePlanStatus.Planned, plan.Status);
		Assert.True(plan.Applied);
		Assert.Equal(4501, plan.ObjectId);
		Assert.Equal("RANGER", plan.PlayerClass);
		Assert.Equal("ELYOS", plan.Race);
		Assert.Equal(10, plan.FromLevel);
		Assert.Equal(11, plan.ToLevel);
		Assert.Equal(5, plan.PlannedGuideCount);
		Assert.Equal(
		[
			"Class race level 10",
			"Inactive class race level 10",
			"Class level 10",
			"Race level 10",
			"General level 10",
			"Class race level 11",
		], plan.Descriptors.Select(descriptor => descriptor.Title));
		Assert.Equal(GuideHtmlLevelChangeDescriptorStatus.SkippedInactive, plan.Descriptors[1].Status);
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Contains(
			plan.Descriptors,
			descriptor => descriptor.Status == GuideHtmlLevelChangeDescriptorStatus.PlannedSendAndPersist
				&& descriptor.Notes!.Contains("SM_QUESTIONNAIRE", StringComparison.Ordinal)
				&& descriptor.Notes.Contains("GuideDAO.saveGuide", StringComparison.Ordinal));
		Assert.Equal(1, plan.Descriptors.Single(descriptor => descriptor.Title == "Class race level 11").Template?.RewardCount);
	}

	[Fact]
	public void CreatePlan_RecordsJavaConfigSpawnedMissingRangeAndInactiveBranches()
	{
		var player = CreatePlayer();
		var inactiveTemplates = new[]
		{
			new GuideHtmlTemplateSummary("Inactive", 10, "RANGER", "ELYOS", IsActivated: false),
		};

		var disabled = GuideHtmlLevelChangePlanService.CreatePlan(player, guidesEnabled: false, isSpawned: true, 10, 10, inactiveTemplates);
		var notSpawned = GuideHtmlLevelChangePlanService.CreatePlan(player, guidesEnabled: true, isSpawned: false, 10, 10, inactiveTemplates);
		var emptyRange = GuideHtmlLevelChangePlanService.CreatePlan(player, guidesEnabled: true, isSpawned: true, 11, 10, inactiveTemplates);
		var missingTemplates = GuideHtmlLevelChangePlanService.CreatePlan(player, guidesEnabled: true, isSpawned: true, 10, 10, null);
		var noMatches = GuideHtmlLevelChangePlanService.CreatePlan(
			player,
			guidesEnabled: true,
			isSpawned: true,
			10,
			10,
			[new GuideHtmlTemplateSummary("Wrong level", 12, "RANGER", "ELYOS")]);
		var inactiveOnly = GuideHtmlLevelChangePlanService.CreatePlan(player, guidesEnabled: true, isSpawned: true, 10, 10, inactiveTemplates);
		var missingPlayer = GuideHtmlLevelChangePlanService.CreatePlan(null, guidesEnabled: true, isSpawned: true, 10, 10, inactiveTemplates);

		Assert.Equal(GuideHtmlLevelChangePlanStatus.SkippedGuidesDisabled, disabled.Status);
		Assert.Equal(GuideHtmlLevelChangePlanStatus.SkippedNotSpawned, notSpawned.Status);
		Assert.Equal(GuideHtmlLevelChangePlanStatus.EmptyLevelRange, emptyRange.Status);
		Assert.Equal(GuideHtmlLevelChangePlanStatus.BlockedMissingGuideTemplates, missingTemplates.Status);
		Assert.Equal(GuideHtmlLevelChangePlanStatus.NoMatchingTemplates, noMatches.Status);
		Assert.Equal(GuideHtmlLevelChangePlanStatus.NoActivatedTemplates, inactiveOnly.Status);
		Assert.Equal(GuideHtmlLevelChangePlanStatus.MissingPlayer, missingPlayer.Status);
		Assert.Empty(disabled.Descriptors);
		Assert.Empty(notSpawned.Descriptors);
		Assert.Empty(emptyRange.Descriptors);
		Assert.Empty(missingTemplates.Descriptors);
		Assert.Empty(noMatches.Descriptors);
		Assert.Single(inactiveOnly.Descriptors);
		Assert.Equal(GuideHtmlLevelChangeDescriptorStatus.SkippedInactive, inactiveOnly.Descriptors[0].Status);
		Assert.False(inactiveOnly.Applied);
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 4501,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 10,
			Exp = 9_000,
			IsOnline = true,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			AbyssRank = PlayerAbyssRank.Default(),
		};
	}
}
