using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListAbnormalEffectSnapshotEntryFactoryServiceTests
{
	[Fact]
	public void Create_PreservesExplicitRemainingTimeAndPacketFacingFields()
	{
		var service = new PlayerKnownListAbnormalEffectSnapshotEntryFactoryService();

		var result = service.Create(new PlayerKnownListAbnormalEffectSnapshotEntryInput(
			EffectorObjectId: 7001,
			SkillId: 3001,
			SkillLevel: 4,
			TargetSlotId: 64,
			TargetSlotOrdinal: 6,
			IsNoShowToggle: true,
			RemainingTimeToDisplayMillis: -1,
			DurationMillis: 30_000,
			EndTimeUnixTimeMilliseconds: 45_000,
			NowUnixTimeMilliseconds: 15_000));

		Assert.Equal(PlayerKnownListAbnormalEffectSnapshotEntryFactoryStatus.Created, result.Status);
		Assert.True(result.UsedExplicitRemainingTime);
		Assert.False(result.UsedComputedRemainingTime);
		Assert.True(result.NeedsJavaEffectParity);
		Assert.Contains("SM_ABNORMAL_EFFECT", result.JavaSource);
		Assert.Equal(7001, result.Entry!.EffectorObjectId);
		Assert.Equal(3001, result.Entry.SkillId);
		Assert.Equal(4, result.Entry.SkillLevel);
		Assert.Equal(64, result.Entry.TargetSlotId);
		Assert.Equal(6, result.Entry.TargetSlotOrdinal);
		Assert.Equal(-1, result.Entry.RemainingTimeToDisplayMillis);
		Assert.True(result.Entry.IsNoShowToggle);
	}

	[Fact]
	public void Create_ComputesRemainingTimeFromSuppliedTimingSnapshot()
	{
		var service = new PlayerKnownListAbnormalEffectSnapshotEntryFactoryService();

		var result = service.Create(new PlayerKnownListAbnormalEffectSnapshotEntryInput(
			EffectorObjectId: 7001,
			SkillId: 3001,
			SkillLevel: 4,
			TargetSlotId: 1,
			TargetSlotOrdinal: 0,
			DurationMillis: 30_000,
			EndTimeUnixTimeMilliseconds: 45_000,
			NowUnixTimeMilliseconds: 15_000));

		Assert.Equal(PlayerKnownListAbnormalEffectSnapshotEntryFactoryStatus.Created, result.Status);
		Assert.False(result.UsedExplicitRemainingTime);
		Assert.True(result.UsedComputedRemainingTime);
		Assert.Equal(30_000, result.Entry!.RemainingTimeToDisplayMillis);
	}

	[Fact]
	public void Create_UsesJavaRemainingTimeSentinelsForComputedSnapshots()
	{
		var service = new PlayerKnownListAbnormalEffectSnapshotEntryFactoryService();

		var permanent = service.Create(new PlayerKnownListAbnormalEffectSnapshotEntryInput(
			EffectorObjectId: 7001,
			SkillId: 3001,
			SkillLevel: 4,
			TargetSlotId: 1,
			TargetSlotOrdinal: 0,
			DurationMillis: 0,
			EndTimeUnixTimeMilliseconds: 45_000,
			NowUnixTimeMilliseconds: 15_000));
		var longNpc = service.Create(new PlayerKnownListAbnormalEffectSnapshotEntryInput(
			EffectorObjectId: 7001,
			SkillId: 3002,
			SkillLevel: 4,
			TargetSlotId: 1,
			TargetSlotOrdinal: 0,
			DurationMillis: PlayerKnownListAbnormalEffectRemainingTimeDisplayService.NpcPermanentDisplayDurationMillis,
			EffectedIsNpc: true,
			EndTimeUnixTimeMilliseconds: 45_000,
			NowUnixTimeMilliseconds: 15_000));

		Assert.Equal(-1, permanent.Entry!.RemainingTimeToDisplayMillis);
		Assert.Equal(-1, longNpc.Entry!.RemainingTimeToDisplayMillis);
	}

	[Fact]
	public void Create_MissingTimingSnapshotReturnsExplicitBlockedMetadata()
	{
		var service = new PlayerKnownListAbnormalEffectSnapshotEntryFactoryService();

		var missingEndTime = service.Create(new PlayerKnownListAbnormalEffectSnapshotEntryInput(
			EffectorObjectId: 7001,
			SkillId: 3001,
			SkillLevel: 4,
			TargetSlotId: 1,
			TargetSlotOrdinal: 0,
			DurationMillis: 30_000,
			NowUnixTimeMilliseconds: 15_000));
		var missingNow = service.Create(new PlayerKnownListAbnormalEffectSnapshotEntryInput(
			EffectorObjectId: 7001,
			SkillId: 3001,
			SkillLevel: 4,
			TargetSlotId: 1,
			TargetSlotOrdinal: 0,
			DurationMillis: 30_000,
			EndTimeUnixTimeMilliseconds: 45_000));

		Assert.Equal(PlayerKnownListAbnormalEffectSnapshotEntryFactoryStatus.MissingTimingSnapshot, missingEndTime.Status);
		Assert.Null(missingEndTime.Entry);
		Assert.False(missingEndTime.UsedExplicitRemainingTime);
		Assert.False(missingEndTime.UsedComputedRemainingTime);
		Assert.True(missingEndTime.NeedsJavaEffectParity);
		Assert.Contains("timing snapshot inputs are incomplete", missingEndTime.Notes);
		Assert.Equal(PlayerKnownListAbnormalEffectSnapshotEntryFactoryStatus.MissingTimingSnapshot, missingNow.Status);
		Assert.Null(missingNow.Entry);
	}
}
