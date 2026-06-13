using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcResourceStatsService
{
	private const int DefaultMaxDp = 4000;

	private readonly WorldNpcLifeStatsService _npcLifeStats;
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly PlayerVisualStatsUpdateService? _playerVisualStats;

	public WorldNpcResourceStatsService(
		WorldNpcLifeStatsService npcLifeStats,
		IGameClientConnectionRegistry? connectionRegistry = null,
		PlayerVisualStatsUpdateService? playerVisualStats = null)
	{
		_npcLifeStats = npcLifeStats;
		_connectionRegistry = connectionRegistry;
		_playerVisualStats = playerVisualStats;
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

	public async ValueTask<WorldNpcResourceChangeResult> IncreaseNpcHpAsync(
		IWorldNpcObject? npc,
		int value,
		int skillId = 0,
		SmAttackStatusType? packetType = SmAttackStatusType.Hp,
		SmAttackStatusLog? packetLog = SmAttackStatusLog.Heal,
		bool targetHasDisease = false,
		int? killingBlow = null,
		Player? effector = null,
		WorldNpcDeathDropOptions? deathOptions = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/stats/container/CreatureLifeStats.increaseHp.
		if (npc == null)
			return WorldNpcResourceChangeResult.MissingTarget(WorldNpcEffectResourceType.Hp, WorldNpcResourceChangeKind.Increase, value);
		if (value < 0)
			return await ApplyNpcNegativeHealAsDamageAsync(npc, -value, skillId, packetType, packetLog, effector, deathOptions, cancellationToken);

		var mutation = _npcLifeStats.ApplyHpIncrease(npc.ObjectId, value, targetHasDisease, killingBlow);
		var previousValue = mutation.Previous?.CurrentHp ?? 0;
		var currentValue = mutation.Current?.CurrentHp ?? previousValue;
		var maxValue = mutation.Current?.MaxHp;
		var shouldSend = mutation.Status is not WorldNpcResourceChangeStatus.MissingStats
			and not WorldNpcResourceChangeStatus.BlockedByDisease
			and not WorldNpcResourceChangeStatus.AlreadyDead
			&& ShouldSendCreatureLifeStatsPacket(mutation.AppliedValue, skillId, packetType);
		var (packet, broadcastCount) = await BroadcastAttackStatusAsync(
			npc.Position,
			npc.ObjectId,
			packetType,
			packetLog,
			skillId,
			mutation.AppliedValue,
			mutation.Current?.GetHpPercentage() ?? 0,
			shouldSend,
			cancellationToken,
			usesNegativeValue: false);
		return WorldNpcResourceChangeResult.FromResourceMutation(
			mutation.Status,
			npc.ObjectId,
			WorldNpcEffectResourceType.Hp,
			WorldNpcResourceChangeKind.Increase,
			value,
			mutation.AppliedValue,
			previousValue,
			currentValue,
			maxValue,
			packetType,
			packetLog,
			packet,
			broadcastCount,
			NotifyHpObservers: mutation.AppliedValue != 0,
			KillingBlowReset: mutation.KillingBlowReset);
	}

	public async ValueTask<WorldNpcResourceChangeResult> IncreasePlayerHpAsync(
		Player? player,
		int maxHp,
		int value,
		int skillId = 0,
		SmAttackStatusType? packetType = SmAttackStatusType.Hp,
		SmAttackStatusLog? packetLog = SmAttackStatusLog.Heal,
		int? killingBlow = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/stats/container/PlayerLifeStats inherits CreatureLifeStats.increaseHp and adds player side effects in onHpChanged.
		if (player == null)
			return WorldNpcResourceChangeResult.MissingTarget(WorldNpcEffectResourceType.Hp, WorldNpcResourceChangeKind.Increase, value);
		if (player.LifeStats == null)
			return WorldNpcResourceChangeResult.MissingStats(player.ObjectId, WorldNpcEffectResourceType.Hp, WorldNpcResourceChangeKind.Increase, value);
		if (value < 0)
			return await ApplyPlayerNegativeHealAsDamageAsync(player, maxHp, -value, skillId, packetType, packetLog, cancellationToken);
		if (player.IsAbnormalSet(PlayerAbnormalState.Disease))
		{
			var blockedHp = player.LifeStats.GetCurrentHp(Math.Max(0, maxHp));
			return WorldNpcResourceChangeResult.FromResourceMutation(
				WorldNpcResourceChangeStatus.BlockedByDisease,
				player.ObjectId,
				WorldNpcEffectResourceType.Hp,
				WorldNpcResourceChangeKind.Increase,
				value,
				AppliedValue: 0,
				PreviousValue: blockedHp,
				CurrentValue: blockedHp,
				MaxValue: Math.Max(0, maxHp));
		}
		if (player.LifeStats.CurrentHp <= 0)
		{
			return WorldNpcResourceChangeResult.FromResourceMutation(
				WorldNpcResourceChangeStatus.AlreadyDead,
				player.ObjectId,
				WorldNpcEffectResourceType.Hp,
				WorldNpcResourceChangeKind.Increase,
				value,
				AppliedValue: 0,
				PreviousValue: player.LifeStats.CurrentHp,
				CurrentValue: player.LifeStats.CurrentHp,
				MaxValue: Math.Max(0, maxHp));
		}

		var normalizedMaxHp = Math.Max(0, maxHp);
		var previousHp = player.LifeStats.GetCurrentHp(normalizedMaxHp);
		var currentHp = Math.Min(previousHp + value, normalizedMaxHp);
		var appliedValue = Math.Max(0, currentHp - previousHp);
		var killingBlowReset = killingBlow is > 0 && currentHp > killingBlow.Value;
		if (appliedValue != 0)
			player.LifeStats = player.LifeStats with { CurrentHp = currentHp };
		var shouldSend = ShouldSendCreatureLifeStatsPacket(appliedValue, skillId, packetType);
		var (packet, broadcastCount) = await BroadcastAttackStatusAsync(
			player.GetPosition(),
			player.ObjectId,
			packetType,
			packetLog,
			skillId,
			appliedValue,
			GetHpPercentage(currentHp, normalizedMaxHp),
			shouldSend,
			cancellationToken,
			usesNegativeValue: false);
		var sendHpStatUpdate = player.IsOnline() && appliedValue != 0;
		var (hpStatUpdatePacket, hpStatUpdateSent) = await SendHpStatUpdateAsync(player, currentHp, normalizedMaxHp, sendHpStatUpdate);
		return WorldNpcResourceChangeResult.FromResourceMutation(
			appliedValue == 0 ? WorldNpcResourceChangeStatus.NoChange : WorldNpcResourceChangeStatus.Increased,
			player.ObjectId,
			WorldNpcEffectResourceType.Hp,
			WorldNpcResourceChangeKind.Increase,
			value,
			appliedValue,
			previousHp,
			currentHp,
			normalizedMaxHp,
			packetType,
			packetLog,
			packet,
			broadcastCount,
			SendHpStatUpdate: sendHpStatUpdate,
			HpStatUpdatePacket: hpStatUpdatePacket,
			HpStatUpdateSent: hpStatUpdateSent,
			SendGroupStatUpdate: player.IsOnline() && player.IsInTeam() && appliedValue != 0,
			NotifyHpObservers: appliedValue != 0,
			ClearAggroOnFullHp: appliedValue != 0 && currentHp == normalizedMaxHp,
			KillingBlowReset: killingBlowReset);
	}

	public async ValueTask<WorldNpcResourceChangeResult> AddPlayerDpAsync(
		Player? player,
		int value,
		int? maxDp = null)
	{
		// Java parity: model/gameobjects/player/PlayerCommonData.addDp/setDp.
		if (player == null)
			return WorldNpcResourceChangeResult.MissingTarget(WorldNpcEffectResourceType.Dp, WorldNpcResourceChangeKind.Increase, value);
		if (IsStartingClass(player.PlayerClass.ToString()))
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
		var resolvedMaxDp = maxDp ?? ResolveOnlineMaxDp(player);

		var previousDp = player.Dp;
		var requestedDp = previousDp + value;
		var currentDp = resolvedMaxDp is { } cap && requestedDp > cap
			? cap
			: requestedDp;
		player.Dp = currentDp;
		var shouldSendDpPackets = player.IsOnline;
		var (dpInfoPacket, dpInfoBroadcastCount) = await BroadcastDpInfoAsync(player, currentDp, shouldSendDpPackets);
		// Java order: PlayerCommonData.setDp broadcasts DP info, updates stats visually, then sends SM_STATUPDATE_DP.
		var visualStatsUpdate = await UpdatePlayerStatsAndSpeedVisuallyAsync(player, shouldSendDpPackets);
		var (dpStatUpdatePacket, dpStatUpdateSent) = await SendDpStatUpdateAsync(player, currentDp, shouldSendDpPackets);
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
			resolvedMaxDp,
			BroadcastDpInfo: shouldSendDpPackets,
			SendDpStatUpdate: shouldSendDpPackets,
			DpInfoPacket: dpInfoPacket,
			DpInfoBroadcastCount: dpInfoBroadcastCount,
			DpStatUpdatePacket: dpStatUpdatePacket,
			DpStatUpdateSent: dpStatUpdateSent,
			UpdateStatsAndSpeedVisually: shouldSendDpPackets,
			VisualStatsUpdate: visualStatsUpdate);
	}

	public async ValueTask<WorldNpcDpUseActionResult> SpendPlayerDpForSkillAsync(
		Player? player,
		int value,
		int? maxDp = null)
	{
		// Java parity: skillengine/action/DpUseAction.act.
		if (player == null)
			return WorldNpcDpUseActionResult.MissingTarget(value);

		var currentDp = player.Dp;
		if (currentDp <= 0 || currentDp < value)
		{
			var (packet, sent) = await SendSkillNotEnoughDpMessageAsync(player, player.IsOnline());
			return WorldNpcDpUseActionResult.NotEnoughDp(player.ObjectId, value, currentDp, packet, sent);
		}

		var change = await AddPlayerDpAsync(player, -value, maxDp);
		return WorldNpcDpUseActionResult.Applied(change, value);
	}

	public async ValueTask<WorldNpcDpTransferEffectResult> TransferPlayerDpAsync(
		Player? effector,
		Player? effected,
		int reservedDp,
		int? effectorMaxDp = null,
		int? effectedMaxDp = null)
	{
		// Java parity: skillengine/effect/DPTransferEffect.applyEffect.
		if (effected == null)
			return WorldNpcDpTransferEffectResult.MissingEffected(reservedDp);
		if (effector == null)
			return WorldNpcDpTransferEffectResult.MissingEffector(effected.ObjectId, reservedDp);

		var effectedChange = await AddPlayerDpAsync(effected, reservedDp, effectedMaxDp);
		var effectorChange = await AddPlayerDpAsync(effector, -reservedDp, effectorMaxDp);
		return WorldNpcDpTransferEffectResult.Applied(reservedDp, effectedChange, effectorChange);
	}

	public async ValueTask<WorldNpcReviveDpResetResult> ResetPlayerDpForReviveAsync(
		Player? player,
		bool hasNoResurrectPenalty,
		int? maxDp = null)
	{
		// Java parity: services/player/PlayerReviveService.revive DP reset branch.
		if (player == null)
			return WorldNpcReviveDpResetResult.MissingTarget(hasNoResurrectPenalty);

		var previousDp = player.Dp;
		if (hasNoResurrectPenalty)
			return WorldNpcReviveDpResetResult.NoResurrectPenalty(player.ObjectId, previousDp);
		if (previousDp <= 0)
			return WorldNpcReviveDpResetResult.NoDp(player.ObjectId, previousDp);

		var change = await AddPlayerDpAsync(player, -previousDp, maxDp);
		return WorldNpcReviveDpResetResult.FromDpChange(change, previousDp, hasNoResurrectPenalty);
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
			case WorldNpcEffectResourceType.Dp:
				if (target.Player == null)
					return WorldNpcResourceEffectApplicationResult.MissingTarget(resourceType, skillId);
				change = await AddPlayerDpAsync(target.Player, value, target.MaxDp);
				return WorldNpcResourceEffectApplicationResult.FromChange(change, skillId);
			case WorldNpcEffectResourceType.Hp:
				var hpValue = kind == WorldNpcResourceChangeKind.Reduce ? -value : value;
				if (target.Player != null)
				{
					if (target.MaxHp == null)
						return WorldNpcResourceEffectApplicationResult.MissingMaxResource(resourceType, skillId);
					change = await IncreasePlayerHpAsync(
						target.Player,
						target.MaxHp.Value,
						hpValue,
						skillId,
						packetType,
						packetLog,
						target.KillingBlow,
						cancellationToken);
					return WorldNpcResourceEffectApplicationResult.FromChange(change, skillId);
				}
				if (target.Npc != null)
				{
					change = await IncreaseNpcHpAsync(
						target.Npc,
						hpValue,
						skillId,
						packetType,
						packetLog,
						target.TargetHasDisease,
						target.KillingBlow,
						target.Effector,
						target.DeathOptions,
						cancellationToken);
					return WorldNpcResourceEffectApplicationResult.FromChange(change, skillId);
				}
				return WorldNpcResourceEffectApplicationResult.MissingTarget(resourceType, skillId);
			default:
				return WorldNpcResourceEffectApplicationResult.UnsupportedResource(resourceType, skillId);
		}
	}

	private async ValueTask<WorldNpcResourceChangeResult> ApplyNpcNegativeHealAsDamageAsync(
		IWorldNpcObject npc,
		int damage,
		int skillId,
		SmAttackStatusType? packetType,
		SmAttackStatusLog? packetLog,
		Player? effector,
		WorldNpcDeathDropOptions? deathOptions,
		CancellationToken cancellationToken)
	{
		if (!_npcLifeStats.TryGetStats(npc.ObjectId, out var stats) || stats == null)
			return WorldNpcResourceChangeResult.MissingStats(npc.ObjectId, WorldNpcEffectResourceType.Hp, WorldNpcResourceChangeKind.Reduce, damage);

		SmAttackStatus? attackStatusPacket = null;
		var attackStatusBroadcastCount = 0;
		var damageResult = await _npcLifeStats.ReduceHpAsync(
			npc,
			damage,
			stats.MaxHp,
			stats.MaxMp,
			effector,
			deathOptions: deathOptions,
			cancellationToken: cancellationToken,
			beforeDeathAsync: async (previous, current, token) =>
			{
				(attackStatusPacket, attackStatusBroadcastCount) = await BroadcastAttackStatusAsync(
					npc.Position,
					npc.ObjectId,
					packetType,
					packetLog,
					skillId,
					previous.CurrentHp - current.CurrentHp,
					current.GetHpPercentage(),
					ShouldSendCreatureLifeStatsPacket(previous.CurrentHp - current.CurrentHp, skillId, packetType),
					token,
					usesNegativeValue: true);
			});
		if (attackStatusPacket == null && damageResult.Previous != null && damageResult.Current != null)
		{
			var appliedDamage = damageResult.Previous.CurrentHp - damageResult.Current.CurrentHp;
			(attackStatusPacket, attackStatusBroadcastCount) = await BroadcastAttackStatusAsync(
				npc.Position,
				npc.ObjectId,
				packetType,
				packetLog,
				skillId,
				appliedDamage,
				damageResult.Current.GetHpPercentage(),
				ShouldSendCreatureLifeStatsPacket(appliedDamage, skillId, packetType),
				cancellationToken,
				usesNegativeValue: true);
		}

		var previousValue = damageResult.Previous?.CurrentHp ?? 0;
		var currentValue = damageResult.Current?.CurrentHp ?? previousValue;
		var appliedValue = Math.Max(0, previousValue - currentValue);
		return WorldNpcResourceChangeResult.FromResourceMutation(
			MapHpDamageStatus(damageResult.Status),
			npc.ObjectId,
			WorldNpcEffectResourceType.Hp,
			WorldNpcResourceChangeKind.Reduce,
			damage,
			appliedValue,
			previousValue,
			currentValue,
			damageResult.Current?.MaxHp ?? stats.MaxHp,
			packetType,
			packetLog,
			attackStatusPacket,
			attackStatusBroadcastCount,
			NotifyHpObservers: appliedValue != 0,
			RoutedNegativeHealToDamage: true);
	}

	private async ValueTask<WorldNpcResourceChangeResult> ApplyPlayerNegativeHealAsDamageAsync(
		Player player,
		int maxHp,
		int damage,
		int skillId,
		SmAttackStatusType? packetType,
		SmAttackStatusLog? packetLog,
		CancellationToken cancellationToken)
	{
		var normalizedMaxHp = Math.Max(0, maxHp);
		if (player.LifeStats == null)
			return WorldNpcResourceChangeResult.MissingStats(player.ObjectId, WorldNpcEffectResourceType.Hp, WorldNpcResourceChangeKind.Reduce, damage);
		if (player.LifeStats.CurrentHp <= 0)
		{
			return WorldNpcResourceChangeResult.FromResourceMutation(
				WorldNpcResourceChangeStatus.AlreadyDead,
				player.ObjectId,
				WorldNpcEffectResourceType.Hp,
				WorldNpcResourceChangeKind.Reduce,
				damage,
				AppliedValue: 0,
				PreviousValue: player.LifeStats.CurrentHp,
				CurrentValue: player.LifeStats.CurrentHp,
				MaxValue: normalizedMaxHp,
				RoutedNegativeHealToDamage: true);
		}

		var previousHp = player.LifeStats.GetCurrentHp(normalizedMaxHp);
		var currentHp = Math.Min(previousHp, Math.Max(previousHp - damage, 0));
		var appliedValue = previousHp - currentHp;
		player.LifeStats = player.LifeStats with
		{
			CurrentHp = currentHp,
			CurrentMp = currentHp == 0 ? 0 : player.LifeStats.CurrentMp,
		};
		var (packet, broadcastCount) = await BroadcastAttackStatusAsync(
			player.GetPosition(),
			player.ObjectId,
			packetType,
			packetLog,
			skillId,
			appliedValue,
			GetHpPercentage(currentHp, normalizedMaxHp),
			ShouldSendCreatureLifeStatsPacket(appliedValue, skillId, packetType),
			cancellationToken,
			usesNegativeValue: true);
		var sendHpStatUpdate = player.IsOnline() && appliedValue != 0;
		var (hpStatUpdatePacket, hpStatUpdateSent) = await SendHpStatUpdateAsync(player, currentHp, normalizedMaxHp, sendHpStatUpdate);
		return WorldNpcResourceChangeResult.FromResourceMutation(
			currentHp == 0 ? WorldNpcResourceChangeStatus.Died : appliedValue == 0 ? WorldNpcResourceChangeStatus.NoChange : WorldNpcResourceChangeStatus.Reduced,
			player.ObjectId,
			WorldNpcEffectResourceType.Hp,
			WorldNpcResourceChangeKind.Reduce,
			damage,
			appliedValue,
			previousHp,
			currentHp,
			normalizedMaxHp,
			packetType,
			packetLog,
			packet,
			broadcastCount,
			SendHpStatUpdate: sendHpStatUpdate,
			HpStatUpdatePacket: hpStatUpdatePacket,
			HpStatUpdateSent: hpStatUpdateSent,
			SendGroupStatUpdate: player.IsOnline() && player.IsInTeam() && appliedValue != 0,
			TriggerRestoreTask: player.IsOnline() && appliedValue != 0,
			NotifyHpObservers: appliedValue != 0,
			RoutedNegativeHealToDamage: true);
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
			cancellationToken,
			usesNegativeValue: kind == WorldNpcResourceChangeKind.Reduce);
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
			player.GetPosition(),
			player.ObjectId,
			packetType,
			packetLog,
			skillId,
			appliedValue,
			mpPercentage,
			ShouldSendCreatureLifeStatsPacket(appliedValue, skillId, packetType),
			cancellationToken,
			usesNegativeValue: kind == WorldNpcResourceChangeKind.Reduce);
		var sendMpStatUpdate = player.IsOnline() && appliedValue != 0;
		var (mpStatUpdatePacket, mpStatUpdateSent) = await SendMpStatUpdateAsync(player, currentMp, normalizedMaxMp, sendMpStatUpdate);
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
			broadcastCount,
			SendMpStatUpdate: sendMpStatUpdate,
			MpStatUpdatePacket: mpStatUpdatePacket,
			MpStatUpdateSent: mpStatUpdateSent,
			SendGroupStatUpdate: player.IsOnline() && player.IsInTeam() && appliedValue != 0,
			TriggerRestoreTask: player.IsOnline() && currentMp < previousMp);
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
			player.GetPosition(),
			player.ObjectId,
			packetType,
			packetLog,
			skillId,
			valueToSend,
			hpPercentage,
			shouldSendPacket,
			cancellationToken,
			usesNegativeValue: kind == WorldNpcResourceChangeKind.Reduce);
		var (flyTimePacket, flyTimeSent) = await SendFlyTimeUpdateAsync(player, currentFp, normalizedMaxFp, shouldSendFlyTimeUpdate && player.IsOnline());
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
			SendFlyTimeUpdate: shouldSendFlyTimeUpdate,
			FlyTimePacket: flyTimePacket,
			FlyTimeSent: flyTimeSent);
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
		CancellationToken cancellationToken,
		bool? usesNegativeValue = null)
	{
		if (!shouldSend || packetType == null)
			return (null, 0);

		var packet = new SmAttackStatus(
			objectId,
			packetType.Value,
			skillId,
			value,
			hpOrMpPercentage,
			packetLog ?? SmAttackStatusLog.Regular,
			usesNegativeValue);
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

	private async ValueTask<(SmStatUpdateHp? Packet, bool Sent)> SendHpStatUpdateAsync(
		Player player,
		int currentHp,
		int maxHp,
		bool shouldSend)
	{
		// Java parity: model/stats/container/PlayerLifeStats.sendHpPacketUpdate.
		if (!shouldSend)
			return (null, false);

		var packet = new SmStatUpdateHp(currentHp, maxHp);
		if (_connectionRegistry == null)
			return (packet, false);

		var sent = await _connectionRegistry.SendPacketToPlayerAsync(player.ObjectId, packet);
		return (packet, sent);
	}

	private async ValueTask<(SmStatUpdateMp? Packet, bool Sent)> SendMpStatUpdateAsync(
		Player player,
		int currentMp,
		int maxMp,
		bool shouldSend)
	{
		// Java parity: model/stats/container/PlayerLifeStats.sendMpPacketUpdate.
		if (!shouldSend)
			return (null, false);

		var packet = new SmStatUpdateMp(currentMp, maxMp);
		if (_connectionRegistry == null)
			return (packet, false);

		var sent = await _connectionRegistry.SendPacketToPlayerAsync(player.ObjectId, packet);
		return (packet, sent);
	}

	private async ValueTask<(SmFlyTime? Packet, bool Sent)> SendFlyTimeUpdateAsync(
		Player player,
		int currentFp,
		int maxFp,
		bool shouldSend)
	{
		// Java parity: model/stats/container/PlayerLifeStats.sendFpPacketUpdate.
		if (!shouldSend)
			return (null, false);

		var packet = new SmFlyTime(currentFp, maxFp);
		if (_connectionRegistry == null)
			return (packet, false);

		var sent = await _connectionRegistry.SendPacketToPlayerAsync(player.ObjectId, packet);
		return (packet, sent);
	}

	private async ValueTask<(SmDpInfo? Packet, int BroadcastCount)> BroadcastDpInfoAsync(
		Player player,
		int currentDp,
		bool shouldBroadcast)
	{
		// Java parity: model/gameobjects/player/PlayerCommonData.setDp -> PacketSendUtility.broadcastPacket(..., true).
		if (!shouldBroadcast)
			return (null, 0);

		var packet = new SmDpInfo(player.ObjectId, currentDp);
		if (_connectionRegistry == null)
			return (packet, 0);

		var count = await _connectionRegistry.BroadcastToVisiblePlayersAsync(
			player.GetPosition(),
			player.ObjectId,
			packet,
			includeSourcePlayer: true);
		return (packet, count);
	}

	private async ValueTask<(SmStatUpdateDp? Packet, bool Sent)> SendDpStatUpdateAsync(
		Player player,
		int currentDp,
		bool shouldSend)
	{
		// Java parity: model/gameobjects/player/PlayerCommonData.setDp -> PacketSendUtility.sendPacket.
		if (!shouldSend)
			return (null, false);

		var packet = new SmStatUpdateDp(currentDp);
		if (_connectionRegistry == null)
			return (packet, false);

		var sent = await _connectionRegistry.SendPacketToPlayerAsync(player.ObjectId, packet);
		return (packet, sent);
	}

	private async ValueTask<PlayerVisualStatsUpdateResult?> UpdatePlayerStatsAndSpeedVisuallyAsync(Player player, bool shouldSend)
	{
		// Java parity: PlayerCommonData.setDp -> player.getGameStats().updateStatsAndSpeedVisually().
		if (!shouldSend || _playerVisualStats == null)
			return null;
		return await _playerVisualStats.UpdateStatsAndSpeedVisuallyAsync(player, speedSnapshot: null);
	}

	private static int? ResolveOnlineMaxDp(Player player)
	{
		// Java parity: PlayerCommonData.setDp asks PlayerGameStats.getMaxDp().getCurrent() for online players.
		if (!player.IsOnline())
			return null;

		// Java parity: PlayerGameStats.getMaxDp() -> getStat(StatEnum.MAXDP, 4000). Full MAXDP modifiers remain deferred.
		return DefaultMaxDp;
	}

	private async ValueTask<(SmSystemMessage? Packet, bool Sent)> SendSkillNotEnoughDpMessageAsync(
		Player player,
		bool shouldSend)
	{
		// Java parity: skillengine/action/DpUseAction.act sends SM_SYSTEM_MESSAGE.STR_SKILL_NOT_ENOUGH_DP.
		if (!shouldSend)
			return (null, false);

		var packet = SmSystemMessage.SkillNotEnoughDp();
		if (_connectionRegistry == null)
			return (packet, false);

		var sent = await _connectionRegistry.SendPacketToPlayerAsync(player.ObjectId, packet);
		return (packet, sent);
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

	private static WorldNpcResourceChangeStatus MapHpDamageStatus(WorldNpcLifeStatsDamageStatus status)
	{
		return status switch
		{
			WorldNpcLifeStatsDamageStatus.NoChange => WorldNpcResourceChangeStatus.NoChange,
			WorldNpcLifeStatsDamageStatus.Reduced => WorldNpcResourceChangeStatus.Reduced,
			WorldNpcLifeStatsDamageStatus.Died => WorldNpcResourceChangeStatus.Died,
			WorldNpcLifeStatsDamageStatus.AlreadyDead => WorldNpcResourceChangeStatus.AlreadyDead,
			WorldNpcLifeStatsDamageStatus.MissingNpc => WorldNpcResourceChangeStatus.MissingTarget,
			_ => WorldNpcResourceChangeStatus.MissingStats,
		};
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
	int? MaxDp = null,
	bool TargetHasDisease = false,
	int? KillingBlow = null,
	Player? Effector = null,
	WorldNpcDeathDropOptions? DeathOptions = null);

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
			WorldNpcResourceChangeStatus.Died => WorldNpcResourceEffectApplicationStatus.TargetDead,
			WorldNpcResourceChangeStatus.BlockedByDisease => WorldNpcResourceEffectApplicationStatus.EffectSkipped,
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

public sealed record WorldNpcDpUseActionResult(
	WorldNpcDpUseActionStatus Status,
	int ObjectId,
	int RequestedValue,
	int CurrentDp,
	WorldNpcResourceChangeResult? Change = null,
	SmSystemMessage? SystemMessagePacket = null,
	bool SystemMessageSent = false)
{
	public static WorldNpcDpUseActionResult MissingTarget(int requestedValue)
	{
		return new WorldNpcDpUseActionResult(
			WorldNpcDpUseActionStatus.MissingTarget,
			0,
			requestedValue,
			0);
	}

	public static WorldNpcDpUseActionResult NotEnoughDp(
		int objectId,
		int requestedValue,
		int currentDp,
		SmSystemMessage? systemMessagePacket,
		bool systemMessageSent)
	{
		return new WorldNpcDpUseActionResult(
			WorldNpcDpUseActionStatus.NotEnoughDp,
			objectId,
			requestedValue,
			currentDp,
			null,
			systemMessagePacket,
			systemMessageSent);
	}

	public static WorldNpcDpUseActionResult Applied(WorldNpcResourceChangeResult change, int requestedValue)
	{
		return new WorldNpcDpUseActionResult(
			WorldNpcDpUseActionStatus.Applied,
			change.ObjectId,
			requestedValue,
			change.CurrentValue,
			change);
	}
}

public enum WorldNpcDpUseActionStatus
{
	Applied,
	MissingTarget,
	NotEnoughDp,
}

public sealed record WorldNpcDpTransferEffectResult(
	WorldNpcDpTransferEffectStatus Status,
	int ReservedDp,
	int EffectedObjectId,
	int EffectorObjectId,
	WorldNpcResourceChangeResult? EffectedChange = null,
	WorldNpcResourceChangeResult? EffectorChange = null)
{
	public static WorldNpcDpTransferEffectResult MissingEffected(int reservedDp)
	{
		return new WorldNpcDpTransferEffectResult(
			WorldNpcDpTransferEffectStatus.MissingEffected,
			reservedDp,
			EffectedObjectId: 0,
			EffectorObjectId: 0);
	}

	public static WorldNpcDpTransferEffectResult MissingEffector(int effectedObjectId, int reservedDp)
	{
		return new WorldNpcDpTransferEffectResult(
			WorldNpcDpTransferEffectStatus.MissingEffector,
			reservedDp,
			effectedObjectId,
			EffectorObjectId: 0);
	}

	public static WorldNpcDpTransferEffectResult Applied(
		int reservedDp,
		WorldNpcResourceChangeResult effectedChange,
		WorldNpcResourceChangeResult effectorChange)
	{
		return new WorldNpcDpTransferEffectResult(
			WorldNpcDpTransferEffectStatus.Applied,
			reservedDp,
			effectedChange.ObjectId,
			effectorChange.ObjectId,
			effectedChange,
			effectorChange);
	}
}

public enum WorldNpcDpTransferEffectStatus
{
	Applied,
	MissingEffected,
	MissingEffector,
}

public sealed record WorldNpcReviveDpResetResult(
	WorldNpcReviveDpResetStatus Status,
	int ObjectId,
	int PreviousDp,
	int CurrentDp,
	bool HasNoResurrectPenalty,
	WorldNpcResourceChangeResult? Change = null)
{
	public static WorldNpcReviveDpResetResult MissingTarget(bool hasNoResurrectPenalty)
	{
		return new WorldNpcReviveDpResetResult(
			WorldNpcReviveDpResetStatus.MissingTarget,
			0,
			PreviousDp: 0,
			CurrentDp: 0,
			hasNoResurrectPenalty);
	}

	public static WorldNpcReviveDpResetResult NoResurrectPenalty(int objectId, int currentDp)
	{
		return new WorldNpcReviveDpResetResult(
			WorldNpcReviveDpResetStatus.NoResurrectPenalty,
			objectId,
			currentDp,
			currentDp,
			HasNoResurrectPenalty: true);
	}

	public static WorldNpcReviveDpResetResult NoDp(int objectId, int currentDp)
	{
		return new WorldNpcReviveDpResetResult(
			WorldNpcReviveDpResetStatus.NoDp,
			objectId,
			currentDp,
			currentDp,
			HasNoResurrectPenalty: false);
	}

	public static WorldNpcReviveDpResetResult FromDpChange(
		WorldNpcResourceChangeResult change,
		int previousDp,
		bool hasNoResurrectPenalty)
	{
		var status = change.Status == WorldNpcResourceChangeStatus.Reduced
			? WorldNpcReviveDpResetStatus.Applied
			: WorldNpcReviveDpResetStatus.DpBoundarySkipped;
		return new WorldNpcReviveDpResetResult(
			status,
			change.ObjectId,
			previousDp,
			change.CurrentValue,
			hasNoResurrectPenalty,
			change);
	}
}

public enum WorldNpcReviveDpResetStatus
{
	Applied,
	MissingTarget,
	NoResurrectPenalty,
	NoDp,
	DpBoundarySkipped,
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
	SmDpInfo? DpInfoPacket = null,
	int DpInfoBroadcastCount = 0,
	SmStatUpdateDp? DpStatUpdatePacket = null,
	bool DpStatUpdateSent = false,
	bool UpdateStatsAndSpeedVisually = false,
	PlayerVisualStatsUpdateResult? VisualStatsUpdate = null,
	bool SendHpStatUpdate = false,
	SmStatUpdateHp? HpStatUpdatePacket = null,
	bool HpStatUpdateSent = false,
	bool SendMpStatUpdate = false,
	SmStatUpdateMp? MpStatUpdatePacket = null,
	bool MpStatUpdateSent = false,
	SmFlyTime? FlyTimePacket = null,
	bool FlyTimeSent = false,
	bool SendGroupStatUpdate = false,
	bool TriggerRestoreTask = false,
	bool TriggerFpRestore = false,
	bool NotifyHpObservers = false,
	bool ClearAggroOnFullHp = false,
	bool KillingBlowReset = false,
	bool RoutedNegativeHealToDamage = false)
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
		SmDpInfo? DpInfoPacket = null,
		int DpInfoBroadcastCount = 0,
		SmStatUpdateDp? DpStatUpdatePacket = null,
		bool DpStatUpdateSent = false,
		bool UpdateStatsAndSpeedVisually = false,
		PlayerVisualStatsUpdateResult? VisualStatsUpdate = null,
		bool SendHpStatUpdate = false,
		SmStatUpdateHp? HpStatUpdatePacket = null,
		bool HpStatUpdateSent = false,
		bool SendMpStatUpdate = false,
		SmStatUpdateMp? MpStatUpdatePacket = null,
		bool MpStatUpdateSent = false,
		SmFlyTime? FlyTimePacket = null,
		bool FlyTimeSent = false,
		bool SendGroupStatUpdate = false,
		bool TriggerRestoreTask = false,
		bool TriggerFpRestore = false,
		bool NotifyHpObservers = false,
		bool ClearAggroOnFullHp = false,
		bool KillingBlowReset = false,
		bool RoutedNegativeHealToDamage = false)
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
			DpInfoPacket,
			DpInfoBroadcastCount,
			DpStatUpdatePacket,
			DpStatUpdateSent,
			UpdateStatsAndSpeedVisually,
			VisualStatsUpdate,
			SendHpStatUpdate,
			HpStatUpdatePacket,
			HpStatUpdateSent,
			SendMpStatUpdate,
			MpStatUpdatePacket,
			MpStatUpdateSent,
			FlyTimePacket,
			FlyTimeSent,
			SendGroupStatUpdate,
			TriggerRestoreTask,
			TriggerFpRestore,
			NotifyHpObservers,
			ClearAggroOnFullHp,
			KillingBlowReset,
			RoutedNegativeHealToDamage);
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
	BlockedByDisease,
	AlreadyDead,
	StartingClass,
	NoChange,
	Reduced,
	Died,
	Increased,
}
