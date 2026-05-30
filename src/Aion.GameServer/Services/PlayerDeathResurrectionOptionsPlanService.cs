using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerDeathResurrectionOptionsPlanStatus
{
	SendSmDie,
	SkipNotDead,
	SkipTeleportTask,
}

public enum PlayerDeathResurrectionOptionsPlanStep
{
	ScheduleCallback,
	CheckPlayerDead,
	CheckTeleportTask,
	SendSmDie,
}

public sealed record PlayerDeathResurrectionOptionsPlan(
	PlayerDeathResurrectionOptionsPlanStatus Status,
	int PlayerObjectId,
	int DelayMilliseconds,
	int TeleportTaskOrdinal,
	string TeleportTaskName,
	int SmDiePacketOpcode,
	int? CurrentHpAtCallback,
	PlayerCreatureState CreatureStateAtCallback,
	bool IsDeadAtCallback,
	bool HasTeleportTaskAtCallback,
	bool ShouldSendPacket,
	bool ScheduledLiveTask,
	IReadOnlyList<PlayerDeathResurrectionOptionsPlanStep> Steps,
	string JavaSource,
	bool IsLive
);

public static class PlayerDeathResurrectionOptionsPlanService
{
	public const int JavaDelayMilliseconds = 500;
	public const int JavaTaskIdTeleportOrdinal = 1;
	public const string JavaTaskIdTeleportName = "TELEPORT";

	public static PlayerDeathResurrectionOptionsPlan CreatePlan(Player player, bool hasTeleportTaskAtCallback)
	{
		// Java parity:
		// PlayerController.scheduleShowResurrectionOptions always schedules a delayed callback.
		// The callback sends SM_DIE only if getOwner().isDead() and TaskId.TELEPORT is absent.
		var steps = new List<PlayerDeathResurrectionOptionsPlanStep>
		{
			PlayerDeathResurrectionOptionsPlanStep.ScheduleCallback,
			PlayerDeathResurrectionOptionsPlanStep.CheckPlayerDead,
		};
		var isDeadAtCallback = IsDeadAtCallback(player);
		if (!isDeadAtCallback)
		{
			return BuildPlan(
				player,
				hasTeleportTaskAtCallback,
				isDeadAtCallback,
				PlayerDeathResurrectionOptionsPlanStatus.SkipNotDead,
				shouldSendPacket: false,
				steps
			);
		}

		steps.Add(PlayerDeathResurrectionOptionsPlanStep.CheckTeleportTask);
		if (hasTeleportTaskAtCallback)
		{
			return BuildPlan(
				player,
				hasTeleportTaskAtCallback,
				isDeadAtCallback,
				PlayerDeathResurrectionOptionsPlanStatus.SkipTeleportTask,
				shouldSendPacket: false,
				steps
			);
		}

		steps.Add(PlayerDeathResurrectionOptionsPlanStep.SendSmDie);
		return BuildPlan(
			player,
			hasTeleportTaskAtCallback,
			isDeadAtCallback,
			PlayerDeathResurrectionOptionsPlanStatus.SendSmDie,
			shouldSendPacket: true,
			steps
		);
	}

	private static bool IsDeadAtCallback(Player player)
	{
		if (player.LifeStats is not null)
		{
			return player.LifeStats.CurrentHp <= 0;
		}

		return player.IsInState(PlayerCreatureState.Dead) || player.IsInState(PlayerCreatureState.FloatingCorpse);
	}

	private static PlayerDeathResurrectionOptionsPlan BuildPlan(
		Player player,
		bool hasTeleportTaskAtCallback,
		bool isDeadAtCallback,
		PlayerDeathResurrectionOptionsPlanStatus status,
		bool shouldSendPacket,
		IReadOnlyList<PlayerDeathResurrectionOptionsPlanStep> steps
	)
	{
		return new PlayerDeathResurrectionOptionsPlan(
			status,
			player.ObjectId,
			JavaDelayMilliseconds,
			JavaTaskIdTeleportOrdinal,
			JavaTaskIdTeleportName,
			SmDie.PacketOpCode,
			player.LifeStats?.CurrentHp,
			player.CreatureState,
			isDeadAtCallback,
			hasTeleportTaskAtCallback,
			shouldSendPacket,
			ScheduledLiveTask: false,
			steps.ToArray(),
			"com.aionemu.gameserver.controllers.PlayerController.scheduleShowResurrectionOptions -> ThreadPoolManager.schedule(..., 500); if getOwner().isDead() && !hasTask(TaskId.TELEPORT) showResurrectionOptions(); showResurrectionOptions -> send SM_DIE",
			IsLive: false
		);
	}
}
