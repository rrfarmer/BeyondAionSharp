using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using System.Collections.Concurrent;

namespace Aion.GameServer.Services;

public sealed class PlayerVisualStatsUpdateService
{
	private const long MainHand = 1L;
	private const long SubHand = 1L << 1;
	private const long MainOffHand = 1L << 17;
	private const long SubOffHand = 1L << 18;
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
			EmotionType.ChangeSpeed,
			emotion: 0,
			targetObjectId: 0,
			resolvedSpeedSnapshot.MovementSpeed,
			resolvedSpeedSnapshot.BaseAttackSpeed,
			resolvedSpeedSnapshot.CurrentAttackSpeed);
		cancellationToken.ThrowIfCancellationRequested();
		var broadcastCount = await _connectionRegistry.BroadcastToVisiblePlayersAsync(
			player.Position,
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

	private PlayerVisualSpeedSnapshot? CreateSpeedSnapshot(Player player)
	{
		// Java parity: PlayerGameStats.getMovementSpeed / getAttackSpeed feeds SM_EMOTION(CHANGE_SPEED).
		var movementSpeed = ResolveKnownMovementSpeed(player);
		if (movementSpeed == null)
			return null;

		var attackSpeed = ResolveAttackSpeed(player, _runtimeContext?.DataManager?.StaticData.ItemTemplates);
		return new PlayerVisualSpeedSnapshot(movementSpeed.Value, attackSpeed, attackSpeed);
	}

	private static float? ResolveKnownMovementSpeed(Player player)
	{
		if (!player.IsInRideMode || player.RideInfo == null)
			return null;

		if (player.IsFlying())
			return player.RideInfo.FlySpeed;

		return player.IsInSprintMode && player.RideInfo.CanSprint()
			? player.RideInfo.SprintSpeed
			: player.RideInfo.MoveSpeed;
	}

	private static int ResolveAttackSpeed(Player player, ItemTemplateTable? itemTemplates)
	{
		if (itemTemplates == null)
			return DefaultBaseAttackSpeed;

		var equippedWeapons = player.InventoryItems
			.Where(item => item.IsEquipped && item.Location == 0)
			.Select(item => (Item: item, Template: itemTemplates.GetItemTemplate(item.ItemId)))
			.Where(item => item.Template?.IsWeapon == true)
			.Select(item => new EquippedWeapon(item.Item, item.Template!))
			.ToArray();
		var mainHand = equippedWeapons.FirstOrDefault(item => IsRightHandSlot(item.Item.Slot));
		if (mainHand == null)
			return DefaultBaseAttackSpeed;

		var offHand = equippedWeapons.FirstOrDefault(item =>
			item != mainHand
			&& IsLeftHandSlot(item.Item.Slot)
			&& !IsTwoHandedSlot(item.Item.Slot));
		return mainHand.Template.WeaponStats?.AttackSpeed + (offHand?.Template.WeaponStats?.AttackSpeed / 4 ?? 0)
			?? DefaultBaseAttackSpeed;
	}

	private static bool IsRightHandSlot(long slot)
	{
		return (slot & (MainHand | MainOffHand)) != 0;
	}

	private static bool IsLeftHandSlot(long slot)
	{
		return (slot & (SubHand | SubOffHand)) != 0;
	}

	private static bool IsTwoHandedSlot(long slot)
	{
		return (slot & (MainHand | SubHand)) == (MainHand | SubHand)
			|| (slot & (MainOffHand | SubOffHand)) == (MainOffHand | SubOffHand);
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

	private sealed record EquippedWeapon(InventoryItem Item, ItemTemplateSummary Template);

	private sealed record PlayerVisualSpeedCache(int MovementSpeedUnits, int CurrentAttackSpeed);
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
	SpeedUnchanged,
	SpeedSnapshotMissing,
}
