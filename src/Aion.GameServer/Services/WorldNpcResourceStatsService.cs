using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcResourceStatsService
{
	private readonly WorldNpcLifeStatsService _npcLifeStats;
	private readonly IGameClientConnectionRegistry? _connectionRegistry;

	public WorldNpcResourceStatsService(
		WorldNpcLifeStatsService npcLifeStats,
		IGameClientConnectionRegistry? connectionRegistry = null)
	{
		_npcLifeStats = npcLifeStats;
		_connectionRegistry = connectionRegistry;
	}

	public ValueTask<WorldNpcResourceChangeResult> ReduceNpcMpAsync(
		IWorldNpcObject? npc,
		int value,
		int skillId = 0,
		SmAttackStatusType? packetType = SmAttackStatusType.DamageMp,
		SmAttackStatusLog? packetLog = SmAttackStatusLog.MpAttack,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/stats/container/CreatureLifeStats.reduceMp.
		return ApplyNpcMpChangeAsync(npc, WorldNpcResourceChangeKind.Reduce, value, skillId, packetType, packetLog, cancellationToken);
	}

	public ValueTask<WorldNpcResourceChangeResult> IncreaseNpcMpAsync(
		IWorldNpcObject? npc,
		int value,
		int skillId = 0,
		SmAttackStatusType? packetType = SmAttackStatusType.Mp,
		SmAttackStatusLog? packetLog = SmAttackStatusLog.MpHeal,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/stats/container/CreatureLifeStats.increaseMp.
		return ApplyNpcMpChangeAsync(npc, WorldNpcResourceChangeKind.Increase, value, skillId, packetType, packetLog, cancellationToken);
	}

	public ValueTask<WorldNpcResourceChangeResult> ReducePlayerMpAsync(
		Player? player,
		int maxMp,
		int value,
		int skillId = 0,
		SmAttackStatusType? packetType = SmAttackStatusType.DamageMp,
		SmAttackStatusLog? packetLog = SmAttackStatusLog.MpAttack,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/stats/container/CreatureLifeStats.reduceMp also backs player MP.
		return ApplyPlayerMpChangeAsync(player, maxMp, WorldNpcResourceChangeKind.Reduce, value, skillId, packetType, packetLog, cancellationToken);
	}

	public ValueTask<WorldNpcResourceChangeResult> IncreasePlayerMpAsync(
		Player? player,
		int maxMp,
		int value,
		int skillId = 0,
		SmAttackStatusType? packetType = SmAttackStatusType.Mp,
		SmAttackStatusLog? packetLog = SmAttackStatusLog.MpHeal,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/stats/container/CreatureLifeStats.increaseMp also backs player MP.
		return ApplyPlayerMpChangeAsync(player, maxMp, WorldNpcResourceChangeKind.Increase, value, skillId, packetType, packetLog, cancellationToken);
	}

	public ValueTask<WorldNpcResourceChangeResult> ReducePlayerFpAsync(
		Player? player,
		int maxHp,
		int maxFp,
		int value,
		int skillId = 0,
		SmAttackStatusType? packetType = SmAttackStatusType.FpDamage,
		SmAttackStatusLog? packetLog = SmAttackStatusLog.FpAttack,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/stats/container/PlayerLifeStats.reduceFp.
		return ApplyPlayerFpChangeAsync(player, maxHp, maxFp, WorldNpcResourceChangeKind.Reduce, value, skillId, packetType, packetLog, cancellationToken);
	}

	public ValueTask<WorldNpcResourceChangeResult> IncreasePlayerFpAsync(
		Player? player,
		int maxHp,
		int maxFp,
		int value,
		int skillId = 0,
		SmAttackStatusType? packetType = SmAttackStatusType.Fp,
		SmAttackStatusLog? packetLog = SmAttackStatusLog.FpHeal,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/stats/container/PlayerLifeStats.increaseFp.
		return ApplyPlayerFpChangeAsync(player, maxHp, maxFp, WorldNpcResourceChangeKind.Increase, value, skillId, packetType, packetLog, cancellationToken);
	}

	public WorldNpcResourceChangeResult AddPlayerDp(
		Player? player,
		int value,
		int? maxDp = null)
	{
		// Java parity: model/gameobjects/player/PlayerCommonData.addDp/setDp.
		if (player == null)
			return WorldNpcResourceChangeResult.MissingTarget(WorldNpcEffectResourceType.Dp, WorldNpcResourceChangeKind.Increase, value);
		if (IsStartingClass(player.PlayerClass))
		{
			return WorldNpcResourceChangeResult.FromResourceMutation(
				WorldNpcResourceChangeStatus.StartingClass,
				player.ObjectId,
				WorldNpcEffectResourceType.Dp,
				WorldNpcResourceChangeKind.Increase,
				value,
				AppliedValue: 0,
				PreviousValue: player.Dp,
				CurrentValue: player.Dp,
				MaxValue: maxDp);
		}
		if (player.IsOnline && maxDp == null)
		{
			return WorldNpcResourceChangeResult.FromResourceMutation(
				WorldNpcResourceChangeStatus.MissingMaxResource,
				player.ObjectId,
				WorldNpcEffectResourceType.Dp,
				WorldNpcResourceChangeKind.Increase,
				value,
				AppliedValue: 0,
				PreviousValue: player.Dp,
				CurrentValue: player.Dp,
				MaxValue: null);
		}

		var previousDp = player.Dp;
		var requestedDp = previousDp + value;
		var currentDp = maxDp is { } cap && requestedDp > cap
			? cap
			: requestedDp;
		player.Dp = currentDp;
		return WorldNpcResourceChangeResult.FromResourceMutation(
			currentDp == previousDp
				? WorldNpcResourceChangeStatus.NoChange
				: currentDp > previousDp ? WorldNpcResourceChangeStatus.Increased : WorldNpcResourceChangeStatus.Reduced,
			player.ObjectId,
			WorldNpcEffectResourceType.Dp,
			WorldNpcResourceChangeKind.Increase,
			value,
			Math.Abs(currentDp - previousDp),
			previousDp,
			currentDp,
			maxDp,
			BroadcastDpInfo: player.IsOnline,
			SendDpStatUpdate: player.IsOnline,
			UpdateStatsAndSpeedVisually: player.IsOnline);
	}

	public ValueTask<WorldNpcResourceEffectApplicationResult> ApplyResourceOverTimePeriodicResultAsync(
		WorldNpcSkillResourceOverTimePeriodicActionResult effectResult,
		WorldNpcResourceMutationTarget target,
		CancellationToken cancellationToken = default)
	{
		// Java parity: skillengine/effect resource periodic actions eventually call CreatureLifeStats/PlayerLifeStats resource mutators.
		if (!effectResult.Applied)
			return ValueTask.FromResult(WorldNpcResourceEffectApplicationResult.EffectSkipped(effectResult.ResourceType, effectResult.SkillId));

		var kind = effectResult.IsDamage ? WorldNpcResourceChangeKind.Reduce : WorldNpcResourceChangeKind.Increase;
		return ApplyStagedResourceMutationAsync(
			effectResult.ResourceType,
			kind,
			effectResult.FinalValue,
			effectResult.SkillId,
			effectResult.PacketType,
			effectResult.PacketLog,
			target,
			cancellationToken);
	}

	public ValueTask<WorldNpcResourceEffectApplicationResult> ApplyInstantResourceResultAsync(
		WorldNpcSkillInstantResourceEffectResult effectResult,
		WorldNpcResourceMutationTarget target,
		CancellationToken cancellationToken = default)
	{
		// Java parity: skillengine/effect/{MpAttackInstant,FpAttackInstant,DelayedFpAtkInstant}Effect live callbacks reduce MP/FP.
		if (!effectResult.Applied)
			return ValueTask.FromResult(WorldNpcResourceEffectApplicationResult.EffectSkipped(effectResult.ResourceType, effectResult.SkillId));

		return ApplyStagedResourceMutationAsync(
			effectResult.ResourceType,
			WorldNpcResourceChangeKind.Reduce,
			effectResult.FinalValue,
			effectResult.SkillId,
			effectResult.PacketType,
			effectResult.PacketLog,
			target,
			cancellationToken);
	}

	private async ValueTask<WorldNpcResourceEffectApplicationResult> ApplyStagedResourceMutationAsync(
		WorldNpcEffectResourceType resourceType,
		WorldNpcResourceChangeKind kind,
		int value,
		int skillId,
		SmAttackStatusType? packetType,
		SmAttackStatusLog? packetLog,
		WorldNpcResourceMutationTarget target,
		CancellationToken cancellationToken)
	{
		WorldNpcResourceChangeResult change;
		switch (resourceType)
		{
			case WorldNpcEffectResourceType.Mp when target.Player != null:
				if (target.MaxMp == null)
					return WorldNpcResourceEffectApplicationResult.MissingMaxResource(resourceType, skillId);
				change = kind == WorldNpcResourceChangeKind.Reduce
					? await ReducePlayerMpAsync(target.Player, target.MaxMp.Value, value, skillId, packetType, packetLog, cancellationToken)
					: await IncreasePlayerMpAsync(target.Player, target.MaxMp.Value, value, skillId, packetType, packetLog, cancellationToken);
				return WorldNpcResourceEffectApplicationResult.FromChange(change, skillId);
			case WorldNpcEffectResourceType.Mp when target.Npc != null:
				change = kind == WorldNpcResourceChangeKind.Reduce
					? await ReduceNpcMpAsync(target.Npc, value, skillId, packetType, packetLog, cancellationToken)
					: await IncreaseNpcMpAsync(target.Npc, value, skillId, packetType, packetLog, cancellationToken);
				return WorldNpcResourceEffectApplicationResult.FromChange(change, skillId);
			case WorldNpcEffectResourceType.Fp:
				if (target.Player == null)
					return WorldNpcResourceEffectApplicationResult.MissingTarget(resourceType, skillId);
				if (target.MaxHp == null || target.MaxFp == null)
					return WorldNpcResourceEffectApplicationResult.MissingMaxResource(resourceType, skillId);
				change = kind == WorldNpcResourceChangeKind.Reduce
					? await ReducePlayerFpAsync(target.Player, target.MaxHp.Value, target.MaxFp.Value, value, skillId, packetType, packetLog, cancellationToken)
					: await IncreasePlayerFpAsync(target.Player, target.MaxHp.Value, target.MaxFp.Value, value, skillId, packetType, packetLog, cancellationToken);
				return WorldNpcResourceEffectApplicationResult.FromChange(change, skillId);
			case WorldNpcEffectResourceType.Hp:
			case WorldNpcEffectResourceType.Dp:
			default:
				return WorldNpcResourceEffectApplicationResult.UnsupportedResource(resourceType, skillId);
		}
	}

	private async ValueTask<WorldNpcResourceChangeResult> ApplyNpcMpChangeAsync(
		IWorldNpcObject? npc,
		WorldNpcResourceChangeKind kind,
		int value,
		int skillId,
		SmAttackStatusType? packetType,
		SmAttackStatusLog? packetLog,
		CancellationToken cancellationToken)
	{
		if (npc == null)
			return WorldNpcResourceChangeResult.MissingTarget(WorldNpcEffectResourceType.Mp, kind, value);

		var mutation = _npcLifeStats.ApplyMpChange(npc.ObjectId, kind, value);
		var previousValue = mutation.Previous?.CurrentMp ?? 0;
		var currentValue = mutation.Current?.CurrentMp ?? previousValue;
		var maxValue = mutation.Current?.MaxMp;
		var (packet, broadcastCount) = await BroadcastAttackStatusAsync(
			npc.Position,
			npc.ObjectId,
			packetType,
			packetLog,
			skillId,
			mutation.AppliedValue,
			mutation.Current?.GetMpPercentage() ?? 0,
			ShouldSendCreatureLifeStatsPacket(mutation.AppliedValue, skillId, packetType),
			cancellationToken);
		return WorldNpcResourceChangeResult.FromResourceMutation(
			mutation.Status,
			npc.ObjectId,
			WorldNpcEffectResourceType.Mp,
			kind,
			value,
			mutation.AppliedValue,
			previousValue,
			currentValue,
			maxValue,
			packetType,
			packetLog,
			packet,
			broadcastCount);
	}

	private async ValueTask<WorldNpcResourceChangeResult> ApplyPlayerMpChangeAsync(
		Player? player,
		int maxMp,
		WorldNpcResourceChangeKind kind,
		int value,
		int skillId,
		SmAttackStatusType? packetType,
		SmAttackStatusLog? packetLog,
		CancellationToken cancellationToken)
	{
		if (player == null)
			return WorldNpcResourceChangeResult.MissingTarget(WorldNpcEffectResourceType.Mp, kind, value);
		if (player.LifeStats == null)
			return WorldNpcResourceChangeResult.MissingStats(player.ObjectId, WorldNpcEffectResourceType.Mp, kind, value);
		if (player.LifeStats.CurrentHp <= 0)
		{
			return WorldNpcResourceChangeResult.FromResourceMutation(
				WorldNpcResourceChangeStatus.AlreadyDead,
				player.ObjectId,
				WorldNpcEffectResourceType.Mp,
				kind,
				value,
				AppliedValue: 0,
				PreviousValue: player.LifeStats.CurrentMp,
				CurrentValue: player.LifeStats.CurrentMp,
				MaxValue: maxMp);
		}

		var normalizedMaxMp = Math.Max(0, maxMp);
		var previousMp = player.LifeStats.GetCurrentMp(normalizedMaxMp);
		var currentMp = kind switch
		{
			WorldNpcResourceChangeKind.Reduce => Math.Min(previousMp, Math.Max(previousMp - value, 0)),
			WorldNpcResourceChangeKind.Increase => Math.Max(previousMp, Math.Min(previousMp + value, normalizedMaxMp)),
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unhandled Java MP resource change kind."),
		};
		var appliedValue = Math.Abs(currentMp - previousMp);
		if (appliedValue != 0)
			player.LifeStats = player.LifeStats with { CurrentMp = currentMp };
		var status = GetStatus(kind, previousMp, currentMp);
		var mpPercentage = normalizedMaxMp <= 0 ? 0 : (int)(100f * currentMp / normalizedMaxMp);
		var (packet, broadcastCount) = await BroadcastAttackStatusAsync(
			player.Position,
			player.ObjectId,
			packetType,
			packetLog,
			skillId,
			appliedValue,
			mpPercentage,
			ShouldSendCreatureLifeStatsPacket(appliedValue, skillId, packetType),
			cancellationToken);
		return WorldNpcResourceChangeResult.FromResourceMutation(
			status,
			player.ObjectId,
			WorldNpcEffectResourceType.Mp,
			kind,
			value,
			appliedValue,
			previousMp,
			currentMp,
			normalizedMaxMp,
			packetType,
			packetLog,
			packet,
			broadcastCount);
	}

	private async ValueTask<WorldNpcResourceChangeResult> ApplyPlayerFpChangeAsync(
		Player? player,
		int maxHp,
		int maxFp,
		WorldNpcResourceChangeKind kind,
		int value,
		int skillId,
		SmAttackStatusType? packetType,
		SmAttackStatusLog? packetLog,
		CancellationToken cancellationToken)
	{
		if (player == null)
			return WorldNpcResourceChangeResult.MissingTarget(WorldNpcEffectResourceType.Fp, kind, value);
		if (player.LifeStats == null)
			return WorldNpcResourceChangeResult.MissingStats(player.ObjectId, WorldNpcEffectResourceType.Fp, kind, value);
		if (kind == WorldNpcResourceChangeKind.Increase && player.LifeStats.CurrentHp <= 0)
		{
			return WorldNpcResourceChangeResult.FromResourceMutation(
				WorldNpcResourceChangeStatus.AlreadyDead,
				player.ObjectId,
				WorldNpcEffectResourceType.Fp,
				kind,
				value,
				AppliedValue: 0,
				PreviousValue: player.LifeStats.GetCurrentFp(),
				CurrentValue: player.LifeStats.GetCurrentFp(),
				MaxValue: maxFp);
		}

		var previousFp = player.LifeStats.GetCurrentFp();
		var normalizedMaxFp = Math.Max(0, maxFp);
		var valueToSend = value;
		var currentFp = kind switch
		{
			WorldNpcResourceChangeKind.Reduce => previousFp - value,
			WorldNpcResourceChangeKind.Increase => previousFp + value,
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unhandled Java FP resource change kind."),
		};
		if (kind == WorldNpcResourceChangeKind.Reduce && currentFp < 0)
		{
			currentFp = 0;
			valueToSend = previousFp;
		}
		else if (kind == WorldNpcResourceChangeKind.Increase && currentFp > normalizedMaxFp)
		{
			currentFp = normalizedMaxFp;
			valueToSend = normalizedMaxFp - previousFp;
		}

		player.LifeStats = player.LifeStats with { CurrentFp = currentFp };
		var shouldSendPacket = packetType != null && (kind == WorldNpcResourceChangeKind.Reduce || valueToSend > 0);
		var shouldSendFlyTimeUpdate = kind == WorldNpcResourceChangeKind.Reduce || valueToSend > 0;
		var hpPercentage = GetHpPercentage(player.LifeStats.GetCurrentHp(Math.Max(0, maxHp)), Math.Max(0, maxHp));
		var (packet, broadcastCount) = await BroadcastAttackStatusAsync(
			player.Position,
			player.ObjectId,
			packetType,
			packetLog,
			skillId,
			valueToSend,
			hpPercentage,
			shouldSendPacket,
			cancellationToken);
		return WorldNpcResourceChangeResult.FromResourceMutation(
			GetStatus(kind, previousFp, currentFp),
			player.ObjectId,
			WorldNpcEffectResourceType.Fp,
			kind,
			value,
			Math.Abs(currentFp - previousFp),
			previousFp,
			currentFp,
			normalizedMaxFp,
			packetType,
			packetLog,
			packet,
			broadcastCount,
			SendFlyTimeUpdate: shouldSendFlyTimeUpdate);
	}

	private async ValueTask<(SmAttackStatus? Packet, int BroadcastCount)> BroadcastAttackStatusAsync(
		WorldPosition position,
		int objectId,
		SmAttackStatusType? packetType,
		SmAttackStatusLog? packetLog,
		int skillId,
		int value,
		int hpOrMpPercentage,
		bool shouldSend,
		CancellationToken cancellationToken)
	{
		if (!shouldSend || packetType == null)
			return (null, 0);

		var packet = new SmAttackStatus(
			objectId,
			packetType.Value,
			skillId,
			value,
			hpOrMpPercentage,
			packetLog ?? SmAttackStatusLog.Regular);
		if (_connectionRegistry == null)
			return (packet, 0);

		cancellationToken.ThrowIfCancellationRequested();
		var count = await _connectionRegistry.BroadcastToVisiblePlayersAsync(
			position,
			objectId,
			packet,
			includeSourcePlayer: true);
		return (packet, count);
	}

	private static bool ShouldSendCreatureLifeStatsPacket(int appliedValue, int skillId, SmAttackStatusType? packetType)
	{
		return packetType != null && (appliedValue != 0 || skillId != 0);
	}

	private static WorldNpcResourceChangeStatus GetStatus(WorldNpcResourceChangeKind kind, int previousValue, int currentValue)
	{
		if (previousValue == currentValue)
			return WorldNpcResourceChangeStatus.NoChange;
		return kind == WorldNpcResourceChangeKind.Reduce
			? WorldNpcResourceChangeStatus.Reduced
			: WorldNpcResourceChangeStatus.Increased;
	}

	private static int GetHpPercentage(int currentHp, int maxHp)
	{
		// Java parity: model/stats/container/CreatureLifeStats.getHpPercentage.
		if (currentHp == 0 || maxHp <= 0)
			return 0;

		return Math.Max(1, (int)(100f * currentHp / maxHp));
	}

	private static bool IsStartingClass(string playerClass)
	{
		// Java parity: model/gameobjects/player/PlayerClass.isStartingClass.
		return playerClass is "WARRIOR" or "SCOUT" or "MAGE" or "PRIEST" or "ENGINEER" or "ARTIST";
	}
}

public sealed record WorldNpcResourceMutationTarget(
	IWorldNpcObject? Npc = null,
	Player? Player = null,
	int? MaxHp = null,
	int? MaxMp = null,
	int? MaxFp = null,
	int? MaxDp = null);

public sealed record WorldNpcResourceEffectApplicationResult(
	WorldNpcResourceEffectApplicationStatus Status,
	WorldNpcEffectResourceType ResourceType,
	int SkillId,
	WorldNpcResourceChangeResult? Change)
{
	public static WorldNpcResourceEffectApplicationResult FromChange(WorldNpcResourceChangeResult change, int skillId = 0)
	{
		var status = change.Status switch
		{
			WorldNpcResourceChangeStatus.MissingTarget => WorldNpcResourceEffectApplicationStatus.MissingTarget,
			WorldNpcResourceChangeStatus.MissingStats => WorldNpcResourceEffectApplicationStatus.MissingStats,
			WorldNpcResourceChangeStatus.MissingMaxResource => WorldNpcResourceEffectApplicationStatus.MissingMaxResource,
			WorldNpcResourceChangeStatus.AlreadyDead => WorldNpcResourceEffectApplicationStatus.TargetDead,
			_ => WorldNpcResourceEffectApplicationStatus.Applied,
		};
		return new WorldNpcResourceEffectApplicationResult(status, change.ResourceType, skillId == 0 ? change.AttackStatusPacket?.SkillId ?? 0 : skillId, change);
	}

	public static WorldNpcResourceEffectApplicationResult EffectSkipped(WorldNpcEffectResourceType resourceType, int skillId)
	{
		return new WorldNpcResourceEffectApplicationResult(
			WorldNpcResourceEffectApplicationStatus.EffectSkipped,
			resourceType,
			skillId,
			Change: null);
	}

	public static WorldNpcResourceEffectApplicationResult MissingTarget(WorldNpcEffectResourceType resourceType, int skillId)
	{
		return new WorldNpcResourceEffectApplicationResult(
			WorldNpcResourceEffectApplicationStatus.MissingTarget,
			resourceType,
			skillId,
			Change: null);
	}

	public static WorldNpcResourceEffectApplicationResult MissingMaxResource(WorldNpcEffectResourceType resourceType, int skillId)
	{
		return new WorldNpcResourceEffectApplicationResult(
			WorldNpcResourceEffectApplicationStatus.MissingMaxResource,
			resourceType,
			skillId,
			Change: null);
	}

	public static WorldNpcResourceEffectApplicationResult UnsupportedResource(WorldNpcEffectResourceType resourceType, int skillId)
	{
		return new WorldNpcResourceEffectApplicationResult(
			WorldNpcResourceEffectApplicationStatus.UnsupportedResource,
			resourceType,
			skillId,
			Change: null);
	}
}

public enum WorldNpcResourceEffectApplicationStatus
{
	Applied,
	EffectSkipped,
	MissingTarget,
	MissingStats,
	MissingMaxResource,
	TargetDead,
	UnsupportedResource,
}

public sealed record WorldNpcResourceChangeResult(
	WorldNpcResourceChangeStatus Status,
	int ObjectId,
	WorldNpcEffectResourceType ResourceType,
	WorldNpcResourceChangeKind ChangeKind,
	int RequestedValue,
	int AppliedValue,
	int PreviousValue,
	int CurrentValue,
	int? MaxValue,
	SmAttackStatusType? PacketType = null,
	SmAttackStatusLog? PacketLog = null,
	SmAttackStatus? AttackStatusPacket = null,
	int AttackStatusBroadcastCount = 0,
	bool SendFlyTimeUpdate = false,
	bool BroadcastDpInfo = false,
	bool SendDpStatUpdate = false,
	bool UpdateStatsAndSpeedVisually = false)
{
	public bool Mutated => PreviousValue != CurrentValue;

	public static WorldNpcResourceChangeResult MissingTarget(
		WorldNpcEffectResourceType resourceType,
		WorldNpcResourceChangeKind kind,
		int requestedValue)
	{
		return FromResourceMutation(
			WorldNpcResourceChangeStatus.MissingTarget,
			ObjectId: 0,
			resourceType,
			kind,
			requestedValue,
			AppliedValue: 0,
			PreviousValue: 0,
			CurrentValue: 0,
			MaxValue: null);
	}

	public static WorldNpcResourceChangeResult MissingStats(
		int objectId,
		WorldNpcEffectResourceType resourceType,
		WorldNpcResourceChangeKind kind,
		int requestedValue)
	{
		return FromResourceMutation(
			WorldNpcResourceChangeStatus.MissingStats,
			objectId,
			resourceType,
			kind,
			requestedValue,
			AppliedValue: 0,
			PreviousValue: 0,
			CurrentValue: 0,
			MaxValue: null);
	}

	public static WorldNpcResourceChangeResult FromResourceMutation(
		WorldNpcResourceChangeStatus status,
		int ObjectId,
		WorldNpcEffectResourceType ResourceType,
		WorldNpcResourceChangeKind ChangeKind,
		int RequestedValue,
		int AppliedValue,
		int PreviousValue,
		int CurrentValue,
		int? MaxValue,
		SmAttackStatusType? PacketType = null,
		SmAttackStatusLog? PacketLog = null,
		SmAttackStatus? AttackStatusPacket = null,
		int AttackStatusBroadcastCount = 0,
		bool SendFlyTimeUpdate = false,
		bool BroadcastDpInfo = false,
		bool SendDpStatUpdate = false,
		bool UpdateStatsAndSpeedVisually = false)
	{
		return new WorldNpcResourceChangeResult(
			status,
			ObjectId,
			ResourceType,
			ChangeKind,
			RequestedValue,
			AppliedValue,
			PreviousValue,
			CurrentValue,
			MaxValue,
			PacketType,
			PacketLog,
			AttackStatusPacket,
			AttackStatusBroadcastCount,
			SendFlyTimeUpdate,
			BroadcastDpInfo,
			SendDpStatUpdate,
			UpdateStatsAndSpeedVisually);
	}
}

public enum WorldNpcResourceChangeKind
{
	Reduce,
	Increase,
}

public enum WorldNpcResourceChangeStatus
{
	MissingTarget,
	MissingStats,
	MissingMaxResource,
	AlreadyDead,
	StartingClass,
	NoChange,
	Reduced,
	Increased,
}
