namespace Aion.GameServer.Services;

public sealed record PlayerKnownListAbnormalEffectSnapshotEntryInput(
	int EffectorObjectId,
	int SkillId,
	int SkillLevel,
	int TargetSlotId,
	int TargetSlotOrdinal,
	bool IsNoShowToggle = false,
	int? RemainingTimeToDisplayMillis = null,
	int? DurationMillis = null,
	long? EndTimeUnixTimeMilliseconds = null,
	bool EffectedIsNpc = false,
	long? NowUnixTimeMilliseconds = null);

public enum PlayerKnownListAbnormalEffectSnapshotEntryFactoryStatus
{
	Created,
	MissingTimingSnapshot,
}

public sealed record PlayerKnownListAbnormalEffectSnapshotEntryFactoryResult(
	PlayerKnownListAbnormalEffectSnapshotEntryFactoryStatus Status,
	PlayerKnownListAbnormalEffectSnapshotEntry? Entry,
	bool UsedExplicitRemainingTime,
	bool UsedComputedRemainingTime,
	bool NeedsJavaEffectParity,
	string JavaSource,
	string Notes);

public sealed class PlayerKnownListAbnormalEffectSnapshotEntryFactoryService
{
	public PlayerKnownListAbnormalEffectSnapshotEntryFactoryResult Create(
		PlayerKnownListAbnormalEffectSnapshotEntryInput input)
	{
		// Java parity breadcrumb: SM_ABNORMAL_EFFECT writes Effect fields plus
		// Effect.getRemainingTimeToDisplay(). This factory composes a supplied
		// packet-facing snapshot only; it never reads live EffectController state
		// or System.currentTimeMillis().
		const string javaSource =
			"com.aionemu.gameserver.network.aion.serverpackets.SM_ABNORMAL_EFFECT; "
			+ "com.aionemu.gameserver.skillengine.model.Effect.getRemainingTimeToDisplay";

		if (input.RemainingTimeToDisplayMillis is { } suppliedRemainingTime)
		{
			return Created(
				input,
				suppliedRemainingTime,
				UsedExplicitRemainingTime: true,
				UsedComputedRemainingTime: false,
				javaSource,
				"Created abnormal-effect snapshot entry using caller-supplied remaining display time.");
		}

		if (input.EndTimeUnixTimeMilliseconds is null || input.NowUnixTimeMilliseconds is null)
		{
			return new PlayerKnownListAbnormalEffectSnapshotEntryFactoryResult(
				PlayerKnownListAbnormalEffectSnapshotEntryFactoryStatus.MissingTimingSnapshot,
				Entry: null,
				UsedExplicitRemainingTime: false,
				UsedComputedRemainingTime: false,
				NeedsJavaEffectParity: true,
				javaSource,
				"Remaining display time was not supplied and timing snapshot inputs are incomplete. Duration, end time, effected creature type, and current time are needed to model Java Effect.getRemainingTimeToDisplay().");
		}

		var remainingTime = PlayerKnownListAbnormalEffectRemainingTimeDisplayService.Resolve(
			new PlayerKnownListAbnormalEffectRemainingTimeSnapshot(
				input.DurationMillis,
				input.EndTimeUnixTimeMilliseconds.Value,
				input.EffectedIsNpc,
				input.NowUnixTimeMilliseconds.Value));

		return Created(
			input,
			remainingTime,
			UsedExplicitRemainingTime: false,
			UsedComputedRemainingTime: true,
			javaSource,
			"Created abnormal-effect snapshot entry using deterministic Java-shaped remaining-time calculation from supplied timing snapshot.");
	}

	private static PlayerKnownListAbnormalEffectSnapshotEntryFactoryResult Created(
		PlayerKnownListAbnormalEffectSnapshotEntryInput input,
		int remainingTimeToDisplayMillis,
		bool UsedExplicitRemainingTime,
		bool UsedComputedRemainingTime,
		string javaSource,
		string notes) =>
		new(
			PlayerKnownListAbnormalEffectSnapshotEntryFactoryStatus.Created,
			new PlayerKnownListAbnormalEffectSnapshotEntry(
				input.EffectorObjectId,
				input.SkillId,
				input.SkillLevel,
				input.TargetSlotId,
				input.TargetSlotOrdinal,
				remainingTimeToDisplayMillis,
				input.IsNoShowToggle),
			UsedExplicitRemainingTime,
			UsedComputedRemainingTime,
			NeedsJavaEffectParity: true,
			javaSource,
			notes);
}
