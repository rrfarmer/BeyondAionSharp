using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using System.Collections.Concurrent;

namespace Aion.GameServer.Services;

public sealed class PlayerVisualStatsUpdateService
{
	private const int DefaultBaseAttackSpeed = 1500;

	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly GameServerRuntimeContext? _runtimeContext;
	private readonly GameTimeService? _gameTimeService;
	private readonly ConcurrentDictionary<int, PlayerVisualSpeedCache> _speedCacheByPlayerObjectId = new();

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

		var resolvedSpeedSnapshot = speedSnapshot ?? CreateSpeedSnapshot(player);
		if (resolvedSpeedSnapshot == null)
		{
			return new PlayerVisualStatsUpdateResult(
				PlayerVisualStatsUpdateStatus.SpeedSnapshotMissing,
				statsPacket,
				StatsPacketSent: true,
				SpeedPacket: null,
				SpeedBroadcastCount: 0,
				SpeedSnapshot: null);
		}

		if (!ShouldBroadcastSpeedUpdate(player.ObjectId, resolvedSpeedSnapshot))
		{
			return new PlayerVisualStatsUpdateResult(
				PlayerVisualStatsUpdateStatus.SpeedUnchanged,
				statsPacket,
				StatsPacketSent: true,
				SpeedPacket: null,
				SpeedBroadcastCount: 0,
				SpeedSnapshot: resolvedSpeedSnapshot);
		}

		var speedPacket = new SmEmotion(
			player,
			EmotionType.CHANGE_SPEED,
			emotion: 0,
			targetObjectId: 0,
			resolvedSpeedSnapshot.MovementSpeed,
			resolvedSpeedSnapshot.BaseAttackSpeed,
			resolvedSpeedSnapshot.CurrentAttackSpeed);
		cancellationToken.ThrowIfCancellationRequested();
		var broadcastCount = await _connectionRegistry.BroadcastToVisiblePlayersAsync(
			player.GetPosition(),
			player.ObjectId,
			speedPacket,
			includeSourcePlayer: true);
		RememberSpeedSnapshot(player.ObjectId, resolvedSpeedSnapshot);
		return new PlayerVisualStatsUpdateResult(
			PlayerVisualStatsUpdateStatus.StatsAndSpeedSent,
			statsPacket,
			StatsPacketSent: true,
			speedPacket,
			broadcastCount,
			resolvedSpeedSnapshot);
	}

	private SM_STATS_INFO CreateStatsInfoPacket(Player player)
	{
		// Java parity: PlayerGameStats.updateStatInfo sends network/aion/serverpackets/SM_STATS_INFO(owner).
		return new SM_STATS_INFO(player);
	}

	private PlayerVisualSpeedSnapshot? CreateSpeedSnapshot(Player player)
	{
		// Java parity: PlayerGameStats.getMovementSpeed / getAttackSpeed feeds SM_EMOTION(CHANGE_SPEED).
		var movementSpeed = PlayerMovementSpeedResolver.ResolveKnownMovementSpeed(player);
		var attackSpeed = ResolveAttackSpeed(player, _runtimeContext?.DataManager?.StaticData.ItemTemplates);
		return new PlayerVisualSpeedSnapshot(movementSpeed, attackSpeed, attackSpeed);
	}

	private static int ResolveAttackSpeed(Player player, ItemTemplateTable? itemTemplates)
	{
		// Java parity: model/stats/calc/functions/PlayerStatFunctions weapon attack-speed read from the faithful Equipment spine.
		var mainHandItem = player.GetEquipment()?.GetMainHandWeapon();
		var mainHandTemplate = mainHandItem?.GetItemTemplate();
		if (mainHandTemplate == null || !mainHandTemplate.IsWeapon())
			return DefaultBaseAttackSpeed;

		var mainHandSpeed = mainHandTemplate.GetWeaponStats()?.AttackSpeed ?? DefaultBaseAttackSpeed;
		var offHandTemplate = player.GetEquipment()?.GetOffHandWeapon()?.GetItemTemplate();
		var offHandBonus = offHandTemplate != null && offHandTemplate.IsWeapon() && !offHandTemplate.IsTwoHandWeapon()
			? (offHandTemplate.GetWeaponStats()?.AttackSpeed ?? 0) / 4
			: 0;
		return mainHandSpeed + offHandBonus;
	}

	private bool ShouldBroadcastSpeedUpdate(int playerObjectId, PlayerVisualSpeedSnapshot snapshot)
	{
		if (!_speedCacheByPlayerObjectId.TryGetValue(playerObjectId, out var cached))
			return GetMovementSpeedUnits(snapshot) != 0 || snapshot.CurrentAttackSpeed != 0;

		return cached.MovementSpeedUnits != GetMovementSpeedUnits(snapshot)
			|| cached.CurrentAttackSpeed != snapshot.CurrentAttackSpeed;
	}

	private void RememberSpeedSnapshot(int playerObjectId, PlayerVisualSpeedSnapshot snapshot)
	{
		_speedCacheByPlayerObjectId[playerObjectId] = new PlayerVisualSpeedCache(
			GetMovementSpeedUnits(snapshot),
			snapshot.CurrentAttackSpeed);
	}

	private static int GetMovementSpeedUnits(PlayerVisualSpeedSnapshot snapshot)
	{
		return (int)MathF.Round(snapshot.MovementSpeed * 1000f);
	}

	private sealed record PlayerVisualSpeedCache(int MovementSpeedUnits, int CurrentAttackSpeed);
}

public sealed record PlayerVisualSpeedSnapshot(
	float MovementSpeed,
	int BaseAttackSpeed,
	int CurrentAttackSpeed);

public sealed record PlayerVisualStatsUpdateResult(
	PlayerVisualStatsUpdateStatus Status,
	SM_STATS_INFO? StatsPacket,
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
	SpeedUnchanged,
	SpeedSnapshotMissing,
}
