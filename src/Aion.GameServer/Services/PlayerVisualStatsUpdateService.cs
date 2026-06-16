using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using System.Collections.Concurrent;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class PlayerVisualStatsUpdateService
{
	private const int DefaultBaseAttackSpeed = 1500;

	private readonly GameWorld? _world;
	private readonly GameServerRuntimeContext? _runtimeContext;
	private readonly GameTimeService? _gameTimeService;
	private readonly ConcurrentDictionary<int, PlayerVisualSpeedCache> _speedCacheByPlayerObjectId = new();

	public PlayerVisualStatsUpdateService(
		GameWorld? world = null,
		GameServerRuntimeContext? runtimeContext = null,
		GameTimeService? gameTimeService = null)
	{
		_world = world;
		_runtimeContext = runtimeContext;
		_gameTimeService = gameTimeService;
	}

	public Task<PlayerVisualStatsUpdateResult> UpdateStatsVisuallyAsync(
		Player? player,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/stats/container/PlayerGameStats.updateStatsVisually -> updateStatInfo.
		return Task.FromResult(Update(player, broadcastSpeedUpdate: false, speedSnapshot: null, cancellationToken));
	}

	public Task<PlayerVisualStatsUpdateResult> UpdateStatsAndSpeedVisuallyAsync(
		Player? player,
		PlayerVisualSpeedSnapshot? speedSnapshot,
		CancellationToken cancellationToken = default)
	{
		// Java parity: PlayerGameStats.updateStatsAndSpeedVisually -> onStatsChange(null), which sends stats before checking speed.
		return Task.FromResult(Update(player, broadcastSpeedUpdate: true, speedSnapshot, cancellationToken));
	}

	private PlayerVisualStatsUpdateResult Update(
		Player? player,
		bool broadcastSpeedUpdate,
		PlayerVisualSpeedSnapshot? speedSnapshot,
		CancellationToken cancellationToken)
	{
		if (player == null)
			return PlayerVisualStatsUpdateResult.Skipped(PlayerVisualStatsUpdateStatus.MissingPlayer);

		var statsPacket = CreateStatsInfoPacket(player);

		cancellationToken.ThrowIfCancellationRequested();
		// Java parity: PacketSendUtility.sendPacket(owner, new SM_STATS_INFO(owner)) no-ops if the player is offline.
		var statsSent = player.IsOnline();
		if (statsSent)
			PacketSendUtility.SendPacket(player, statsPacket);
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
		// Java parity: PacketSendUtility.broadcastPacket(owner, new SM_EMOTION(...), true) sends to the player and everyone who knows it.
		var broadcastCount = BroadcastToKnownPlayers(player.ObjectId, speedPacket, includeSource: true, filter: null);
		RememberSpeedSnapshot(player.ObjectId, resolvedSpeedSnapshot);
		return new PlayerVisualStatsUpdateResult(
			PlayerVisualStatsUpdateStatus.StatsAndSpeedSent,
			statsPacket,
			StatsPacketSent: true,
			speedPacket,
			broadcastCount,
			resolvedSpeedSnapshot);
	}

	// Java parity: PacketSendUtility.broadcastPacket(obj, packet, toSelf, filter) over the source object's KnownList.
	// Returns the number of players the packet was sent to (the reworked count plumbing kept by the result records).
	private int BroadcastToKnownPlayers(int sourceObjectId, AionServerPacket packet, bool includeSource, Predicate<Player>? filter)
	{
		var obj = _world?.FindVisibleObject(sourceObjectId);
		if (obj == null)
			return 0;

		var sent = 0;
		if (includeSource && obj is Player self && self.IsOnline())
		{
			PacketSendUtility.SendPacket(self, packet);
			sent++;
		}

		obj.GetKnownList().ForEachPlayer(p =>
		{
			if ((filter == null || filter(p)) && p.IsOnline())
			{
				PacketSendUtility.SendPacket(p, packet);
				sent++;
			}
		});
		return sent;
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
		var attackSpeed = ResolveAttackSpeed(player);
		return new PlayerVisualSpeedSnapshot(movementSpeed, attackSpeed, attackSpeed);
	}

	private static int ResolveAttackSpeed(Player player)
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
