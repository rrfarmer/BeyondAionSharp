using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListPacketConstructionFactPlanServiceTests
{
	[Fact]
	public void Plan_WithSuppliedSnapshotsCreatesNonLivePacketConstructionFacts()
	{
		var service = new PlayerKnownListPacketConstructionFactPlanService();
		var viewer = CreatePlayer(ViewerPlayerObjectId, "Viewer", "ASMODIANS");
		var subject = CreatePlayer(SubjectPlayerObjectId, "Subject", "ELYOS");
		subject.Motions =
		[
			new PlayerMotion(11, 1010, true),
			new PlayerMotion(12, 1010, false),
		];
		subject.MountRide(new PlayerRideInfo(RideNpcId, StartFp: 0, CostFp: null, SprintSpeed: 9.5f, FlySpeed: 10.5f, MoveSpeed: 7.25f));

		var plan = service.Plan(new PlayerKnownListPacketConstructionFactPlanRequest(
			viewer,
			subject,
			new PlayerKnownListOperationSideEffectDirectionFacts(
				ViewerAggroIconToSubject: true,
				SubjectIsInRideMode: true,
				SubjectRideNpcId: RideNpcId,
				SubjectIsUnderStance: true),
			ViewerIsEnemyToSubject: true,
			RideAttackSpeedFacts: new PlayerKnownListPacketConstructionAttackSpeedFacts(1400, 1200)));

		Assert.Equal(PlayerKnownListPacketConstructionFactPlanStatus.Complete, plan.Status);
		Assert.Empty(plan.Blockers);
		Assert.False(plan.ExecutesLivePackets);
		Assert.False(plan.IsLive);
		Assert.False(plan.IsJavaControllerParity);
		var facts = plan.Facts!;
		Assert.Same(subject, facts.SubjectPlayer);
		Assert.Equal([11], facts.ActiveMotions.Select(motion => motion.Id));
		Assert.Equal("ASMODIANS", facts.ViewerContext!.ActivePlayerRace);
		Assert.True(facts.ViewerContext.ActivePlayerIsEnemyToPlayer);
		Assert.Equal(7.25f, facts.RideMovementSpeed);
		Assert.Equal(1400, facts.RideBaseAttackSpeed);
		Assert.Equal(1200, facts.RideCurrentAttackSpeed);
		Assert.Null(facts.AbnormalEffects);
	}

	[Fact]
	public void Plan_RideSubjectWithoutRideInfoOrAttackSpeedBlocksFactConstruction()
	{
		var service = new PlayerKnownListPacketConstructionFactPlanService();
		var subject = CreatePlayer(SubjectPlayerObjectId, "Subject", "ELYOS");
		subject.IsInRideMode = true;

		var plan = service.Plan(new PlayerKnownListPacketConstructionFactPlanRequest(
			CreatePlayer(ViewerPlayerObjectId, "Viewer", "ELYOS"),
			subject,
			new PlayerKnownListOperationSideEffectDirectionFacts(SubjectIsInRideMode: true)));

		Assert.Equal(PlayerKnownListPacketConstructionFactPlanStatus.Blocked, plan.Status);
		Assert.Null(plan.Facts);
		Assert.Contains(PlayerKnownListPacketConstructionFactBlocker.MissingRideInfo, plan.Blockers);
		Assert.Contains(PlayerKnownListPacketConstructionFactBlocker.MissingRideAttackSpeedFacts, plan.Blockers);
	}

	[Fact]
	public void Plan_RideSubjectUsesResolvedAttackSpeedWhenSuppliedFactsAreMissing()
	{
		var service = new PlayerKnownListPacketConstructionFactPlanService();
		var subject = CreatePlayer(SubjectPlayerObjectId, "Subject", "ELYOS");
		subject.MountRide(new PlayerRideInfo(RideNpcId, StartFp: 0, CostFp: null, SprintSpeed: 9.5f, FlySpeed: 10.5f, MoveSpeed: 7.25f));

		var plan = service.Plan(new PlayerKnownListPacketConstructionFactPlanRequest(
			CreatePlayer(ViewerPlayerObjectId, "Viewer", "ELYOS"),
			subject,
			new PlayerKnownListOperationSideEffectDirectionFacts(
				SubjectIsInRideMode: true,
				SubjectRideNpcId: RideNpcId),
			RideAttackSpeedResolution: CreateResolvedAttackSpeed(1500, 1500)));

		Assert.Equal(PlayerKnownListPacketConstructionFactPlanStatus.Complete, plan.Status);
		Assert.Empty(plan.Blockers);
		Assert.Equal(PlayerKnownListPacketConstructionAttackSpeedFactSource.ResolvedApproximation, plan.RideAttackSpeedFactSource);
		Assert.Equal(PlayerKnownListAttackSpeedFactResolutionStatus.ResolvedApproximation, plan.RideAttackSpeedResolutionStatus);
		Assert.Equal(1500, plan.Facts!.RideBaseAttackSpeed);
		Assert.Equal(1500, plan.Facts.RideCurrentAttackSpeed);
		Assert.False(plan.ExecutesLivePackets);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void Plan_SuppliedRideAttackSpeedFactsRemainAuthoritativeOverResolvedApproximation()
	{
		var service = new PlayerKnownListPacketConstructionFactPlanService();
		var subject = CreatePlayer(SubjectPlayerObjectId, "Subject", "ELYOS");
		subject.MountRide(new PlayerRideInfo(RideNpcId, StartFp: 0, CostFp: null, SprintSpeed: 9.5f, FlySpeed: 10.5f, MoveSpeed: 7.25f));

		var plan = service.Plan(new PlayerKnownListPacketConstructionFactPlanRequest(
			CreatePlayer(ViewerPlayerObjectId, "Viewer", "ELYOS"),
			subject,
			new PlayerKnownListOperationSideEffectDirectionFacts(
				SubjectIsInRideMode: true,
				SubjectRideNpcId: RideNpcId),
			RideAttackSpeedFacts: new PlayerKnownListPacketConstructionAttackSpeedFacts(1400, 1200),
			RideAttackSpeedResolution: CreateResolvedAttackSpeed(1500, 1500)));

		Assert.Equal(PlayerKnownListPacketConstructionFactPlanStatus.Complete, plan.Status);
		Assert.Equal(PlayerKnownListPacketConstructionAttackSpeedFactSource.Supplied, plan.RideAttackSpeedFactSource);
		Assert.Equal(PlayerKnownListAttackSpeedFactResolutionStatus.ResolvedApproximation, plan.RideAttackSpeedResolutionStatus);
		Assert.Equal(1400, plan.Facts!.RideBaseAttackSpeed);
		Assert.Equal(1200, plan.Facts.RideCurrentAttackSpeed);
	}

	[Fact]
	public void Plan_NonRideSubjectDoesNotConsumeAttackSpeedMetadata()
	{
		var service = new PlayerKnownListPacketConstructionFactPlanService();

		var plan = service.Plan(new PlayerKnownListPacketConstructionFactPlanRequest(
			CreatePlayer(ViewerPlayerObjectId, "Viewer", "ELYOS"),
			CreatePlayer(SubjectPlayerObjectId, "Subject", "ELYOS"),
			new PlayerKnownListOperationSideEffectDirectionFacts(),
			RideAttackSpeedFacts: new PlayerKnownListPacketConstructionAttackSpeedFacts(1400, 1200),
			RideAttackSpeedResolution: CreateResolvedAttackSpeed(1500, 1500)));

		Assert.Equal(PlayerKnownListPacketConstructionFactPlanStatus.Complete, plan.Status);
		Assert.Equal(PlayerKnownListPacketConstructionAttackSpeedFactSource.None, plan.RideAttackSpeedFactSource);
		Assert.Equal(PlayerKnownListAttackSpeedFactResolutionStatus.ResolvedApproximation, plan.RideAttackSpeedResolutionStatus);
		Assert.Equal(0, plan.Facts!.RideBaseAttackSpeed);
		Assert.Equal(0, plan.Facts.RideCurrentAttackSpeed);
	}

	[Fact]
	public void Plan_RideSubjectWithBlockedAttackSpeedResolutionKeepsMissingFactBlocker()
	{
		var service = new PlayerKnownListPacketConstructionFactPlanService();
		var subject = CreatePlayer(SubjectPlayerObjectId, "Subject", "ELYOS");
		subject.MountRide(new PlayerRideInfo(RideNpcId, StartFp: 0, CostFp: null, SprintSpeed: 9.5f, FlySpeed: 10.5f, MoveSpeed: 7.25f));

		var plan = service.Plan(new PlayerKnownListPacketConstructionFactPlanRequest(
			CreatePlayer(ViewerPlayerObjectId, "Viewer", "ELYOS"),
			subject,
			new PlayerKnownListOperationSideEffectDirectionFacts(
				SubjectIsInRideMode: true,
				SubjectRideNpcId: RideNpcId),
			RideAttackSpeedResolution: CreateBlockedAttackSpeedResolution()));

		Assert.Equal(PlayerKnownListPacketConstructionFactPlanStatus.Blocked, plan.Status);
		Assert.Null(plan.Facts);
		Assert.Contains(PlayerKnownListPacketConstructionFactBlocker.MissingRideAttackSpeedFacts, plan.Blockers);
		Assert.Equal(PlayerKnownListPacketConstructionAttackSpeedFactSource.None, plan.RideAttackSpeedFactSource);
		Assert.Equal(PlayerKnownListAttackSpeedFactResolutionStatus.MissingItemTemplates, plan.RideAttackSpeedResolutionStatus);
	}

	[Fact]
	public void Plan_AbnormalEffectsWithoutEntriesOrMaskBlocksFactConstruction()
	{
		var service = new PlayerKnownListPacketConstructionFactPlanService();

		var plan = service.Plan(new PlayerKnownListPacketConstructionFactPlanRequest(
			CreatePlayer(ViewerPlayerObjectId, "Viewer", "ELYOS"),
			CreatePlayer(SubjectPlayerObjectId, "Subject", "ELYOS"),
			new PlayerKnownListOperationSideEffectDirectionFacts(SubjectHasAbnormalEffects: true)));

		Assert.Equal(PlayerKnownListPacketConstructionFactPlanStatus.Blocked, plan.Status);
		Assert.Null(plan.Facts);
		Assert.Equal([PlayerKnownListPacketConstructionFactBlocker.MissingAbnormalEffectFacts], plan.Blockers);
	}

	[Fact]
	public void Plan_AbnormalEffectsWithSuppliedEntriesAndMaskCreatesFacts()
	{
		var service = new PlayerKnownListPacketConstructionFactPlanService();
		var effects = new[]
		{
			new SmAbnormalEffectEntry(
				EffectorObjectId: 7001,
				SkillId: 1200,
				SkillLevel: 3,
				TargetSlotId: 1,
				TargetSlotOrdinal: 0,
				RemainingTimeToDisplayMillis: 30_000),
		};

		var plan = service.Plan(new PlayerKnownListPacketConstructionFactPlanRequest(
			CreatePlayer(ViewerPlayerObjectId, "Viewer", "ELYOS"),
			CreatePlayer(SubjectPlayerObjectId, "Subject", "ELYOS"),
			new PlayerKnownListOperationSideEffectDirectionFacts(SubjectHasAbnormalEffects: true),
			AbnormalEffects: effects,
			AbnormalEffectMask: 0x80,
			AbnormalEffectSlots: 1));

		Assert.Equal(PlayerKnownListPacketConstructionFactPlanStatus.Complete, plan.Status);
		Assert.Same(effects, plan.Facts!.AbnormalEffects);
		Assert.Equal(0x80, plan.Facts.AbnormalEffectMask);
		Assert.Equal(1, plan.Facts.AbnormalEffectSlots);
	}

	[Fact]
	public void Plan_MissingViewerOrSubjectBlocksExplicitly()
	{
		var service = new PlayerKnownListPacketConstructionFactPlanService();

		var plan = service.Plan(new PlayerKnownListPacketConstructionFactPlanRequest(
			ViewerPlayer: null,
			SubjectPlayer: null,
			new PlayerKnownListOperationSideEffectDirectionFacts()));

		Assert.Equal(PlayerKnownListPacketConstructionFactPlanStatus.Blocked, plan.Status);
		Assert.Null(plan.Facts);
		Assert.Equal(
			[
				PlayerKnownListPacketConstructionFactBlocker.MissingViewerPlayer,
				PlayerKnownListPacketConstructionFactBlocker.MissingSubjectPlayer,
			],
			plan.Blockers);
	}

	private static Player CreatePlayer(int objectId, string name, string race) =>
		new()
		{
			ObjectId = objectId,
			Name = name,
			Race = race,
			Gender = "MALE",
			PlayerClass = "GLADIATOR",
			Position = new WorldPosition(210010000, 1, 2, 3, 4),
		};

	private static PlayerKnownListAttackSpeedFactResolution CreateResolvedAttackSpeed(
		int baseAttackSpeed,
		int currentAttackSpeed) =>
		new(
			PlayerKnownListAttackSpeedFactResolutionStatus.ResolvedApproximation,
			new PlayerKnownListPacketConstructionAttackSpeedFacts(baseAttackSpeed, currentAttackSpeed),
			NeedsJavaStatParity: true,
			IsLive: false,
			IsJavaStatParity: false,
			"com.aionemu.gameserver.model.stats.container.PlayerGameStats.getAttackSpeed",
			"Resolved by disabled approximation.");

	private static PlayerKnownListAttackSpeedFactResolution CreateBlockedAttackSpeedResolution() =>
		new(
			PlayerKnownListAttackSpeedFactResolutionStatus.MissingItemTemplates,
			Facts: null,
			NeedsJavaStatParity: true,
			IsLive: false,
			IsJavaStatParity: false,
			"com.aionemu.gameserver.model.stats.container.PlayerGameStats.getAttackSpeed",
			"Item templates missing.");

	private const int ViewerPlayerObjectId = 9001;
	private const int SubjectPlayerObjectId = 9002;
	private const int RideNpcId = 730001;
}
