using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SummonCreatePlanServiceTests
{
	[Fact]
	public void CreatePlan_BlocksAlreadyHasFollowerWithJavaSystemMessage()
	{
		var plan = SummonCreatePlanService.CreatePlan(
			alreadyHasSummon: true,
			panelSnapshot: CreatePanelSnapshot(),
			emotionSnapshot: CreateEmotionSnapshot(),
			updateSnapshot: CreateUpdateSnapshot());

		Assert.Equal(SummonCreatePlanStatus.BlockedAlreadyHasFollower, plan.Status);
		Assert.False(plan.IsLive);
		Assert.NotNull(plan.AlreadyHasFollowerMessage);
		Assert.Equal(1300072, plan.AlreadyHasFollowerMessage!.MessageId);
		Assert.False(plan.ShouldSendPanelToMaster);
		Assert.False(plan.ShouldBroadcastEmotionFromSummon);
		Assert.False(plan.ShouldBroadcastUpdateFromSummon);
		Assert.Null(plan.SummonPanelPacket);
		Assert.Null(plan.ChangeSpeedEmotionPacket);
		Assert.Null(plan.SummonUpdatePacket);
		Assert.Contains("STR_SKILL_SUMMON_ALREADY_HAVE_A_FOLLOWER", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_ComposesPanelEmotionUpdateInJavaOrder()
	{
		var plan = SummonCreatePlanService.CreatePlan(
			alreadyHasSummon: false,
			panelSnapshot: CreatePanelSnapshot(),
			emotionSnapshot: CreateEmotionSnapshot(),
			updateSnapshot: CreateUpdateSnapshot());

		Assert.Equal(SummonCreatePlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.Null(plan.AlreadyHasFollowerMessage);
		Assert.NotNull(plan.SummonPanelPacket);
		Assert.NotNull(plan.ChangeSpeedEmotionPacket);
		Assert.NotNull(plan.SummonUpdatePacket);
		Assert.True(plan.ShouldSendPanelToMaster);
		Assert.True(plan.ShouldBroadcastEmotionFromSummon);
		Assert.True(plan.ShouldBroadcastUpdateFromSummon);
		Assert.Contains("SM_SUMMON_PANEL", plan.JavaSource);
		Assert.Contains("CHANGE_SPEED", plan.JavaSource);
		Assert.Contains("SM_SUMMON_UPDATE", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_OmitsPanelWhenSnapshotIsNull()
	{
		var plan = SummonCreatePlanService.CreatePlan(
			alreadyHasSummon: false,
			panelSnapshot: null,
			emotionSnapshot: CreateEmotionSnapshot(),
			updateSnapshot: CreateUpdateSnapshot());

		Assert.Equal(SummonCreatePlanStatus.PlanCreated, plan.Status);
		Assert.Null(plan.SummonPanelPacket);
		Assert.False(plan.ShouldSendPanelToMaster);
		Assert.NotNull(plan.ChangeSpeedEmotionPacket);
		Assert.NotNull(plan.SummonUpdatePacket);
	}

	[Fact]
	public void CreatePlan_OmitsEmotionWhenSnapshotIsNull()
	{
		var plan = SummonCreatePlanService.CreatePlan(
			alreadyHasSummon: false,
			panelSnapshot: CreatePanelSnapshot(),
			emotionSnapshot: null,
			updateSnapshot: CreateUpdateSnapshot());

		Assert.Equal(SummonCreatePlanStatus.PlanCreated, plan.Status);
		Assert.NotNull(plan.SummonPanelPacket);
		Assert.Null(plan.ChangeSpeedEmotionPacket);
		Assert.False(plan.ShouldBroadcastEmotionFromSummon);
		Assert.NotNull(plan.SummonUpdatePacket);
	}

	[Fact]
	public void CreatePlan_BlocksInvalidSummonUpdateSnapshot()
	{
		var invalidUpdate = CreateUpdateSnapshot() with { MagicCritical = new SummonUpdateStatSnapshot(Current: 1, Base: -1) };

		var plan = SummonCreatePlanService.CreatePlan(
			alreadyHasSummon: false,
			panelSnapshot: CreatePanelSnapshot(),
			emotionSnapshot: CreateEmotionSnapshot(),
			updateSnapshot: invalidUpdate);

		Assert.Equal(SummonCreatePlanStatus.BlockedInvalidSummonUpdateSnapshot, plan.Status);
		Assert.False(plan.ShouldSendPanelToMaster);
		Assert.False(plan.ShouldBroadcastEmotionFromSummon);
		Assert.False(plan.ShouldBroadcastUpdateFromSummon);
		Assert.Null(plan.SummonUpdatePacket);
	}

	[Fact]
	public void CreatePlan_EmotionSnapshotUsesChangeSpeedEmotionType()
	{
		// Java: PacketSendUtility.broadcastPacket(summon, new SM_EMOTION(summon, EmotionType.CHANGE_SPEED))
		var plan = SummonCreatePlanService.CreatePlan(
			alreadyHasSummon: false,
			panelSnapshot: null,
			emotionSnapshot: CreateEmotionSnapshot(summonObjectId: 9001, creatureState: 0, movementSpeed: 5.5f, baseAttack: 1200, currentAttack: 1200),
			updateSnapshot: null);

		Assert.Equal(SummonCreatePlanStatus.PlanCreated, plan.Status);
		Assert.NotNull(plan.ChangeSpeedEmotionPacket);
	}

	private static SummonPanelSnapshot CreatePanelSnapshot(int objectId = 5001)
	{
		return new SummonPanelSnapshot(
			ObjectId: objectId,
			Level: 55,
			CurrentHp: 7100,
			MaxHp: 8200,
			MainHandPhysicalAttack: 415,
			PhysicalDefense: 771,
			MagicResist: 490,
			LiveTime: 7200000);
	}

	private static SummonCreatedEmotionSnapshot CreateEmotionSnapshot(
		int summonObjectId = 5001,
		int creatureState = 0,
		float movementSpeed = 5.5f,
		int baseAttack = 1200,
		int currentAttack = 1200)
	{
		return new SummonCreatedEmotionSnapshot(
			SummonObjectId: summonObjectId,
			CreatureState: creatureState,
			MovementSpeed: movementSpeed,
			BaseAttackSpeed: baseAttack,
			CurrentAttackSpeed: currentAttack);
	}

	private static SummonUpdateSnapshot CreateUpdateSnapshot()
	{
		return new SummonUpdateSnapshot(
			Level: 55,
			Mode: SummonUpdateModeId.Release,
			CurrentHp: 7100,
			MaxHp: new SummonUpdateStatSnapshot(Current: 8200, Base: 7600),
			MainHandPhysicalAttack: new SummonUpdateStatSnapshot(Current: 415, Base: 390),
			PhysicalDefense: new SummonUpdateStatSnapshot(Current: 771, Base: 720),
			MagicResist: new SummonUpdateStatSnapshot(Current: 490, Base: 440),
			MagicDefense: new SummonUpdateStatSnapshot(Current: 615, Base: 580),
			MainHandPhysicalAccuracy: new SummonUpdateStatSnapshot(Current: 1320, Base: 1275),
			MainHandPhysicalCritical: new SummonUpdateStatSnapshot(Current: 250, Base: 230),
			MagicBoost: new SummonUpdateStatSnapshot(Current: 950, Base: 900),
			MagicBoostResist: new SummonUpdateStatSnapshot(Current: 85, Base: 70),
			MagicAccuracy: new SummonUpdateStatSnapshot(Current: 1100, Base: 1050),
			MagicCritical: new SummonUpdateStatSnapshot(Current: 120, Base: 100),
			Parry: new SummonUpdateStatSnapshot(Current: 330, Base: 300),
			Evasion: new SummonUpdateStatSnapshot(Current: 405, Base: 375));
	}
}
