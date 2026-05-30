using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskFanoutAction
{
	Start,
	Stop,
}

public enum PlayerProtectionActiveTaskFanoutStatus
{
	BroadcastPlanned,
	SkippedAlreadyProtectedStart,
	SkippedUnspawnedStop,
}

public enum PlayerProtectionActiveTaskFanoutStep
{
	CaptureVisualStateAfterMutation,
	ConstructSmPlayerState,
	SendToSourcePlayer,
	BroadcastToSightedPlayers,
}

public sealed record PlayerProtectionActiveTaskFanoutPlan(
	PlayerProtectionActiveTaskFanoutAction Action,
	PlayerProtectionActiveTaskFanoutStatus Status,
	int PlayerObjectId,
	bool ShouldBroadcast,
	bool SentPackets,
	bool IncludeSourcePlayer,
	bool SendsSourceBeforeSightedPlayers,
	bool UsesKnownListSeesFilter,
	bool RequiresLiveKnownList,
	Type? PacketType,
	string? PacketTypeName,
	int? PacketOpCode,
	PlayerProtectionActiveTaskPlanStep? VisualMutationStep,
	int? VisualMutationStepIndex,
	int? BroadcastStepIndex,
	IReadOnlyList<PlayerProtectionActiveTaskFanoutStep> Steps,
	string RecipientSelection,
	string JavaSource,
	bool IsLive
);

public static class PlayerProtectionActiveTaskFanoutPlanService
{
	public static PlayerProtectionActiveTaskFanoutPlan Create(PlayerProtectionActiveTaskPlan plan, PlayerProtectionActiveTaskFanoutAction action)
	{
		// Java parity: startProtectionActiveTask and stopProtectionActiveTask build SM_PLAYER_STATE and
		// send it to self first and then to sighted players when the branch reaches broadcast. This plan
		// captures that recipient ordering and the skipped branches.
		if (!plan.ShouldBroadcastPlayerState)
			return CreateSkippedPlan(plan, action);

		var visualMutationStep =
			action == PlayerProtectionActiveTaskFanoutAction.Start
				? PlayerProtectionActiveTaskPlanStep.SetBlinkingVisualState
				: PlayerProtectionActiveTaskPlanStep.UnsetBlinkingVisualState;

		return new PlayerProtectionActiveTaskFanoutPlan(
			action,
			PlayerProtectionActiveTaskFanoutStatus.BroadcastPlanned,
			plan.PlayerObjectId,
			ShouldBroadcast: true,
			SentPackets: false,
			IncludeSourcePlayer: true,
			SendsSourceBeforeSightedPlayers: true,
			UsesKnownListSeesFilter: true,
			RequiresLiveKnownList: true,
			typeof(SmPlayerState),
			nameof(SmPlayerState),
			SmPlayerState.PacketOpCode,
			visualMutationStep,
			IndexOf(plan.Steps, visualMutationStep),
			IndexOf(plan.Steps, PlayerProtectionActiveTaskPlanStep.BroadcastPlayerState),
			[
				PlayerProtectionActiveTaskFanoutStep.CaptureVisualStateAfterMutation,
				PlayerProtectionActiveTaskFanoutStep.ConstructSmPlayerState,
				PlayerProtectionActiveTaskFanoutStep.SendToSourcePlayer,
				PlayerProtectionActiveTaskFanoutStep.BroadcastToSightedPlayers,
			],
			"source player first because toSelf=true, then known-list players where other.getKnownList().sees(source)",
			"com.aionemu.gameserver.controllers.PlayerController protection task -> PacketSendUtility.broadcastToSightedPlayers(player, new SM_PLAYER_STATE(player), true)",
			IsLive: false
		);
	}

	private static PlayerProtectionActiveTaskFanoutPlan CreateSkippedPlan(
		PlayerProtectionActiveTaskPlan plan,
		PlayerProtectionActiveTaskFanoutAction action
	)
	{
		var status =
			plan.Status == PlayerProtectionActiveTaskPlanStatus.AlreadyProtected
				? PlayerProtectionActiveTaskFanoutStatus.SkippedAlreadyProtectedStart
				: PlayerProtectionActiveTaskFanoutStatus.SkippedUnspawnedStop;

		return new PlayerProtectionActiveTaskFanoutPlan(
			action,
			status,
			plan.PlayerObjectId,
			ShouldBroadcast: false,
			SentPackets: false,
			IncludeSourcePlayer: false,
			SendsSourceBeforeSightedPlayers: false,
			UsesKnownListSeesFilter: false,
			RequiresLiveKnownList: false,
			PacketType: null,
			PacketTypeName: null,
			PacketOpCode: null,
			VisualMutationStep: null,
			VisualMutationStepIndex: null,
			BroadcastStepIndex: null,
			[],
			"no recipients because Java branch does not call PacketSendUtility.broadcastToSightedPlayers",
			action == PlayerProtectionActiveTaskFanoutAction.Start
				? "com.aionemu.gameserver.controllers.PlayerController.startProtectionActiveTask -> already BLINKING, no SM_PLAYER_STATE broadcast"
				: "com.aionemu.gameserver.controllers.PlayerController.stopProtectionActiveTask -> !player.isSpawned(), no SM_PLAYER_STATE broadcast",
			IsLive: false
		);
	}

	private static int? IndexOf(IReadOnlyList<PlayerProtectionActiveTaskPlanStep> steps, PlayerProtectionActiveTaskPlanStep step)
	{
		for (var i = 0; i < steps.Count; i++)
		{
			if (steps[i] == step)
				return i;
		}

		return null;
	}
}
