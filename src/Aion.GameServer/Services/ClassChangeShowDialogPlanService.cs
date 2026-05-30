using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum ClassChangeShowDialogPlanStatus
{
	PlanCreated,
	SkippedLevelTooLow,
	SkippedNotStartingClass,
}

public sealed record ClassChangeShowDialogPlan(
	ClassChangeShowDialogPlanStatus Status,
	int PlayerLevel,
	string Race,
	string PlayerClass,
	int DialogPageId,
	int AscensionQuestId,
	SmDialogWindow? DialogWindowPacket,
	bool ShouldSendDialogWindow,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ClassChangeShowDialogPlanService
{
	public const int MinLevelForClassChange = 9;
	public const int ElyosAscensionQuestId = 1006;
	public const int AsmodiansAscensionQuestId = 2008;

	// Java parity: services/ClassChangeService.showClassChangeDialog.
	private static readonly IReadOnlySet<string> StartingClasses =
		new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"WARRIOR", "SCOUT", "MAGE", "PRIEST", "ENGINEER", "ARTIST",
		};

	public static ClassChangeShowDialogPlan CreatePlan(int playerLevel, string race, string playerClass)
	{
		if (playerLevel < MinLevelForClassChange)
		{
			return new ClassChangeShowDialogPlan(
				ClassChangeShowDialogPlanStatus.SkippedLevelTooLow,
				playerLevel, race, playerClass,
				DialogPageId: 0,
				AscensionQuestId: 0,
				DialogWindowPacket: null,
				ShouldSendDialogWindow: false,
				$"ClassChangeService.showClassChangeDialog -> player.getLevel() ({playerLevel}) < 9 -> no dialog"
			);
		}

		if (!StartingClasses.Contains(playerClass))
		{
			return new ClassChangeShowDialogPlan(
				ClassChangeShowDialogPlanStatus.SkippedNotStartingClass,
				playerLevel, race, playerClass,
				DialogPageId: 0,
				AscensionQuestId: 0,
				DialogWindowPacket: null,
				ShouldSendDialogWindow: false,
				$"ClassChangeService.showClassChangeDialog -> !{playerClass}.isStartingClass() -> no dialog"
			);
		}

		var pageIdPlan = ClassChangeDialogPlanService.CreateDialogPageIdPlan(race, playerClass);
		var questId = string.Equals(race, "ELYOS", StringComparison.OrdinalIgnoreCase)
			? ElyosAscensionQuestId
			: AsmodiansAscensionQuestId;
		var packet = new SmDialogWindow(targetObjectId: 0, pageIdPlan.DialogPageId, questId);

		return new ClassChangeShowDialogPlan(
			ClassChangeShowDialogPlanStatus.PlanCreated,
			playerLevel, race.ToUpperInvariant(), playerClass.ToUpperInvariant(),
			pageIdPlan.DialogPageId,
			questId,
			packet,
			ShouldSendDialogWindow: true,
			$"ClassChangeService.showClassChangeDialog -> level={playerLevel}>=9 and isStartingClass -> SM_DIALOG_WINDOW(0, pageId={pageIdPlan.DialogPageId}, questId={questId})"
		);
	}
}
