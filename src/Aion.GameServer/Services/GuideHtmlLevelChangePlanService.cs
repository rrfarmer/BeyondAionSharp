using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class GuideHtmlLevelChangePlanService
{
	private const string RaceAll = "PC_ALL";

	public static GuideHtmlLevelChangePlan CreatePlan(
		Player? player,
		bool guidesEnabled,
		bool isSpawned,
		int fromLevel,
		int toLevel,
		IEnumerable<GuideHtmlTemplateSummary>? templates)
	{
		if (player == null)
			return GuideHtmlLevelChangePlan.MissingPlayer(guidesEnabled, isSpawned, fromLevel, toLevel);
		if (!guidesEnabled)
			return GuideHtmlLevelChangePlan.Skipped(player, GuideHtmlLevelChangePlanStatus.SkippedGuidesDisabled, guidesEnabled, isSpawned, fromLevel, toLevel);
		if (!isSpawned)
			return GuideHtmlLevelChangePlan.Skipped(player, GuideHtmlLevelChangePlanStatus.SkippedNotSpawned, guidesEnabled, isSpawned, fromLevel, toLevel);
		if (fromLevel > toLevel)
			return GuideHtmlLevelChangePlan.Skipped(player, GuideHtmlLevelChangePlanStatus.EmptyLevelRange, guidesEnabled, isSpawned, fromLevel, toLevel);
		if (templates == null)
			return GuideHtmlLevelChangePlan.Skipped(player, GuideHtmlLevelChangePlanStatus.BlockedMissingGuideTemplates, guidesEnabled, isSpawned, fromLevel, toLevel);

		var templateList = templates.ToArray();
		var descriptors = new List<GuideHtmlLevelChangeDescriptor>();
		for (var level = fromLevel; level <= toLevel; level++)
		{
			foreach (var template in GetTemplatesFor(templateList, player.PlayerClass.ToString(), player.Race.ToString(), level))
			{
				var descriptorStatus = template.IsActivated
					? GuideHtmlLevelChangeDescriptorStatus.PlannedSendAndPersist
					: GuideHtmlLevelChangeDescriptorStatus.SkippedInactive;
				descriptors.Add(new GuideHtmlLevelChangeDescriptor(
					template.Title,
					level,
					descriptorStatus,
					"HTMLService.sendGuideHtml -> DataManager.GUIDE_HTML_DATA.getTemplatesFor; sendData; GuideDAO.saveGuide",
					template,
					Notes: template.IsActivated
						? "Future live execution must allocate IDFactory id, send SM_QUESTIONNAIRE chunks, and persist GuideDAO.saveGuide."
						: "Java skips templates where GuideTemplate.isActivated() is false."));
			}
		}

		var planned = descriptors.Count(descriptor => descriptor.Status == GuideHtmlLevelChangeDescriptorStatus.PlannedSendAndPersist);
		var planStatus = planned > 0
			? GuideHtmlLevelChangePlanStatus.Planned
			: descriptors.Count == 0
				? GuideHtmlLevelChangePlanStatus.NoMatchingTemplates
				: GuideHtmlLevelChangePlanStatus.NoActivatedTemplates;
		return new GuideHtmlLevelChangePlan(
			planStatus,
			player.ObjectId,
			player.PlayerClass.ToString(),
			player.Race.ToString(),
			guidesEnabled,
			isSpawned,
			fromLevel,
			toLevel,
			planned,
			descriptors);
	}

	private static IEnumerable<GuideHtmlTemplateSummary> GetTemplatesFor(
		IReadOnlyList<GuideHtmlTemplateSummary> templates,
		string playerClass,
		string race,
		int level)
	{
		// Java parity breadcrumb: GuideHtmlData.getTemplatesFor class+race, class+PC_ALL, CLASS_ALL+race, CLASS_ALL+PC_ALL order.
		foreach (var template in templates.Where(template => Matches(template, playerClass, race, level)))
			yield return template;
		foreach (var template in templates.Where(template => Matches(template, playerClass, RaceAll, level)))
			yield return template;
		foreach (var template in templates.Where(template => Matches(template, null, race, level)))
			yield return template;
		foreach (var template in templates.Where(template => Matches(template, null, RaceAll, level)))
			yield return template;
	}

	private static bool Matches(GuideHtmlTemplateSummary template, string? playerClass, string race, int level)
	{
		return template.Level == level
			&& string.Equals(template.PlayerClass, playerClass, StringComparison.Ordinal)
			&& string.Equals(template.Race ?? RaceAll, race, StringComparison.Ordinal);
	}
}

public sealed record GuideHtmlTemplateSummary(
	string Title,
	int Level,
	string? PlayerClass,
	string? Race,
	bool IsActivated = true,
	int RewardCount = 0,
	IReadOnlyList<GuideHtmlSurveySummary>? Surveys = null,
	string Message = "",
	string Select = "",
	string RewardInfo = "");

public sealed record GuideHtmlSurveySummary(int ItemId, long Count);

public sealed record GuideHtmlLevelChangePlan(
	GuideHtmlLevelChangePlanStatus Status,
	int ObjectId,
	string PlayerClass,
	string Race,
	bool GuidesEnabled,
	bool IsSpawned,
	int FromLevel,
	int ToLevel,
	int PlannedGuideCount,
	IReadOnlyList<GuideHtmlLevelChangeDescriptor> Descriptors)
{
	public bool Applied => Status == GuideHtmlLevelChangePlanStatus.Planned;

	public static GuideHtmlLevelChangePlan MissingPlayer(
		bool guidesEnabled,
		bool isSpawned,
		int fromLevel,
		int toLevel)
	{
		return new GuideHtmlLevelChangePlan(
			GuideHtmlLevelChangePlanStatus.MissingPlayer,
			ObjectId: 0,
			PlayerClass: string.Empty,
			Race: string.Empty,
			guidesEnabled,
			isSpawned,
			fromLevel,
			toLevel,
			PlannedGuideCount: 0,
			Array.Empty<GuideHtmlLevelChangeDescriptor>());
	}

	public static GuideHtmlLevelChangePlan Skipped(
		Player player,
		GuideHtmlLevelChangePlanStatus status,
		bool guidesEnabled,
		bool isSpawned,
		int fromLevel,
		int toLevel)
	{
		return new GuideHtmlLevelChangePlan(
			status,
			player.ObjectId,
			player.PlayerClass.ToString(),
			player.Race.ToString(),
			guidesEnabled,
			isSpawned,
			fromLevel,
			toLevel,
			PlannedGuideCount: 0,
			Array.Empty<GuideHtmlLevelChangeDescriptor>());
	}
}

public sealed record GuideHtmlLevelChangeDescriptor(
	string Title,
	int Level,
	GuideHtmlLevelChangeDescriptorStatus Status,
	string JavaSource,
	GuideHtmlTemplateSummary? Template = null,
	bool IsLive = false,
	string? Notes = null);

public enum GuideHtmlLevelChangePlanStatus
{
	Planned,
	SkippedGuidesDisabled,
	SkippedNotSpawned,
	EmptyLevelRange,
	BlockedMissingGuideTemplates,
	NoMatchingTemplates,
	NoActivatedTemplates,
	MissingPlayer,
}

public enum GuideHtmlLevelChangeDescriptorStatus
{
	PlannedSendAndPersist,
	SkippedInactive,
}
