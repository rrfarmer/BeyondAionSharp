using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerVisualStatsUpdateService
{
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly GameServerRuntimeContext? _runtimeContext;
	private readonly GameTimeService? _gameTimeService;

	public PlayerVisualStatsUpdateService(
		IGameClientConnectionRegistry? connectionRegistry = null,
		GameServerRuntimeContext? runtimeContext = null,
		GameTimeService? gameTimeService = null)
	{
		_connectionRegistry = connectionRegistry;
		_runtimeContext = runtimeContext;
		_gameTimeService = gameTimeService;
	}

	public Task<PlayerVisualStatsUpdateResult> UpdateStatsVisuallyAsync(
		Player? player,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/stats/container/PlayerGameStats.updateStatsVisually -> updateStatInfo.
		return UpdateAsync(player, broadcastSpeedUpdate: false, speedSnapshot: null, cancellationToken);
	}

	public Task<PlayerVisualStatsUpdateResult> UpdateStatsAndSpeedVisuallyAsync(
		Player? player,
		PlayerVisualSpeedSnapshot? speedSnapshot,
		CancellationToken cancellationToken = default)
	{
		// Java parity: PlayerGameStats.updateStatsAndSpeedVisually -> onStatsChange(null), which sends stats before checking speed.
		return UpdateAsync(player, broadcastSpeedUpdate: true, speedSnapshot, cancellationToken);
	}

	private async Task<PlayerVisualStatsUpdateResult> UpdateAsync(
		Player? player,
		bool broadcastSpeedUpdate,
		PlayerVisualSpeedSnapshot? speedSnapshot,
		CancellationToken cancellationToken)
	{
		if (player == null)
			return PlayerVisualStatsUpdateResult.Skipped(PlayerVisualStatsUpdateStatus.MissingPlayer);

		var statsPacket = CreateStatsInfoPacket(player);
		if (_connectionRegistry == null)
		{
			return new PlayerVisualStatsUpdateResult(
				PlayerVisualStatsUpdateStatus.MissingConnectionRegistry,
				statsPacket,
				StatsPacketSent: false,
				SpeedPacket: null,
				SpeedBroadcastCount: 0,
				SpeedSnapshot: speedSnapshot);
		}

		cancellationToken.ThrowIfCancellationRequested();
		var statsSent = await _connectionRegistry.SendPacketToPlayerAsync(player.ObjectId, statsPacket);
		if (!statsSent)
		{
			return new PlayerVisualStatsUpdateResult(
				PlayerVisualStatsUpdateStatus.StatsSendFailed,
				statsPacket,
				StatsPacketSent: false,
				SpeedPacket: null,
				SpeedBroadcastCount: 0,
				SpeedSnapshot: speedSnapshot);
		}

		if (!broadcastSpeedUpdate)
		{
			return new PlayerVisualStatsUpdateResult(
				PlayerVisualStatsUpdateStatus.StatsSent,
				statsPacket,
				StatsPacketSent: true,
				SpeedPacket: null,
				SpeedBroadcastCount: 0,
				SpeedSnapshot: null);
		}

		if (speedSnapshot == null)
		{
			return new PlayerVisualStatsUpdateResult(
				PlayerVisualStatsUpdateStatus.SpeedSnapshotMissing,
				statsPacket,
				StatsPacketSent: true,
				SpeedPacket: null,
				SpeedBroadcastCount: 0,
				SpeedSnapshot: null);
		}

		var speedPacket = new SmEmotion(
			player,
			EmotionType.ChangeSpeed,
			emotion: 0,
			targetObjectId: 0,
			speedSnapshot.MovementSpeed,
			speedSnapshot.BaseAttackSpeed,
			speedSnapshot.CurrentAttackSpeed);
		cancellationToken.ThrowIfCancellationRequested();
		var broadcastCount = await _connectionRegistry.BroadcastToVisiblePlayersAsync(
			player.Position,
			player.ObjectId,
			speedPacket,
			includeSourcePlayer: true);
		return new PlayerVisualStatsUpdateResult(
			PlayerVisualStatsUpdateStatus.StatsAndSpeedSent,
			statsPacket,
			StatsPacketSent: true,
			speedPacket,
			broadcastCount,
			speedSnapshot);
	}

	private SmStatsInfo CreateStatsInfoPacket(Player player)
	{
		// Java parity: PlayerGameStats.updateStatInfo sends network/aion/serverpackets/SM_STATS_INFO(owner).
		var staticData = _runtimeContext?.DataManager?.StaticData;
		return new SmStatsInfo(
			player,
			staticData?.PlayerExperienceTable,
			_gameTimeService?.GameMinutes ?? 0,
			staticData?.ItemTemplates,
			staticData?.ItemRandomBonuses,
			staticData?.ItemSets,
			staticData?.EnchantTemplates,
			staticData?.TemperingTemplates,
			staticData?.SkillTemplates,
			staticData?.TitleTemplates);
	}
}

public sealed record PlayerVisualSpeedSnapshot(
	float MovementSpeed,
	int BaseAttackSpeed,
	int CurrentAttackSpeed);

public sealed record PlayerVisualStatsUpdateResult(
	PlayerVisualStatsUpdateStatus Status,
	SmStatsInfo? StatsPacket,
	bool StatsPacketSent,
	SmEmotion? SpeedPacket,
	int SpeedBroadcastCount,
	PlayerVisualSpeedSnapshot? SpeedSnapshot)
{
	public static PlayerVisualStatsUpdateResult Skipped(PlayerVisualStatsUpdateStatus status)
	{
		return new PlayerVisualStatsUpdateResult(
			status,
			StatsPacket: null,
			StatsPacketSent: false,
			SpeedPacket: null,
			SpeedBroadcastCount: 0,
			SpeedSnapshot: null);
	}
}

public enum PlayerVisualStatsUpdateStatus
{
	MissingPlayer,
	MissingConnectionRegistry,
	StatsSendFailed,
	StatsSent,
	StatsAndSpeedSent,
	SpeedSnapshotMissing,
}
