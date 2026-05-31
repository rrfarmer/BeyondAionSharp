using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum CraftingTaskPacketPlanStatus
{
	Planned,
}

public sealed record CraftingTaskPacketPlan(
	CraftingTaskPacketPlanStatus Status,
	IReadOnlyList<GameServerPacket> SelfPackets,
	IReadOnlyList<GameServerPacket> BroadcastPackets,
	string JavaSource,
	bool IsLive);

public static class CraftingTaskPacketPlanService
{
	public const int FullBarValue = 1000;
	public const int InitAction = 0;
	public const int ProgressAction = 1;
	public const int CritBlueProgressAction = 2;
	public const int CritProcAction = 3;
	public const int CancelAction = 4;
	public const int SuccessAction = 5;
	public const int FailureAction = 6;
	public const int AnimationStartAction = 0;
	public const int AnimationProgressAction = 1;
	public const int AnimationCompleteAction = 2;
	public const int AnimationFailureAction = 3;

	public static CraftingTaskPacketPlan CreateInteractionStartPlan(
		int playerObjectId,
		int targetObjectId,
		int skillId,
		ItemTemplateSummary itemTemplate,
		bool isComboStart)
	{
		return new CraftingTaskPacketPlan(
			CraftingTaskPacketPlanStatus.Planned,
			[
				new SmCraftUpdate(skillId, itemTemplate, FullBarValue, FullBarValue, isComboStart ? CritProcAction : InitAction, 0, 0),
				new SmCraftUpdate(skillId, itemTemplate, 0, 0, ProgressAction, 0, 0),
			],
			[
				new SmCraftAnimation(playerObjectId, targetObjectId, skillId, AnimationStartAction),
				new SmCraftAnimation(playerObjectId, targetObjectId, skillId, AnimationProgressAction),
			],
			isComboStart
				? "CraftingTask.onInteractionStart combo -> send SM_CRAFT_UPDATE(action=3), SM_CRAFT_UPDATE(action=1), broadcast SM_CRAFT_ANIMATION(action=0/1)"
				: "CraftingTask.onInteractionStart -> send SM_CRAFT_UPDATE(action=0), SM_CRAFT_UPDATE(action=1), broadcast SM_CRAFT_ANIMATION(action=0/1)",
			IsLive: false);
	}

	public static CraftingTaskPacketPlan CreateProgressUpdatePlan(
		int skillId,
		ItemTemplateSummary itemTemplate,
		int success,
		int failure,
		int progressAction,
		int executionSpeed,
		int showBarDelay)
	{
		return new CraftingTaskPacketPlan(
			CraftingTaskPacketPlanStatus.Planned,
			[new SmCraftUpdate(skillId, itemTemplate, success, failure, progressAction, executionSpeed, showBarDelay)],
			Array.Empty<GameServerPacket>(),
			"CraftingTask.sendInteractionUpdate -> send SM_CRAFT_UPDATE(action=craftType.getProgressId(), executionSpeed, showBarDelay)",
			IsLive: false);
	}

	public static CraftingTaskPacketPlan CreateAbortPlan(
		int playerObjectId,
		int targetObjectId,
		int skillId,
		ItemTemplateSummary itemTemplate)
	{
		return new CraftingTaskPacketPlan(
			CraftingTaskPacketPlanStatus.Planned,
			[new SmCraftUpdate(skillId, itemTemplate, 0, 0, CancelAction, 0, 0)],
			[new SmCraftAnimation(playerObjectId, targetObjectId, 0, AnimationCompleteAction)],
			"CraftingTask.onInteractionAbort -> send SM_CRAFT_UPDATE(action=4), broadcast SM_CRAFT_ANIMATION(skill=0, action=2)",
			IsLive: false);
	}

	public static CraftingTaskPacketPlan CreateFailureFinishPlan(
		int playerObjectId,
		int targetObjectId,
		int skillId,
		ItemTemplateSummary itemTemplate,
		int success,
		int failure)
	{
		return new CraftingTaskPacketPlan(
			CraftingTaskPacketPlanStatus.Planned,
			[new SmCraftUpdate(skillId, itemTemplate, success, failure, FailureAction, 0, 0)],
			[new SmCraftAnimation(playerObjectId, targetObjectId, 0, AnimationFailureAction)],
			"CraftingTask.onFailureFinish -> send SM_CRAFT_UPDATE(action=6), broadcast SM_CRAFT_ANIMATION(skill=0, action=3)",
			IsLive: false);
	}

	public static CraftingTaskPacketPlan CreateSuccessFinishPlan(
		int playerObjectId,
		int targetObjectId,
		int skillId,
		ItemTemplateSummary itemTemplate,
		int success,
		int failure)
	{
		return new CraftingTaskPacketPlan(
			CraftingTaskPacketPlanStatus.Planned,
			[new SmCraftUpdate(skillId, itemTemplate, success, failure, SuccessAction, 0, 0)],
			[new SmCraftAnimation(playerObjectId, targetObjectId, 0, AnimationCompleteAction)],
			"CraftingTask.onSuccessFinish non-crit -> send SM_CRAFT_UPDATE(action=5), broadcast SM_CRAFT_ANIMATION(skill=0, action=2)",
			IsLive: false);
	}
}
