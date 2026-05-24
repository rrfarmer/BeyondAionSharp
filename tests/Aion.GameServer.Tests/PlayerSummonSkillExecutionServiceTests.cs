using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;
using Aion.Commons.Network;

namespace Aion.GameServer.Tests;

public class PlayerSummonSkillExecutionServiceTests
{
	[Fact]
	public void ProjectMercenaryNpcSkillTemplate_MapsJavaTemplateDefaultsAndOverrides()
	{
		var service = new PlayerSummonSkillExecutionService();

		var defaultProjection = service.ProjectMercenaryNpcSkillTemplate(new PlayerSummonKnownObjectNpcSkillTemplateMetadata());
		var condition = new PlayerSummonKnownObjectNpcSkillConditionMetadata(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsInRange,
			HpBelowPercentage: 35,
			RangeMeters: 18,
			NpcId: 212345,
			DelayMilliseconds: 750,
			CanDie: false,
			DespawnTimeMilliseconds: 1200);
		var spawn = new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
			NpcId: 212346,
			DelayMilliseconds: 500,
			MinDistance: 3,
			MaxDistance: 8,
			MinCount: 2,
			MaxCount: 5);
		var overrideTemplate = new PlayerSummonKnownObjectNpcSkillTemplateMetadata(
			SkillId: 1001,
			SkillLevel: 3,
			Probability: 70,
			MinHpPercentage: 25,
			MaxHpPercentage: 80,
			MaxTimeMilliseconds: 9000,
			MinTimeMilliseconds: 3000,
			ConjunctionType: PlayerSummonKnownObjectNpcSkillConjunction.Or,
			CooldownMilliseconds: 4000,
			IsPostSpawn: true,
			Priority: 12,
			NextSkillTimeMilliseconds: 6500,
			ConditionTemplate: condition,
			NextChainId: 3002,
			ChainId: 3001,
			MaxChainTimeMilliseconds: 22000,
			SpawnTemplate: spawn,
			Target: PlayerSummonKnownObjectNpcSkillTargetAttribute.Random);
		var overrideProjection = service.ProjectMercenaryNpcSkillTemplate(overrideTemplate, lastTimeUsedMilliseconds: 123456);

		Assert.Equal(new PlayerSummonKnownObjectNpcSkillEntryTiming(), defaultProjection.EntryTiming);
		Assert.Equal(new PlayerSummonKnownObjectNpcSkillConditionMetadata(), defaultProjection.ConditionTemplate);
		Assert.Equal(PlayerSummonKnownObjectSkillTargetMode.MostHated, defaultProjection.TargetMode);
		Assert.Equal(0, defaultProjection.Probability);
		Assert.Equal(0, defaultProjection.Priority);
		Assert.Equal(-1, defaultProjection.NextSkillTimeMilliseconds);
		Assert.Equal(0, defaultProjection.NextChainId);
		Assert.Equal(0, defaultProjection.ChainId);
		Assert.Equal(15000, defaultProjection.MaxChainTimeMilliseconds);
		Assert.False(defaultProjection.IsPostSpawn);
		Assert.Null(defaultProjection.SpawnTemplate);

		Assert.Equal(25, overrideProjection.EntryTiming.MinHpPercentage);
		Assert.Equal(80, overrideProjection.EntryTiming.MaxHpPercentage);
		Assert.Equal(3000, overrideProjection.EntryTiming.MinTimeMilliseconds);
		Assert.Equal(9000, overrideProjection.EntryTiming.MaxTimeMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConjunction.Or, overrideProjection.EntryTiming.ConjunctionType);
		Assert.Equal(4000, overrideProjection.EntryTiming.CooldownMilliseconds);
		Assert.Equal(123456, overrideProjection.EntryTiming.LastTimeUsedMilliseconds);
		Assert.Equal(condition, overrideProjection.ConditionTemplate);
		Assert.Equal(PlayerSummonKnownObjectSkillTargetMode.CreatureTarget, overrideProjection.TargetMode);
		Assert.Equal(70, overrideProjection.Probability);
		Assert.Equal(12, overrideProjection.Priority);
		Assert.Equal(6500, overrideProjection.NextSkillTimeMilliseconds);
		Assert.Equal(3002, overrideProjection.NextChainId);
		Assert.Equal(3001, overrideProjection.ChainId);
		Assert.Equal(22000, overrideProjection.MaxChainTimeMilliseconds);
		Assert.True(overrideProjection.IsPostSpawn);
		Assert.Equal(spawn, overrideProjection.SpawnTemplate);
	}

	[Fact]
	public void PreviewMercenaryNpcSkillPostSpawn_ProjectsJavaFireOnEndCastEvents()
	{
		var service = new PlayerSummonSkillExecutionService();
		var immediateSpawn = new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
			NpcId: 212347,
			DelayMilliseconds: 0,
			MinDistance: 0,
			MaxDistance: 0,
			MinCount: 3,
			MaxCount: 0);
		var delayedRandomSpawn = new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
			NpcId: 212348,
			DelayMilliseconds: 1500,
			MinDistance: 4,
			MaxDistance: 9,
			MinCount: 1,
			MaxCount: 4);
		var noSpawnSkill = service.ProjectMercenaryNpcSkillTemplate(new PlayerSummonKnownObjectNpcSkillTemplateMetadata());
		var immediateSkill = service.ProjectMercenaryNpcSkillTemplate(
			new PlayerSummonKnownObjectNpcSkillTemplateMetadata(SpawnTemplate: immediateSpawn));
		var delayedSkill = service.ProjectMercenaryNpcSkillTemplate(
			new PlayerSummonKnownObjectNpcSkillTemplateMetadata(SpawnTemplate: delayedRandomSpawn));

		var noSpawn = service.PreviewMercenaryNpcSkillPostSpawn(noSpawnSkill);
		var ownerDead = service.PreviewMercenaryNpcSkillPostSpawn(immediateSkill, ownerIsDead: true);
		var immediate = service.PreviewMercenaryNpcSkillPostSpawn(immediateSkill);
		var delayed = service.PreviewMercenaryNpcSkillPostSpawn(delayedSkill);
		var missingSchedule = service.PreviewMercenaryNpcSkillPostSpawnSchedule(null, currentTimeMilliseconds: 20_000);
		var immediateSchedule = service.PreviewMercenaryNpcSkillPostSpawnSchedule(immediate, currentTimeMilliseconds: 20_000);
		var delayedSchedule = service.PreviewMercenaryNpcSkillPostSpawnSchedule(delayed, currentTimeMilliseconds: 20_000);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus.NoSpawnTemplate, noSpawn.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus.OwnerNotReady, ownerDead.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus.ImmediateSpawn, immediate.Status);
		Assert.True(immediate.ShouldSpawnImmediately);
		Assert.False(immediate.ShouldScheduleSpawn);
		Assert.False(immediate.RequiresRandomCount);
		Assert.False(immediate.RequiresRandomDistance);
		Assert.False(immediate.RequiresRandomAngle);
		Assert.Equal(3, immediate.EffectiveMinCount);
		Assert.Equal(3, immediate.EffectiveMaxCount);
		Assert.Equal(0, immediate.EffectiveMinDistance);
		Assert.Equal(0, immediate.EffectiveMaxDistance);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus.DelayedSpawn, delayed.Status);
		Assert.True(delayed.ShouldScheduleSpawn);
		Assert.True(delayed.RequiresOwnerAliveRecheck);
		Assert.True(delayed.RequiresRandomCount);
		Assert.True(delayed.RequiresRandomDistance);
		Assert.True(delayed.RequiresRandomAngle);
		Assert.Equal(1, delayed.EffectiveMinCount);
		Assert.Equal(4, delayed.EffectiveMaxCount);
		Assert.Equal(4, delayed.EffectiveMinDistance);
		Assert.Equal(9, delayed.EffectiveMaxDistance);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillPostSpawnScheduleStatus.MissingPreview, missingSchedule.Status);
		Assert.False(missingSchedule.WouldScheduleTask);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillPostSpawnScheduleStatus.NotScheduled, immediateSchedule.Status);
		Assert.False(immediateSchedule.WouldScheduleTask);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillPostSpawnScheduleStatus.Scheduled, delayedSchedule.Status);
		Assert.True(delayedSchedule.WouldScheduleTask);
		Assert.True(delayedSchedule.RequiresOwnerAliveRecheck);
		Assert.Equal(1500, delayedSchedule.DelayMilliseconds);
		Assert.Equal(21_500, delayedSchedule.ScheduledAtMilliseconds);
	}

	[Fact]
	public void PreviewMercenaryNpcSkillPostSpawnExecution_ProjectsJavaSpawnNpcInputs()
	{
		var service = new PlayerSummonSkillExecutionService();
		var origin = new PlayerSummonKnownObjectNpcSkillSpawnOrigin(
			WorldId: 210010000,
			InstanceId: 12,
			X: 100.5f,
			Y: 200.25f,
			Z: 35.75f,
			Heading: 90);
		var immediateSpawn = new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
			NpcId: 212350,
			DelayMilliseconds: 0,
			MinDistance: 0,
			MaxDistance: 0,
			MinCount: 2,
			MaxCount: 0);
		var delayedRandomSpawn = new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
			NpcId: 212351,
			DelayMilliseconds: 1250,
			MinDistance: 3,
			MaxDistance: 7,
			MinCount: 1,
			MaxCount: 4);
		var noSpawn = service.PreviewMercenaryNpcSkillPostSpawn(
			service.ProjectMercenaryNpcSkillTemplate(new PlayerSummonKnownObjectNpcSkillTemplateMetadata()));
		var immediate = service.PreviewMercenaryNpcSkillPostSpawn(
			service.ProjectMercenaryNpcSkillTemplate(new PlayerSummonKnownObjectNpcSkillTemplateMetadata(SpawnTemplate: immediateSpawn)));
		var delayed = service.PreviewMercenaryNpcSkillPostSpawn(
			service.ProjectMercenaryNpcSkillTemplate(new PlayerSummonKnownObjectNpcSkillTemplateMetadata(SpawnTemplate: delayedRandomSpawn)));

		var missingPreview = service.PreviewMercenaryNpcSkillPostSpawnExecution(null, origin);
		var noSpawnExecution = service.PreviewMercenaryNpcSkillPostSpawnExecution(noSpawn, origin);
		var missingOrigin = service.PreviewMercenaryNpcSkillPostSpawnExecution(immediate, origin: null);
		var immediateExecution = service.PreviewMercenaryNpcSkillPostSpawnExecution(immediate, origin);
		var delayedExecution = service.PreviewMercenaryNpcSkillPostSpawnExecution(delayed, origin);
		var delayedOwnerDead = service.PreviewMercenaryNpcSkillPostSpawnExecution(delayed, origin, ownerIsDead: true);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnExecutionPreviewStatus.MissingPreview, missingPreview.Status);
		Assert.False(missingPreview.WouldSpawn);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnExecutionPreviewStatus.NoSpawn, noSpawnExecution.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnExecutionPreviewStatus.MissingOrigin, missingOrigin.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnExecutionPreviewStatus.WouldSpawn, immediateExecution.Status);
		Assert.True(immediateExecution.RequiresSpawnEngine);
		Assert.True(immediateExecution.RequiresInstanceSpawn);
		Assert.True(immediateExecution.UsesOwnerPosition);
		Assert.Same(origin, immediateExecution.Origin);
		Assert.Equal(212350, immediateExecution.NpcId);
		Assert.Equal(2, immediateExecution.EffectiveMinCount);
		Assert.Equal(2, immediateExecution.EffectiveMaxCount);
		Assert.False(immediateExecution.RequiresRandomCount);
		Assert.False(immediateExecution.RequiresRandomDistance);
		Assert.False(immediateExecution.RequiresRandomAngle);
		Assert.Equal(0, immediateExecution.EffectiveMinDistance);
		Assert.Equal(0, immediateExecution.EffectiveMaxDistance);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnExecutionPreviewStatus.WouldSpawn, delayedExecution.Status);
		Assert.Equal(212351, delayedExecution.NpcId);
		Assert.True(delayedExecution.RequiresOwnerAliveRecheck);
		Assert.True(delayedExecution.RequiresRandomCount);
		Assert.True(delayedExecution.RequiresRandomDistance);
		Assert.True(delayedExecution.RequiresRandomAngle);
		Assert.Equal(1, delayedExecution.EffectiveMinCount);
		Assert.Equal(4, delayedExecution.EffectiveMaxCount);
		Assert.Equal(3, delayedExecution.EffectiveMinDistance);
		Assert.Equal(7, delayedExecution.EffectiveMaxDistance);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnExecutionPreviewStatus.OwnerNotReady, delayedOwnerDead.Status);
		Assert.False(delayedOwnerDead.RequiresSpawnEngine);
	}

	[Fact]
	public void PreviewMercenaryNpcSkillPostSpawnLocation_ProjectsJavaSpawnNpcOffsetsWithInjectedRandomInputs()
	{
		var service = new PlayerSummonSkillExecutionService();
		var origin = new PlayerSummonKnownObjectNpcSkillSpawnOrigin(
			WorldId: 210010000,
			InstanceId: 12,
			X: 100.5f,
			Y: 200.25f,
			Z: 35.75f,
			Heading: 90);
		var noOffsetSpawn = new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
			NpcId: 212352,
			MinDistance: 0,
			MaxDistance: 0,
			MinCount: 1,
			MaxCount: 0);
		var fixedDistanceSpawn = new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
			NpcId: 212353,
			MinDistance: 5,
			MaxDistance: 0,
			MinCount: 1,
			MaxCount: 0);
		var randomDistanceSpawn = new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
			NpcId: 212354,
			MinDistance: 3,
			MaxDistance: 7,
			MinCount: 1,
			MaxCount: 0);

		PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview Execution(PlayerSummonKnownObjectNpcSkillSpawnMetadata spawn)
		{
			var postSpawn = service.PreviewMercenaryNpcSkillPostSpawn(
				service.ProjectMercenaryNpcSkillTemplate(new PlayerSummonKnownObjectNpcSkillTemplateMetadata(SpawnTemplate: spawn)));
			return service.PreviewMercenaryNpcSkillPostSpawnExecution(postSpawn, origin);
		}

		var missingExecution = service.PreviewMercenaryNpcSkillPostSpawnLocation(null);
		var notSpawnable = service.PreviewMercenaryNpcSkillPostSpawnLocation(
			PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview.MissingPreview());
		var noOffset = service.PreviewMercenaryNpcSkillPostSpawnLocation(Execution(noOffsetSpawn));
		var missingAngle = service.PreviewMercenaryNpcSkillPostSpawnLocation(Execution(fixedDistanceSpawn));
		var fixedDistance = service.PreviewMercenaryNpcSkillPostSpawnLocation(
			Execution(fixedDistanceSpawn),
			randomAngleDegrees: 90f);
		var missingDistance = service.PreviewMercenaryNpcSkillPostSpawnLocation(
			Execution(randomDistanceSpawn),
			randomAngleDegrees: 90f);
		var randomDistance = service.PreviewMercenaryNpcSkillPostSpawnLocation(
			Execution(randomDistanceSpawn),
			randomAngleDegrees: 90f,
			randomDistance: 7);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.MissingExecution, missingExecution.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.NotSpawnable, notSpawnable.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.Projected, noOffset.Status);
		Assert.True(noOffset.HasProjectedLocation);
		Assert.Equal(270f, noOffset.HeadingAngleDegrees);
		Assert.Null(noOffset.RandomAngleDegrees);
		Assert.Equal(0, noOffset.Distance);
		Assert.Equal(0f, noOffset.OffsetX);
		Assert.Equal(0f, noOffset.OffsetY);
		Assert.Equal(origin.X, noOffset.SpawnX);
		Assert.Equal(origin.Y, noOffset.SpawnY);
		Assert.Equal(origin.Z, noOffset.SpawnZ);
		Assert.Equal(origin.Heading, noOffset.Heading);
		Assert.Equal(origin.WorldId, noOffset.WorldId);
		Assert.Equal(origin.InstanceId, noOffset.InstanceId);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.MissingRandomAngle, missingAngle.Status);
		Assert.True(missingAngle.RequiresJavaRandomAngle);
		Assert.Equal(270f, missingAngle.HeadingAngleDegrees);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.Projected, fixedDistance.Status);
		Assert.Equal(90f, fixedDistance.RandomAngleDegrees);
		Assert.Equal(5, fixedDistance.Distance);
		Assert.Equal(5f, fixedDistance.OffsetX.GetValueOrDefault(), precision: 5);
		Assert.Equal(0f, fixedDistance.OffsetY.GetValueOrDefault(), precision: 5);
		Assert.Equal(105.5f, fixedDistance.SpawnX.GetValueOrDefault(), precision: 5);
		Assert.Equal(200.25f, fixedDistance.SpawnY.GetValueOrDefault(), precision: 5);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.MissingRandomDistance, missingDistance.Status);
		Assert.True(missingDistance.RequiresJavaRandomDistance);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.Projected, randomDistance.Status);
		Assert.Equal(7, randomDistance.Distance);
		Assert.Equal(7f, randomDistance.OffsetX.GetValueOrDefault(), precision: 5);
		Assert.Equal(0f, randomDistance.OffsetY.GetValueOrDefault(), precision: 5);
		Assert.Equal(107.5f, randomDistance.SpawnX.GetValueOrDefault(), precision: 5);
		Assert.Equal(200.25f, randomDistance.SpawnY.GetValueOrDefault(), precision: 5);
	}

	[Fact]
	public void PreviewMercenaryNpcSkillSpawnTemplate_ProjectsJavaNewSingleTimeSpawnInputs()
	{
		var service = new PlayerSummonSkillExecutionService();
		var origin = new PlayerSummonKnownObjectNpcSkillSpawnOrigin(
			WorldId: 210010000,
			InstanceId: 12,
			X: 100.5f,
			Y: 200.25f,
			Z: 35.75f,
			Heading: 90,
			CreatorObjectId: 8001);
		var spawn = new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
			NpcId: 212355,
			MinDistance: 5,
			MaxDistance: 0,
			MinCount: 1,
			MaxCount: 0);
		var postSpawn = service.PreviewMercenaryNpcSkillPostSpawn(
			service.ProjectMercenaryNpcSkillTemplate(new PlayerSummonKnownObjectNpcSkillTemplateMetadata(SpawnTemplate: spawn)));
		var execution = service.PreviewMercenaryNpcSkillPostSpawnExecution(postSpawn, origin);
		var location = service.PreviewMercenaryNpcSkillPostSpawnLocation(execution, randomAngleDegrees: 90f);

		var missingLocation = service.PreviewMercenaryNpcSkillSpawnTemplate(null);
		var notReady = service.PreviewMercenaryNpcSkillSpawnTemplate(
			PlayerSummonKnownObjectNpcSkillSpawnLocationPreview.MissingExecution());
		var withoutEventTemplate = service.PreviewMercenaryNpcSkillSpawnTemplate(
			location,
			creatorHasSpawn: false,
			creatorSpawnHasEventTemplate: true);
		var withEventTemplate = service.PreviewMercenaryNpcSkillSpawnTemplate(
			location,
			creatorHasSpawn: true,
			creatorSpawnHasEventTemplate: true);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnTemplatePreviewStatus.MissingLocation, missingLocation.Status);
		Assert.False(missingLocation.WouldCreateSpawnTemplate);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnTemplatePreviewStatus.NotReady, notReady.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnTemplatePreviewStatus.Created, withoutEventTemplate.Status);
		Assert.True(withoutEventTemplate.WouldCreateSpawnTemplate);
		Assert.True(withoutEventTemplate.RequiresSpawnObjectCall);
		Assert.True(withoutEventTemplate.IsSingleTimeSpawn);
		Assert.True(withoutEventTemplate.HasCreator);
		Assert.Equal(210010000, withoutEventTemplate.WorldId);
		Assert.Equal(212355, withoutEventTemplate.NpcId);
		Assert.Equal(105.5f, withoutEventTemplate.X.GetValueOrDefault(), precision: 5);
		Assert.Equal(200.25f, withoutEventTemplate.Y.GetValueOrDefault(), precision: 5);
		Assert.Equal(35.75f, withoutEventTemplate.Z);
		Assert.Equal((byte)90, withoutEventTemplate.Heading);
		Assert.Equal(12, withoutEventTemplate.InstanceId);
		Assert.Equal(0, withoutEventTemplate.RespawnTimeSeconds);
		Assert.Equal(8001, withoutEventTemplate.CreatorObjectId);
		Assert.False(withoutEventTemplate.CarriesCreatorEventTemplate);
		Assert.Equal(0, withoutEventTemplate.RandomWalkRange);
		Assert.Null(withoutEventTemplate.WalkerId);
		Assert.Equal(0, withoutEventTemplate.StaticId);
		Assert.Null(withoutEventTemplate.AiName);
		Assert.True(withEventTemplate.CarriesCreatorEventTemplate);
	}

	[Fact]
	public void PreviewMercenaryNpcSkillSpawnObjectDispatch_ProjectsJavaSpawnObjectBranches()
	{
		var service = new PlayerSummonSkillExecutionService();

		PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview Template(int npcId)
		{
			var origin = new PlayerSummonKnownObjectNpcSkillSpawnOrigin(
				WorldId: 210010000,
				InstanceId: 12,
				X: 100.5f,
				Y: 200.25f,
				Z: 35.75f,
				Heading: 90,
				CreatorObjectId: 8001);
			var spawn = new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
				NpcId: npcId,
				MinDistance: 0,
				MaxDistance: 0,
				MinCount: 1,
				MaxCount: 0);
			var postSpawn = service.PreviewMercenaryNpcSkillPostSpawn(
				service.ProjectMercenaryNpcSkillTemplate(new PlayerSummonKnownObjectNpcSkillTemplateMetadata(SpawnTemplate: spawn)));
			var execution = service.PreviewMercenaryNpcSkillPostSpawnExecution(postSpawn, origin);
			var location = service.PreviewMercenaryNpcSkillPostSpawnLocation(execution);
			return service.PreviewMercenaryNpcSkillSpawnTemplate(location);
		}

		var missing = service.PreviewMercenaryNpcSkillSpawnObjectDispatch(null);
		var notReady = service.PreviewMercenaryNpcSkillSpawnObjectDispatch(
			PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview.MissingLocation());
		var gatherable = service.PreviewMercenaryNpcSkillSpawnObjectDispatch(
			Template(450001),
			PlayerSummonKnownObjectNpcSkillSpawnTemplateKind.Rift);
		var rift = service.PreviewMercenaryNpcSkillSpawnObjectDispatch(
			Template(212356),
			PlayerSummonKnownObjectNpcSkillSpawnTemplateKind.Rift);
		var siege = service.PreviewMercenaryNpcSkillSpawnObjectDispatch(
			Template(212357),
			PlayerSummonKnownObjectNpcSkillSpawnTemplateKind.Siege);
		var vortex = service.PreviewMercenaryNpcSkillSpawnObjectDispatch(
			Template(212358),
			PlayerSummonKnownObjectNpcSkillSpawnTemplateKind.Vortex);
		var npc = service.PreviewMercenaryNpcSkillSpawnObjectDispatch(Template(212359));

		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreviewStatus.MissingTemplate, missing.Status);
		Assert.False(missing.WouldCallVisibleObjectSpawner);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreviewStatus.NotReady, notReady.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.Gatherable, gatherable.Branch);
		Assert.True(gatherable.RequiresGatherableSpawner);
		Assert.False(gatherable.RequiresNpcTemplateLookup);
		Assert.False(gatherable.RequiresRiftEnabledCheck);
		Assert.Equal(450001, gatherable.NpcId);
		Assert.Equal(12, gatherable.InstanceId);
		Assert.True(gatherable.RequiresBringIntoWorld);
		Assert.True(gatherable.RequiresTemporarySpawnRegistrationCheck);
		Assert.True(gatherable.RequiresInstanceOnSpawnCheck);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.RiftNpc, rift.Branch);
		Assert.True(rift.RequiresNpcTemplateLookup);
		Assert.True(rift.RequiresRiftEnabledCheck);
		Assert.True(rift.MayReturnNull);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.SiegeNpc, siege.Branch);
		Assert.True(siege.RequiresSiegeEnabledCheck);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.InvasionNpc, vortex.Branch);
		Assert.True(vortex.RequiresVortexEnabledCheck);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.Npc, npc.Branch);
		Assert.True(npc.RequiresNpcTemplateLookup);
		Assert.False(npc.RequiresRiftEnabledCheck);
		Assert.False(npc.RequiresSiegeEnabledCheck);
		Assert.False(npc.RequiresVortexEnabledCheck);
	}

	[Fact]
	public void PreviewMercenaryNpcSkillOrdinaryNpcCreation_ProjectsJavaVisibleObjectSpawnerSpawnNpc()
	{
		var service = new PlayerSummonSkillExecutionService();

		PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview Dispatch(int npcId)
		{
			var origin = new PlayerSummonKnownObjectNpcSkillSpawnOrigin(
				WorldId: 210010000,
				InstanceId: 12,
				X: 100.5f,
				Y: 200.25f,
				Z: 35.75f,
				Heading: 90,
				CreatorObjectId: 8001);
			var spawn = new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
				NpcId: npcId,
				MinDistance: 0,
				MaxDistance: 0,
				MinCount: 1,
				MaxCount: 0);
			var postSpawn = service.PreviewMercenaryNpcSkillPostSpawn(
				service.ProjectMercenaryNpcSkillTemplate(new PlayerSummonKnownObjectNpcSkillTemplateMetadata(SpawnTemplate: spawn)));
			var execution = service.PreviewMercenaryNpcSkillPostSpawnExecution(postSpawn, origin);
			var location = service.PreviewMercenaryNpcSkillPostSpawnLocation(execution);
			var template = service.PreviewMercenaryNpcSkillSpawnTemplate(location);
			return service.PreviewMercenaryNpcSkillSpawnObjectDispatch(template);
		}

		var ordinaryDispatch = Dispatch(212360);
		var gatherableDispatch = service.PreviewMercenaryNpcSkillSpawnObjectDispatch(
			ordinaryDispatch.SpawnTemplatePreview! with { NpcId = 450001 });

		var missingDispatch = service.PreviewMercenaryNpcSkillOrdinaryNpcCreation(null, npcTemplateExists: true);
		var notOrdinary = service.PreviewMercenaryNpcSkillOrdinaryNpcCreation(gatherableDispatch, npcTemplateExists: true);
		var missingTemplate = service.PreviewMercenaryNpcSkillOrdinaryNpcCreation(ordinaryDispatch, npcTemplateExists: false);
		var ordinary = service.PreviewMercenaryNpcSkillOrdinaryNpcCreation(ordinaryDispatch, npcTemplateExists: true);
		var flag = service.PreviewMercenaryNpcSkillOrdinaryNpcCreation(
			ordinaryDispatch,
			npcTemplateExists: true,
			npcTemplateIsFlag: true);
		var clustered = service.PreviewMercenaryNpcSkillOrdinaryNpcCreation(
			ordinaryDispatch,
			npcTemplateExists: true,
			walkerFormatorBroughtIntoWorld: true);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillNpcCreationPreviewStatus.MissingDispatch, missingDispatch.Status);
		Assert.False(missingDispatch.WouldCreateNpc);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillNpcCreationPreviewStatus.NotOrdinaryNpc, notOrdinary.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillNpcCreationPreviewStatus.MissingNpcTemplate, missingTemplate.Status);
		Assert.True(missingTemplate.MayReturnNull);
		Assert.Equal(212360, missingTemplate.NpcId);
		Assert.Equal(12, missingTemplate.InstanceId);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillNpcCreationPreviewStatus.Created, ordinary.Status);
		Assert.True(ordinary.WouldCreateNpc);
		Assert.True(ordinary.RequiresNpcController);
		Assert.True(ordinary.RequiresNpcObject);
		Assert.True(ordinary.RequiresCreatorIdCopy);
		Assert.True(ordinary.RequiresKnownList);
		Assert.True(ordinary.RequiresEffectController);
		Assert.True(ordinary.RequiresWalkerFormator);
		Assert.True(ordinary.RequiresBringIntoWorld);
		Assert.True(ordinary.RequiresControllerDeleteOnBringIntoWorldFailure);
		Assert.True(ordinary.HasCreator);
		Assert.Equal(8001, ordinary.CreatorObjectId);
		Assert.Equal(212360, ordinary.NpcId);
		Assert.Equal(12, ordinary.InstanceId);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillNpcKnownListKind.NpcKnownList, ordinary.KnownListKind);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillNpcKnownListKind.FlagKnownList, flag.KnownListKind);
		Assert.True(clustered.WalkerFormatorBroughtIntoWorld);
		Assert.False(clustered.RequiresBringIntoWorld);
		Assert.False(clustered.RequiresControllerDeleteOnBringIntoWorldFailure);
	}

	[Fact]
	public void PreviewMercenaryNpcSkillBringIntoWorld_ProjectsJavaWorldInsertionSteps()
	{
		var service = new PlayerSummonSkillExecutionService();

		PlayerSummonKnownObjectNpcSkillNpcCreationPreview Creation(bool walkerBroughtIntoWorld = false)
		{
			var origin = new PlayerSummonKnownObjectNpcSkillSpawnOrigin(
				WorldId: 210010000,
				InstanceId: 12,
				X: 100.5f,
				Y: 200.25f,
				Z: 35.75f,
				Heading: 90,
				CreatorObjectId: 8001);
			var spawn = new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
				NpcId: 212361,
				MinDistance: 0,
				MaxDistance: 0,
				MinCount: 1,
				MaxCount: 0);
			var postSpawn = service.PreviewMercenaryNpcSkillPostSpawn(
				service.ProjectMercenaryNpcSkillTemplate(new PlayerSummonKnownObjectNpcSkillTemplateMetadata(SpawnTemplate: spawn)));
			var execution = service.PreviewMercenaryNpcSkillPostSpawnExecution(postSpawn, origin);
			var location = service.PreviewMercenaryNpcSkillPostSpawnLocation(execution);
			var template = service.PreviewMercenaryNpcSkillSpawnTemplate(location);
			var dispatch = service.PreviewMercenaryNpcSkillSpawnObjectDispatch(template);
			return service.PreviewMercenaryNpcSkillOrdinaryNpcCreation(
				dispatch,
				npcTemplateExists: true,
				walkerFormatorBroughtIntoWorld: walkerBroughtIntoWorld);
		}

		var creation = Creation();
		var missing = service.PreviewMercenaryNpcSkillBringIntoWorld(null);
		var notReady = service.PreviewMercenaryNpcSkillBringIntoWorld(Creation(walkerBroughtIntoWorld: true));
		var invalidMap = service.PreviewMercenaryNpcSkillBringIntoWorld(creation, mapExists: false);
		var invalidInstance = service.PreviewMercenaryNpcSkillBringIntoWorld(creation, instanceExists: false);
		var invalidRegion = service.PreviewMercenaryNpcSkillBringIntoWorld(creation, regionExists: false);
		var alreadySpawned = service.PreviewMercenaryNpcSkillBringIntoWorld(creation, objectAlreadySpawned: true);
		var insert = service.PreviewMercenaryNpcSkillBringIntoWorld(creation);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.MissingCreation, missing.Status);
		Assert.False(missing.WouldStoreObject);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.NotReady, notReady.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.InvalidMap, invalidMap.Status);
		Assert.True(invalidMap.RequiresMapLookup);
		Assert.False(invalidMap.RequiresInstanceLookup);
		Assert.True(invalidMap.MayThrow);
		Assert.Equal(210010000, invalidMap.WorldId);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.InvalidInstance, invalidInstance.Status);
		Assert.True(invalidInstance.RequiresInstanceLookup);
		Assert.False(invalidInstance.RequiresRegionLookup);
		Assert.True(invalidInstance.MayThrow);
		Assert.Equal(12, invalidInstance.InstanceId);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.InvalidRegion, invalidRegion.Status);
		Assert.True(invalidRegion.RequiresRegionLookup);
		Assert.True(invalidRegion.MayThrow);
		Assert.Equal(100.5f, invalidRegion.X);
		Assert.Equal(200.25f, invalidRegion.Y);
		Assert.Equal(35.75f, invalidRegion.Z);
		Assert.Equal((byte)90, invalidRegion.Heading);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.AlreadySpawned, alreadySpawned.Status);
		Assert.True(alreadySpawned.MayThrow);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.WouldInsert, insert.Status);
		Assert.True(insert.WouldStoreObject);
		Assert.True(insert.WouldSetPosition);
		Assert.True(insert.WouldSpawn);
		Assert.True(insert.RequiresDuplicateObjectCheck);
		Assert.True(insert.RequiresMapLookup);
		Assert.True(insert.RequiresInstanceLookup);
		Assert.True(insert.RequiresRegionLookup);
		Assert.True(insert.RequiresControllerBeforeAfterSpawn);
		Assert.True(insert.RequiresMapRegionAdd);
		Assert.True(insert.RequiresKnownListUpdate);
		Assert.False(insert.MayThrow);
		Assert.Equal(210010000, insert.WorldId);
		Assert.Equal(12, insert.InstanceId);
		Assert.Equal(100.5f, insert.X);
		Assert.Equal(200.25f, insert.Y);
		Assert.Equal(35.75f, insert.Z);
		Assert.Equal((byte)90, insert.Heading);
	}

	[Fact]
	public void PreviewMercenaryNpcSkillPostSpawnCallbacks_ProjectsJavaSpawnObjectPostProcessing()
	{
		var service = new PlayerSummonSkillExecutionService();

		PlayerSummonKnownObjectNpcSkillWorldInsertionPreview WorldInsertion(bool walkerBroughtIntoWorld = false)
		{
			var origin = new PlayerSummonKnownObjectNpcSkillSpawnOrigin(
				WorldId: 210010000,
				InstanceId: 12,
				X: 100.5f,
				Y: 200.25f,
				Z: 35.75f,
				Heading: 90,
				CreatorObjectId: 8001);
			var spawn = new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
				NpcId: 212362,
				MinDistance: 0,
				MaxDistance: 0,
				MinCount: 1,
				MaxCount: 0);
			var postSpawn = service.PreviewMercenaryNpcSkillPostSpawn(
				service.ProjectMercenaryNpcSkillTemplate(new PlayerSummonKnownObjectNpcSkillTemplateMetadata(SpawnTemplate: spawn)));
			var execution = service.PreviewMercenaryNpcSkillPostSpawnExecution(postSpawn, origin);
			var location = service.PreviewMercenaryNpcSkillPostSpawnLocation(execution);
			var template = service.PreviewMercenaryNpcSkillSpawnTemplate(location);
			var dispatch = service.PreviewMercenaryNpcSkillSpawnObjectDispatch(template);
			var creation = service.PreviewMercenaryNpcSkillOrdinaryNpcCreation(
				dispatch,
				npcTemplateExists: true,
				walkerFormatorBroughtIntoWorld: walkerBroughtIntoWorld);
			return service.PreviewMercenaryNpcSkillBringIntoWorld(creation);
		}

		var worldInsertion = WorldInsertion();
		var missing = service.PreviewMercenaryNpcSkillPostSpawnCallbacks(null, spawnedObjectReturned: true);
		var notReady = service.PreviewMercenaryNpcSkillPostSpawnCallbacks(
			WorldInsertion(walkerBroughtIntoWorld: true),
			spawnedObjectReturned: true);
		var noObject = service.PreviewMercenaryNpcSkillPostSpawnCallbacks(worldInsertion, spawnedObjectReturned: false);
		var ordinary = service.PreviewMercenaryNpcSkillPostSpawnCallbacks(worldInsertion, spawnedObjectReturned: true);
		var temporary = service.PreviewMercenaryNpcSkillPostSpawnCallbacks(
			worldInsertion,
			spawnedObjectReturned: true,
			spawnedObjectHasSpawn: true,
			spawnIsTemporary: true,
			spawnedObjectIsSpawned: true);
		var delayedWalker = service.PreviewMercenaryNpcSkillPostSpawnCallbacks(
			worldInsertion,
			spawnedObjectReturned: true,
			spawnedObjectHasSpawn: true,
			spawnIsTemporary: true,
			spawnedObjectIsSpawned: false);
		var noSpawnTemplate = service.PreviewMercenaryNpcSkillPostSpawnCallbacks(
			worldInsertion,
			spawnedObjectReturned: true,
			spawnedObjectHasSpawn: false,
			spawnIsTemporary: true,
			spawnedObjectIsSpawned: true);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreviewStatus.MissingWorldInsertion, missing.Status);
		Assert.False(missing.HasSpawnedObject);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreviewStatus.NotReady, notReady.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreviewStatus.NoSpawnedObject, noObject.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreviewStatus.Callbacks, ordinary.Status);
		Assert.True(ordinary.HasSpawnedObject);
		Assert.True(ordinary.RequiresTemporarySpawnCheck);
		Assert.True(ordinary.RequiresInstanceOnSpawnCheck);
		Assert.False(ordinary.ShouldRegisterTemporarySpawn);
		Assert.True(ordinary.ShouldInvokeInstanceOnSpawn);
		Assert.True(ordinary.RequiresWorldMapInstance);
		Assert.True(ordinary.RequiresInstanceHandler);

		Assert.True(temporary.ShouldRegisterTemporarySpawn);
		Assert.True(temporary.ShouldInvokeInstanceOnSpawn);
		Assert.True(temporary.SpawnedObjectHasSpawn);
		Assert.True(temporary.SpawnIsTemporary);
		Assert.True(temporary.SpawnedObjectIsSpawned);
		Assert.True(delayedWalker.ShouldRegisterTemporarySpawn);
		Assert.False(delayedWalker.ShouldInvokeInstanceOnSpawn);
		Assert.False(delayedWalker.RequiresWorldMapInstance);
		Assert.False(delayedWalker.RequiresInstanceHandler);
		Assert.False(noSpawnTemplate.ShouldRegisterTemporarySpawn);
		Assert.True(noSpawnTemplate.ShouldInvokeInstanceOnSpawn);
	}

	[Fact]
	public void ProjectMercenaryNpcSkillCandidate_AdaptsStaticTemplateEntryIntoSelectableCandidate()
	{
		var service = new PlayerSummonSkillExecutionService();
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8008,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary);
		var condition = new PlayerSummonKnownObjectNpcSkillConditionMetadata(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsInRange,
			RangeMeters: 12);
		var target = service.ProjectMercenaryNpcSkillConditionTarget(
			PlayerSummonKnownObjectNpcSkillConditionTargetKind.Npc,
			condition,
			distanceMeters: 10);
		var targetRangeReadiness = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
			hasCreatureTarget: true,
			isInRange: true);
		var template = new PlayerSummonKnownObjectNpcSkillTemplateMetadata(
			Priority: 9,
			MinHpPercentage: 30,
			MaxHpPercentage: 80,
			MinTimeMilliseconds: 1000,
			MaxTimeMilliseconds: 5000,
			CooldownMilliseconds: 4000,
			ConditionTemplate: condition,
			Target: PlayerSummonKnownObjectNpcSkillTargetAttribute.Random);
		var blockedTemplate = template with { Priority = 20 };

		var blockedCandidate = service.ProjectMercenaryNpcSkillCandidate(
			new PlayerSummonKnownObjectNpcSkillCandidateMetadata(
				Position: 0,
				Template: blockedTemplate,
				LastTimeUsedMilliseconds: 3000,
				ConditionTarget: target),
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 2500,
			currentTimeMilliseconds: 6000);
		var candidate = service.ProjectMercenaryNpcSkillCandidate(
			new PlayerSummonKnownObjectNpcSkillCandidateMetadata(
				Position: 1,
				Template: template,
				LastTimeUsedMilliseconds: 1000,
				ConditionTarget: target,
				TargetRangeReadiness: targetRangeReadiness),
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 2500,
			currentTimeMilliseconds: 6000);
		var selected = service.SelectMercenaryNpcSkillCandidate([blockedCandidate, candidate]);

		Assert.Equal(1, candidate.Position);
		Assert.Equal(9, candidate.Projection.Priority);
		Assert.Equal(PlayerSummonKnownObjectSkillTargetMode.CreatureTarget, candidate.Projection.TargetMode);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready, candidate.EntryTimingReadiness.Status);
		Assert.True(candidate.EntryTimingReadiness.HpReady);
		Assert.True(candidate.EntryTimingReadiness.TimeReady);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, candidate.EntryConditionReadiness.Status);
		Assert.Same(targetRangeReadiness, candidate.TargetRangeReadiness);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.OnCooldown, blockedCandidate.EntryTimingReadiness.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready, selected.Status);
		Assert.Equal(1, selected.Candidate?.Position);
	}

	[Fact]
	public void ProjectMercenaryNpcSkillCandidateList_AdaptsStaticEntriesForSelectionPreview()
	{
		var service = new PlayerSummonSkillExecutionService();
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8009,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary,
			LastSkillTimeMilliseconds: 10_000,
			NextSkillDelayMilliseconds: 0);
		var ordinary = new PlayerSummonKnownObjectNpcSkillCandidateMetadata(
			Position: 0,
			Template: new PlayerSummonKnownObjectNpcSkillTemplateMetadata(Priority: 9));
		var chain = new PlayerSummonKnownObjectNpcSkillCandidateMetadata(
			Position: 1,
			Template: new PlayerSummonKnownObjectNpcSkillTemplateMetadata(Priority: 1, ChainId: 77));
		var postSpawn = new PlayerSummonKnownObjectNpcSkillCandidateMetadata(
			Position: 2,
			Template: new PlayerSummonKnownObjectNpcSkillTemplateMetadata(Priority: 4, IsPostSpawn: true));
		var queuedImmediate = new PlayerSummonKnownObjectNpcSkillCandidateMetadata(
			Position: 3,
			Template: new PlayerSummonKnownObjectNpcSkillTemplateMetadata(Priority: 2, NextSkillTimeMilliseconds: 0));
		var lastSkill = new PlayerSummonKnownObjectNpcSkillTemplateMetadata(
			NextChainId: 77,
			MaxChainTimeMilliseconds: 15_000);

		var list = service.ProjectMercenaryNpcSkillCandidateList(
			[ordinary, chain, postSpawn],
			hpPercentage: 100,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 11_000);
		var queuedPreview = service.PreviewMercenaryNextNpcSkillSelectionFromCandidateMetadata(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 20_000,
			currentTimeMilliseconds: 10_001,
			isInCastSubState: false,
			candidates: [ordinary, chain],
			hpPercentage: 100,
			queuedCandidate: queuedImmediate,
			lastSkill: lastSkill);
		var chainPreview = service.PreviewMercenaryNextNpcSkillSelectionFromCandidateMetadata(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 0,
			currentTimeMilliseconds: 11_000,
			isInCastSubState: false,
			candidates: [ordinary, chain],
			hpPercentage: 100,
			lastSkill: lastSkill);

		Assert.False(list.IsEmpty);
		Assert.Equal([9, 4, 1], list.Priorities);
		Assert.Equal(2, Assert.Single(list.PostSpawnCandidates).Position);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready, queuedPreview.Selection.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.ImmediateQueuedSkill, queuedPreview.Selection.Source);
		Assert.Equal(3, queuedPreview.Selection.Candidate?.Position);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready, chainPreview.Selection.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.ChainSkill, chainPreview.Selection.Source);
		Assert.Equal(1, chainPreview.Selection.Candidate?.Position);
	}

	[Fact]
	public void ProjectMercenaryNpcSkillCandidateList_AdaptsRepresentedNpcSkillTableEntries()
	{
		var service = new PlayerSummonSkillExecutionService();
		var table = new NpcSkillTable(
		[
			new NpcSkillListSummary(
				[833288, 833289],
				[
					new NpcSkillTemplateSummary(
						SkillId: 22107,
						SkillLevel: 2,
						Probability: 65,
						MinHp: 20,
						MaxHp: 90,
						MaxTime: 10_000,
						MinTime: 1_000,
						Conjunction: "OR",
						Cooldown: 4_000,
						IsPostSpawn: false,
						Priority: 7,
						NextSkillTime: 0,
						NextChainId: 44,
						ChainId: 0,
						MaxChainTime: 9_000,
						Target: "SECOND_MOST_HATED",
						Spawn: null,
						Condition: new NpcSkillConditionSummary("TARGET_IS_IN_RANGE", 35, 12, 212362, 750, false, 1200)),
					new NpcSkillTemplateSummary(
						SkillId: 22108,
						SkillLevel: 1,
						Probability: 100,
						MinHp: 0,
						MaxHp: 100,
						MaxTime: 0,
						MinTime: 0,
						Conjunction: "AND",
						Cooldown: 0,
						IsPostSpawn: true,
						Priority: 3,
						NextSkillTime: -1,
						NextChainId: 0,
						ChainId: 44,
						MaxChainTime: 15_000,
						Target: "RANDOM_EXCEPT_CURRENT_TARGET",
						Spawn: new NpcSkillSpawnSummary(212362, 500, 2, 8, 1, 4)),
				]),
		]);

		var metadata = service.ProjectMercenaryNpcSkillCandidateMetadata(table, npcId: 833289);
		var projection = service.ProjectMercenaryNpcSkillCandidateList(
			table,
			npcId: 833288,
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 5_000,
			currentTimeMilliseconds: 20_000);
		var missing = service.ProjectMercenaryNpcSkillCandidateList(
			table,
			npcId: 999999,
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 5_000,
			currentTimeMilliseconds: 20_000);

		Assert.Equal([0, 1], metadata.Select(candidate => candidate.Position));
		Assert.Equal(22107, metadata[0].Template.SkillId);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConjunction.Or, metadata[0].Template.ConjunctionType);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillTargetAttribute.SecondMostHated, metadata[0].Template.Target);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillCondition.TargetIsInRange, metadata[0].Template.ConditionTemplate?.Condition);
		Assert.Equal(35, metadata[0].Template.ConditionTemplate?.HpBelowPercentage);
		Assert.Equal(12, metadata[0].Template.ConditionTemplate?.RangeMeters);
		Assert.Equal(212362, metadata[0].Template.ConditionTemplate?.NpcId);
		Assert.Equal(750, metadata[0].Template.ConditionTemplate?.DelayMilliseconds);
		Assert.False(metadata[0].Template.ConditionTemplate?.CanDie);
		Assert.Equal(1200, metadata[0].Template.ConditionTemplate?.DespawnTimeMilliseconds);
		Assert.Null(metadata[0].Template.SpawnTemplate);
		Assert.True(metadata[1].Template.IsPostSpawn);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillTargetAttribute.RandomExceptCurrentTarget, metadata[1].Template.Target);
		Assert.Equal(new PlayerSummonKnownObjectNpcSkillSpawnMetadata(212362, 500, 2, 8, 1, 4), metadata[1].Template.SpawnTemplate);

		Assert.Equal(2, projection.Candidates.Count);
		Assert.Equal([7, 3], projection.Priorities);
		Assert.Equal(1, Assert.Single(projection.PostSpawnCandidates).Position);
		Assert.Equal(PlayerSummonKnownObjectSkillTargetMode.CreatureTarget, projection.Candidates[0].Projection.TargetMode);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready, projection.Candidates[0].EntryTimingReadiness.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready, projection.Candidates[1].EntryTimingReadiness.Status);
		Assert.True(missing.IsEmpty);
		Assert.Empty(missing.Priorities);
		Assert.Empty(missing.PostSpawnCandidates);
	}

	[Fact]
	public void ProjectMercenaryNpcSkillCandidateMetadata_PrunesMissingSkillTemplatesLikeJavaInitSkillList()
	{
		var service = new PlayerSummonSkillExecutionService();
		var table = new NpcSkillTable(
		[
			new NpcSkillListSummary(
				[833288],
				[
					new NpcSkillTemplateSummary(1001, 1, 100, 0, 100, 0, 0, "AND", 0, false, 5, -1, 0, 0, 15000, "MOST_HATED", null),
					new NpcSkillTemplateSummary(9999, 1, 100, 0, 100, 0, 0, "AND", 0, false, 10, -1, 0, 0, 15000, "MOST_HATED", null),
					new NpcSkillTemplateSummary(1002, 1, 100, 0, 100, 0, 0, "AND", 0, true, 3, -1, 0, 0, 15000, "MOST_HATED", new NpcSkillSpawnSummary(212362, 0, 0, 0, 1, 0)),
				]),
		]);
		var skillTemplates = new SkillTemplateTable(
		[
			new SkillTemplateSummary(1001, "Ready One", 0, 1, "", "", "MAGICAL", "", 0, 0),
			new SkillTemplateSummary(1002, "Ready Two", 0, 1, "", "", "MAGICAL", "", 0, 0),
		]);

		var metadataProjection = service.ProjectMercenaryNpcSkillCandidateMetadata(table, skillTemplates, npcId: 833288);
		var candidateProjection = service.ProjectMercenaryNpcSkillCandidateList(
			table,
			skillTemplates,
			npcId: 833288,
			hpPercentage: 100,
			elapsedFightTimeMilliseconds: 0,
			currentTimeMilliseconds: 1_000);
		var missingNpc = service.ProjectMercenaryNpcSkillCandidateMetadata(table, skillTemplates, npcId: 999999);

		Assert.Equal(3, metadataProjection.OriginalSkillCount);
		Assert.Equal([9999], metadataProjection.MissingSkillIds);
		Assert.True(metadataProjection.RequiresSkillTemplateLookup);
		Assert.True(metadataProjection.WouldPruneMissingSkills);
		Assert.True(metadataProjection.JavaWouldWarnMissingSkills);
		Assert.True(metadataProjection.JavaWouldMutateSourceTemplateList);
		Assert.Equal([0, 1], metadataProjection.Candidates.Select(candidate => candidate.Position));
		Assert.Equal([1001, 1002], metadataProjection.Candidates.Select(candidate => candidate.Template.SkillId));
		Assert.Equal([5, 3], candidateProjection.Priorities);
		Assert.Equal(1, Assert.Single(candidateProjection.PostSpawnCandidates).Position);
		Assert.True(missingNpc.IsEmpty);
		Assert.False(missingNpc.RequiresSkillTemplateLookup);
		Assert.False(missingNpc.WouldPruneMissingSkills);
	}

	[Fact]
	public void CaptureMercenaryNpcSkillPreview_StoresRepresentedSelectionAndActionState()
	{
		var service = new PlayerSummonSkillExecutionService();
		var player = new Player { ObjectId = 1 };
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8010,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary,
			LastSkillTimeMilliseconds: 10_000,
			NextSkillDelayMilliseconds: 0);
		player.SetSummonKnownObject(knownObject);
		var candidate = new PlayerSummonKnownObjectNpcSkillCandidateMetadata(
			Position: 0,
			Template: new PlayerSummonKnownObjectNpcSkillTemplateMetadata(Priority: 9));
		var listProjection = service.ProjectMercenaryNpcSkillCandidateList(
			[candidate],
			hpPercentage: 100,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 12_000);
		var selectionPreview = service.PreviewMercenaryNextNpcSkillSelectionFromCandidateMetadata(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 0,
			currentTimeMilliseconds: 12_000,
			isInCastSubState: false,
			candidates: [candidate],
			hpPercentage: 100);
		var actionPreview = service.PreviewMercenaryNpcSkillAction(
			isInCastSubState: true,
			shouldResumeFightAfterInterruptedCast: false,
			hasCreatureTarget: true,
			targetIsDead: false,
			hasLastSkill: true,
			ownerUsesMeleeAggroRange: false,
			targetInAggroRange: true,
			skillReadiness: null,
			targetSelection: service.SelectMercenaryNpcSkillActionTarget(
				skillFirstTargetIsSelf: false,
				PlayerSummonKnownObjectNpcSkillTargetAttribute.None),
			controllerUseSkillSucceeded: true);
		var postSpawnPreview = service.PreviewMercenaryNpcSkillPostSpawn(
			service.ProjectMercenaryNpcSkillTemplate(
				new PlayerSummonKnownObjectNpcSkillTemplateMetadata(
					SpawnTemplate: new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
						NpcId: 212349,
						DelayMilliseconds: 250))));

		var missing = service.CaptureMercenaryNpcSkillPreview(
			player,
			mercenaryObjectId: 9999,
			listProjection,
			selectionPreview,
			actionPreview,
			postSpawnPreview);
		var captured = service.CaptureMercenaryNpcSkillPreview(
			player,
			mercenaryObjectId: 8010,
			listProjection,
			selectionPreview,
			actionPreview,
			postSpawnPreview);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillPreviewCaptureStatus.MissingKnownObject, missing.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillPreviewCaptureStatus.Captured, captured.Status);
		Assert.True(player.TryGetSummonKnownObject(8010, out var storedKnownObject));
		Assert.Same(listProjection, storedKnownObject.LastNpcSkillListProjection);
		Assert.Same(selectionPreview, storedKnownObject.LastNpcSkillSelectionPreview);
		Assert.Same(actionPreview, storedKnownObject.LastNpcSkillActionPreview);
		Assert.Same(postSpawnPreview, storedKnownObject.LastNpcSkillPostSpawnPreview);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready, storedKnownObject.LastNpcSkillSelectionPreview?.Selection.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionPreviewStatus.WouldUseSkill, storedKnownObject.LastNpcSkillActionPreview?.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus.DelayedSpawn, storedKnownObject.LastNpcSkillPostSpawnPreview?.Status);
	}

	[Fact]
	public void ResolveMercenaryNpcSkillTargetMode_MapsJavaNpcSkillTargetAttributes()
	{
		var service = new PlayerSummonSkillExecutionService();

		Assert.Equal(
			PlayerSummonKnownObjectSkillTargetMode.None,
			service.ResolveMercenaryNpcSkillTargetMode(PlayerSummonKnownObjectNpcSkillTargetAttribute.None));
		Assert.Equal(
			PlayerSummonKnownObjectSkillTargetMode.MostHated,
			service.ResolveMercenaryNpcSkillTargetMode(PlayerSummonKnownObjectNpcSkillTargetAttribute.MostHated));
		Assert.Equal(
			PlayerSummonKnownObjectSkillTargetMode.Self,
			service.ResolveMercenaryNpcSkillTargetMode(PlayerSummonKnownObjectNpcSkillTargetAttribute.Me));

		foreach (var target in new[]
		{
			PlayerSummonKnownObjectNpcSkillTargetAttribute.Friend,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.SecondMostHated,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.ThirdMostHated,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.Random,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.RandomExceptCurrentTarget,
		})
		{
			Assert.Equal(
				PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
				service.ResolveMercenaryNpcSkillTargetMode(target));
		}
	}

	[Fact]
	public void EvaluateMercenaryNpcSkillConditionReadiness_ProjectsNpcIsAliveWorldLookup()
	{
		var service = new PlayerSummonSkillExecutionService();
		var condition = new PlayerSummonKnownObjectNpcSkillConditionMetadata(
			PlayerSummonKnownObjectNpcSkillCondition.NpcIsAlive,
			NpcId: 212362);

		var missingWorldState = service.EvaluateMercenaryNpcSkillConditionReadiness(
			condition,
			target: null);
		var alive = service.EvaluateMercenaryNpcSkillConditionReadiness(
			condition,
			target: null,
			npcIsAliveInWorld: true);
		var absent = service.EvaluateMercenaryNpcSkillConditionReadiness(
			condition,
			target: null,
			npcIsAliveInWorld: false);
		var ownerDead = service.EvaluateMercenaryNpcSkillConditionReadiness(
			condition,
			target: null,
			ownerIsDead: true,
			npcIsAliveInWorld: true);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Unsupported, missingWorldState.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, alive.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.NotReady, absent.Status);
		Assert.Equal(212362, absent.Target?.NpcId);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionTargetKind.Npc, absent.Target?.Kind);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.OwnerNotReady, ownerDead.Status);
	}

	[Fact]
	public void SelectMercenaryNpcSkillActionTarget_ProjectsJavaSkillActionTargetMutation()
	{
		var service = new PlayerSummonSkillExecutionService();

		var firstTargetSelf = service.SelectMercenaryNpcSkillActionTarget(
			skillFirstTargetIsSelf: true,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.Random,
			hasRandomTarget: true);
		var npcTargetSelf = service.SelectMercenaryNpcSkillActionTarget(
			skillFirstTargetIsSelf: false,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.Me);
		var friendSelected = service.SelectMercenaryNpcSkillActionTarget(
			skillFirstTargetIsSelf: false,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.Friend,
			hasFriendTarget: true);
		var friendMissing = service.SelectMercenaryNpcSkillActionTarget(
			skillFirstTargetIsSelf: false,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.Friend,
			hasFriendTarget: false);
		var mostHated = service.SelectMercenaryNpcSkillActionTarget(
			skillFirstTargetIsSelf: false,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.MostHated,
			hasMostHatedTarget: true);
		var secondMostHated = service.SelectMercenaryNpcSkillActionTarget(
			skillFirstTargetIsSelf: false,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.SecondMostHated,
			hasSecondMostHatedTarget: true);
		var thirdMostHated = service.SelectMercenaryNpcSkillActionTarget(
			skillFirstTargetIsSelf: false,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.ThirdMostHated,
			hasThirdMostHatedTarget: true);
		var random = service.SelectMercenaryNpcSkillActionTarget(
			skillFirstTargetIsSelf: false,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.Random,
			hasRandomTarget: true);
		var randomExceptCurrent = service.SelectMercenaryNpcSkillActionTarget(
			skillFirstTargetIsSelf: false,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.RandomExceptCurrentTarget,
			hasRandomExceptCurrentTarget: true);
		var none = service.SelectMercenaryNpcSkillActionTarget(
			skillFirstTargetIsSelf: false,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.None);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionTargetSelectionStatus.Selected, firstTargetSelf.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionTargetSource.Owner, firstTargetSelf.Source);
		Assert.True(firstTargetSelf.ShouldSetOwnerTarget);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionTargetSource.Owner, npcTargetSelf.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionTargetSource.Friend, friendSelected.Source);
		Assert.True(friendSelected.ShouldSetOwnerTarget);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionTargetSelectionStatus.MissingTarget, friendMissing.Status);
		Assert.False(friendMissing.ShouldSetOwnerTarget);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionTargetSource.MostHated, mostHated.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionTargetSource.SecondMostHated, secondMostHated.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionTargetSource.ThirdMostHated, thirdMostHated.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionTargetSource.Random, random.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionTargetSource.RandomExceptCurrentTarget, randomExceptCurrent.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionTargetSelectionStatus.NotRequired, none.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionTargetSource.None, none.Source);
		Assert.False(none.ShouldSetOwnerTarget);
	}

	[Fact]
	public void PreviewMercenaryNpcSkillAction_ComposesJavaSkillActionPreUseOutcomes()
	{
		var service = new PlayerSummonSkillExecutionService();
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8006,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary);
		var readySkill = service.EvaluateMercenarySkillReadiness(knownObject, CreateSkillTemplate("MAGICAL"));
		var blockedSkill = service.EvaluateMercenarySkillReadiness(
			knownObject with { AbnormalState = PlayerAbnormalState.Silence },
			CreateSkillTemplate("MAGICAL"));
		var targetSelection = service.SelectMercenaryNpcSkillActionTarget(
			skillFirstTargetIsSelf: false,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.MostHated,
			hasMostHatedTarget: true);

		PlayerSummonKnownObjectNpcSkillActionPreview Preview(
			bool isInCastSubState = true,
			bool shouldResumeFightAfterInterruptedCast = false,
			bool hasCreatureTarget = true,
			bool targetIsDead = false,
			bool hasLastSkill = true,
			bool ownerUsesMeleeAggroRange = false,
			bool targetInAggroRange = true,
			PlayerSummonKnownObjectSkillReadiness? skillReadiness = null,
			PlayerSummonKnownObjectNpcSkillActionTargetSelection? selection = null,
			bool controllerUseSkillSucceeded = true)
		{
			return service.PreviewMercenaryNpcSkillAction(
				isInCastSubState,
				shouldResumeFightAfterInterruptedCast,
				hasCreatureTarget,
				targetIsDead,
				hasLastSkill,
				ownerUsesMeleeAggroRange,
				targetInAggroRange,
				skillReadiness ?? readySkill,
				selection ?? targetSelection,
				controllerUseSkillSucceeded);
		}

		var notCasting = Preview(isInCastSubState: false);
		var resumeFight = Preview(isInCastSubState: false, shouldResumeFightAfterInterruptedCast: true);
		var missingTarget = Preview(hasCreatureTarget: false);
		var deadTarget = Preview(targetIsDead: true);
		var missingSkill = Preview(hasLastSkill: false);
		var targetTooFar = Preview(ownerUsesMeleeAggroRange: true, targetInAggroRange: false);
		var abnormalBlocked = Preview(skillReadiness: blockedSkill);
		var wouldSetTarget = Preview();
		var wouldUseWithoutTargetMutation = Preview(selection: service.SelectMercenaryNpcSkillActionTarget(
			skillFirstTargetIsSelf: false,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.None));
		var useFailed = Preview(controllerUseSkillSucceeded: false);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionPreviewStatus.NotInCastSubState, notCasting.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionPreviewStatus.ResumeFightAfterInterruptedCast, resumeFight.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionPreviewStatus.TargetGiveUp, missingTarget.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionPreviewStatus.TargetGiveUp, deadTarget.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionPreviewStatus.TargetGiveUp, missingSkill.Status);
		Assert.True(missingSkill.ShouldSetSubStateNone);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionPreviewStatus.TargetTooFar, targetTooFar.Status);
		Assert.True(targetTooFar.ShouldAbortCast);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionPreviewStatus.AfterUseSkillBlocked, abnormalBlocked.Status);
		Assert.Same(blockedSkill, abnormalBlocked.SkillReadiness);
		Assert.True(abnormalBlocked.ShouldSetSubStateNone);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionPreviewStatus.WouldSetTargetAndUseSkill, wouldSetTarget.Status);
		Assert.True(wouldSetTarget.ShouldSetOwnerTarget);
		Assert.True(wouldSetTarget.ShouldUseSkill);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionPreviewStatus.WouldUseSkill, wouldUseWithoutTargetMutation.Status);
		Assert.False(wouldUseWithoutTargetMutation.ShouldSetOwnerTarget);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillActionPreviewStatus.AfterUseSkillUseFailed, useFailed.Status);
		Assert.True(useFailed.ShouldUseSkill);
		Assert.True(useFailed.ShouldSetSubStateNone);
	}

	[Fact]
	public void PreviewMercenaryNextNpcSkillSelection_AdaptsNpcGameStatsTimingIntoSelection()
	{
		var service = new PlayerSummonSkillExecutionService();
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8007,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary,
			LastSkillTimeMilliseconds: 10_000,
			NextSkillDelayMilliseconds: 5_000);
		var readyTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			new PlayerSummonKnownObjectNpcSkillEntryTiming(),
			hpPercentage: 100,
			elapsedFightTimeMilliseconds: 0,
			currentTimeMilliseconds: 15_001);
		var readyCondition = service.EvaluateMercenaryNpcSkillConditionReadiness(
			new PlayerSummonKnownObjectNpcSkillConditionMetadata(),
			target: null);

		PlayerSummonKnownObjectNpcSkillCandidate Candidate(
			int position,
			int priority,
			int nextSkillTime = -1)
		{
			var projection = service.ProjectMercenaryNpcSkillTemplate(
				new PlayerSummonKnownObjectNpcSkillTemplateMetadata(
					Priority: priority,
					NextSkillTimeMilliseconds: nextSkillTime));

			return new PlayerSummonKnownObjectNpcSkillCandidate(position, projection, readyTiming, readyCondition);
		}

		var ordinaryCandidate = Candidate(1, priority: 5);
		var queuedImmediate = Candidate(2, priority: 1, nextSkillTime: 0);
		var waiting = service.PreviewMercenaryNextNpcSkillSelection(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 1_000,
			currentTimeMilliseconds: 11_000,
			isInCastSubState: false,
			queuedCandidate: null,
			lastSkill: null,
			candidates: [ordinaryCandidate]);
		var notReady = service.PreviewMercenaryNextNpcSkillSelection(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 1_000,
			currentTimeMilliseconds: 14_999,
			isInCastSubState: false,
			queuedCandidate: null,
			lastSkill: null,
			candidates: [ordinaryCandidate]);
		var selected = service.PreviewMercenaryNextNpcSkillSelection(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 1_000,
			currentTimeMilliseconds: 15_000,
			isInCastSubState: false,
			queuedCandidate: null,
			lastSkill: null,
			candidates: [ordinaryCandidate]);
		var immediateQueued = service.PreviewMercenaryNextNpcSkillSelection(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 20_000,
			currentTimeMilliseconds: 10_001,
			isInCastSubState: false,
			queuedCandidate: queuedImmediate,
			lastSkill: null,
			candidates: [ordinaryCandidate]);

		Assert.Equal(1_000, waiting.ElapsedFightTimeMilliseconds);
		Assert.False(waiting.InitialSkillDelayElapsed);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.WaitingForDelayGate, waiting.Selection.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.ChooseNextSkillGate, waiting.Selection.Source);
		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.NotReady, notReady.NextSkillReadiness.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.WaitingForDelayGate, notReady.Selection.Status);
		Assert.True(selected.InitialSkillDelayElapsed);
		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.Ready, selected.NextSkillReadiness.Status);
		Assert.Equal(5_000, selected.ElapsedSinceLastSkillMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready, selected.Selection.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.OrdinaryPriority, selected.Selection.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready, immediateQueued.Selection.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.ImmediateQueuedSkill, immediateQueued.Selection.Source);
		Assert.False(immediateQueued.InitialSkillDelayElapsed);
	}

	[Fact]
	public void EvaluateMercenaryNpcSkillConditionReadiness_ConsumesConditionMetadataRange()
	{
		var service = new PlayerSummonSkillExecutionService();
		var condition = new PlayerSummonKnownObjectNpcSkillConditionMetadata(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsInRange,
			RangeMeters: 12);

		var insideTarget = service.ProjectMercenaryNpcSkillConditionTarget(
			PlayerSummonKnownObjectNpcSkillConditionTargetKind.Npc,
			condition,
			distanceMeters: 11.9);
		var boundaryTarget = service.ProjectMercenaryNpcSkillConditionTarget(
			PlayerSummonKnownObjectNpcSkillConditionTargetKind.Npc,
			condition,
			distanceMeters: 12);
		var outsideTarget = service.ProjectMercenaryNpcSkillConditionTarget(
			PlayerSummonKnownObjectNpcSkillConditionTargetKind.Npc,
			condition,
			distanceMeters: 12.1);

		var inside = service.EvaluateMercenaryNpcSkillConditionReadiness(condition, insideTarget);
		var boundary = service.EvaluateMercenaryNpcSkillConditionReadiness(condition, boundaryTarget);
		var outside = service.EvaluateMercenaryNpcSkillConditionReadiness(condition, outsideTarget);

		Assert.True(insideTarget.IsInRange);
		Assert.True(boundaryTarget.IsInRange);
		Assert.False(outsideTarget.IsInRange);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, inside.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, boundary.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.NotReady, outside.Status);
		Assert.Equal(condition.Condition, inside.Condition);
		Assert.Equal(condition.Condition, outside.Condition);
	}

	[Fact]
	public void SelectMercenaryNpcSkillCandidate_OrdersByPriorityAndReadiness()
	{
		var service = new PlayerSummonSkillExecutionService();
		var readyTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			new PlayerSummonKnownObjectNpcSkillEntryTiming(),
			hpPercentage: 100,
			elapsedFightTimeMilliseconds: 0,
			currentTimeMilliseconds: 1000);
		var blockedTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			new PlayerSummonKnownObjectNpcSkillEntryTiming(),
			hpPercentage: 100,
			elapsedFightTimeMilliseconds: 0,
			currentTimeMilliseconds: 1000,
			chanceReady: false);
		var readyCondition = service.EvaluateMercenaryNpcSkillConditionReadiness(
			new PlayerSummonKnownObjectNpcSkillConditionMetadata(),
			target: null);
		var blockedCondition = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsBleeding,
			new PlayerSummonKnownObjectNpcSkillConditionTarget(PlayerSummonKnownObjectNpcSkillConditionTargetKind.Npc));

		PlayerSummonKnownObjectNpcSkillCandidate Candidate(
			int position,
			int priority,
			int chainId,
			PlayerSummonKnownObjectNpcSkillEntryReadiness timing,
			PlayerSummonKnownObjectNpcSkillConditionReadiness condition,
			PlayerSummonKnownObjectTargetRangeReadiness? targetRange = null)
		{
			var projection = service.ProjectMercenaryNpcSkillTemplate(
				new PlayerSummonKnownObjectNpcSkillTemplateMetadata(Priority: priority, ChainId: chainId));

			return new PlayerSummonKnownObjectNpcSkillCandidate(position, projection, timing, condition, targetRange);
		}

		var empty = service.SelectMercenaryNpcSkillCandidate([]);
		var selected = service.SelectMercenaryNpcSkillCandidate([
			Candidate(0, priority: 9, chainId: 10, readyTiming, readyCondition),
			Candidate(1, priority: 8, chainId: 0, blockedTiming, readyCondition),
			Candidate(2, priority: 7, chainId: 0, readyTiming, blockedCondition),
			Candidate(3, priority: 5, chainId: 0, readyTiming, readyCondition),
			Candidate(4, priority: 1, chainId: 0, readyTiming, readyCondition),
		]);

		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8003,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary);
		var targetOutOfRange = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
			hasCreatureTarget: true,
			isInRange: false);
		var blockedByRange = service.SelectMercenaryNpcSkillCandidate([
			Candidate(0, priority: 10, chainId: 0, readyTiming, readyCondition, targetOutOfRange),
			Candidate(1, priority: 1, chainId: 0, readyTiming, readyCondition),
		]);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Empty, empty.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready, selected.Status);
		Assert.Equal(3, selected.Candidate?.Position);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.TargetRangeNotReady, blockedByRange.Status);
		Assert.Equal(0, blockedByRange.Candidate?.Position);
	}

	[Fact]
	public void SelectMercenaryQueuedNpcSkillCandidate_ProjectsImmediateAndDelayedQueuedBranches()
	{
		var service = new PlayerSummonSkillExecutionService();
		var readyTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			new PlayerSummonKnownObjectNpcSkillEntryTiming(),
			hpPercentage: 100,
			elapsedFightTimeMilliseconds: 0,
			currentTimeMilliseconds: 1000);
		var blockedTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			new PlayerSummonKnownObjectNpcSkillEntryTiming(),
			hpPercentage: 100,
			elapsedFightTimeMilliseconds: 0,
			currentTimeMilliseconds: 1000,
			chanceReady: false);
		var readyCondition = service.EvaluateMercenaryNpcSkillConditionReadiness(
			new PlayerSummonKnownObjectNpcSkillConditionMetadata(),
			target: null);

		PlayerSummonKnownObjectNpcSkillCandidate Candidate(
			int nextSkillTime,
			PlayerSummonKnownObjectNpcSkillEntryReadiness timing,
			PlayerSummonKnownObjectTargetRangeReadiness? targetRange = null)
		{
			var projection = service.ProjectMercenaryNpcSkillTemplate(
				new PlayerSummonKnownObjectNpcSkillTemplateMetadata(NextSkillTimeMilliseconds: nextSkillTime));

			return new PlayerSummonKnownObjectNpcSkillCandidate(0, projection, timing, readyCondition, targetRange);
		}

		var immediate = service.SelectMercenaryQueuedNpcSkillCandidate(
			Candidate(nextSkillTime: 0, readyTiming),
			initialSkillDelayElapsed: false,
			canUseNextSkill: false);
		var waiting = service.SelectMercenaryQueuedNpcSkillCandidate(
			Candidate(nextSkillTime: 5000, readyTiming),
			initialSkillDelayElapsed: false,
			canUseNextSkill: true);
		var delayed = service.SelectMercenaryQueuedNpcSkillCandidate(
			Candidate(nextSkillTime: 5000, readyTiming),
			initialSkillDelayElapsed: true,
			canUseNextSkill: true);
		var delayedNotReady = service.SelectMercenaryQueuedNpcSkillCandidate(
			Candidate(nextSkillTime: 0, blockedTiming),
			initialSkillDelayElapsed: true,
			canUseNextSkill: true);

		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8004,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary);
		var targetOutOfRange = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
			hasCreatureTarget: true,
			isInRange: false);
		var immediateRangeBlocked = service.SelectMercenaryQueuedNpcSkillCandidate(
			Candidate(nextSkillTime: 0, readyTiming, targetOutOfRange),
			initialSkillDelayElapsed: false,
			canUseNextSkill: false);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready, immediate.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.ImmediateQueuedSkill, immediate.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.WaitingForDelayGate, waiting.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.DelayedQueuedSkill, waiting.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready, delayed.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.DelayedQueuedSkill, delayed.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.NoReadyCandidate, delayedNotReady.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.DelayedQueuedSkill, delayedNotReady.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.TargetRangeNotReady, immediateRangeBlocked.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.ImmediateQueuedSkill, immediateRangeBlocked.Source);
	}

	[Fact]
	public void SelectMercenaryChainNpcSkillCandidate_ProjectsChainWindowAndPrioritySelection()
	{
		var service = new PlayerSummonSkillExecutionService();
		var readyTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			new PlayerSummonKnownObjectNpcSkillEntryTiming(),
			hpPercentage: 100,
			elapsedFightTimeMilliseconds: 0,
			currentTimeMilliseconds: 1000);
		var blockedTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			new PlayerSummonKnownObjectNpcSkillEntryTiming(),
			hpPercentage: 100,
			elapsedFightTimeMilliseconds: 0,
			currentTimeMilliseconds: 1000,
			chanceReady: false);
		var readyCondition = service.EvaluateMercenaryNpcSkillConditionReadiness(
			new PlayerSummonKnownObjectNpcSkillConditionMetadata(),
			target: null);

		var lastSkill = service.ProjectMercenaryNpcSkillTemplate(
			new PlayerSummonKnownObjectNpcSkillTemplateMetadata(
				NextChainId: 77,
				MaxChainTimeMilliseconds: 15000));

		PlayerSummonKnownObjectNpcSkillCandidate Candidate(
			int position,
			int chainId,
			int priority,
			PlayerSummonKnownObjectNpcSkillEntryReadiness timing,
			PlayerSummonKnownObjectTargetRangeReadiness? targetRange = null)
		{
			var projection = service.ProjectMercenaryNpcSkillTemplate(
				new PlayerSummonKnownObjectNpcSkillTemplateMetadata(ChainId: chainId, Priority: priority));

			return new PlayerSummonKnownObjectNpcSkillCandidate(position, projection, timing, readyCondition, targetRange);
		}

		var selected = service.SelectMercenaryChainNpcSkillCandidate(
			lastSkill,
			[
				Candidate(0, chainId: 99, priority: 10, readyTiming),
				Candidate(1, chainId: 77, priority: 5, blockedTiming),
				Candidate(2, chainId: 77, priority: 4, readyTiming),
				Candidate(3, chainId: 77, priority: 0, readyTiming),
			],
			elapsedSinceLastSkillMilliseconds: 14000);
		var expired = service.SelectMercenaryChainNpcSkillCandidate(
			lastSkill,
			[Candidate(0, chainId: 77, priority: 5, readyTiming)],
			elapsedSinceLastSkillMilliseconds: 15000);

		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8005,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary);
		var targetOutOfRange = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
			hasCreatureTarget: true,
			isInRange: false);
		var rangeBlocked = service.SelectMercenaryChainNpcSkillCandidate(
			lastSkill,
			[Candidate(0, chainId: 77, priority: 5, readyTiming, targetOutOfRange)],
			elapsedSinceLastSkillMilliseconds: 1000);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready, selected.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.ChainSkill, selected.Source);
		Assert.Equal(2, selected.Candidate?.Position);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.NoReadyCandidate, expired.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.ChainSkill, expired.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.TargetRangeNotReady, rangeBlocked.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.ChainSkill, rangeBlocked.Source);
	}

	[Fact]
	public void SelectMercenaryNextNpcSkillCandidate_ComposesJavaChooseNextSkillBranchOrder()
	{
		var service = new PlayerSummonSkillExecutionService();
		var readyTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			new PlayerSummonKnownObjectNpcSkillEntryTiming(),
			hpPercentage: 100,
			elapsedFightTimeMilliseconds: 0,
			currentTimeMilliseconds: 1000);
		var blockedTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			new PlayerSummonKnownObjectNpcSkillEntryTiming(),
			hpPercentage: 100,
			elapsedFightTimeMilliseconds: 0,
			currentTimeMilliseconds: 1000,
			chanceReady: false);
		var readyCondition = service.EvaluateMercenaryNpcSkillConditionReadiness(
			new PlayerSummonKnownObjectNpcSkillConditionMetadata(),
			target: null);

		PlayerSummonKnownObjectNpcSkillCandidate Candidate(
			int position,
			int priority,
			int chainId,
			int nextSkillTime,
			PlayerSummonKnownObjectNpcSkillEntryReadiness timing)
		{
			var projection = service.ProjectMercenaryNpcSkillTemplate(
				new PlayerSummonKnownObjectNpcSkillTemplateMetadata(
					Priority: priority,
					ChainId: chainId,
					NextSkillTimeMilliseconds: nextSkillTime));

			return new PlayerSummonKnownObjectNpcSkillCandidate(position, projection, timing, readyCondition);
		}

		var lastSkill = service.ProjectMercenaryNpcSkillTemplate(
			new PlayerSummonKnownObjectNpcSkillTemplateMetadata(
				NextChainId: 77,
				MaxChainTimeMilliseconds: 15000));
		var queuedImmediate = Candidate(0, priority: 1, chainId: 0, nextSkillTime: 0, readyTiming);
		var queuedDelayed = Candidate(1, priority: 1, chainId: 0, nextSkillTime: 5000, readyTiming);
		var chainCandidate = Candidate(2, priority: 7, chainId: 77, nextSkillTime: -1, readyTiming);
		var ordinaryCandidate = Candidate(3, priority: 9, chainId: 0, nextSkillTime: -1, readyTiming);

		var castBlocked = service.SelectMercenaryNextNpcSkillCandidate(
			isInCastSubState: true,
			initialSkillDelayElapsed: true,
			canUseNextSkill: true,
			queuedCandidate: queuedImmediate,
			lastSkill: lastSkill,
			candidates: [chainCandidate, ordinaryCandidate],
			elapsedSinceLastSkillMilliseconds: 1000);
		var immediateQueued = service.SelectMercenaryNextNpcSkillCandidate(
			isInCastSubState: false,
			initialSkillDelayElapsed: false,
			canUseNextSkill: false,
			queuedCandidate: queuedImmediate,
			lastSkill: lastSkill,
			candidates: [chainCandidate, ordinaryCandidate],
			elapsedSinceLastSkillMilliseconds: 1000);
		var waitingForGate = service.SelectMercenaryNextNpcSkillCandidate(
			isInCastSubState: false,
			initialSkillDelayElapsed: false,
			canUseNextSkill: true,
			queuedCandidate: queuedDelayed,
			lastSkill: lastSkill,
			candidates: [chainCandidate, ordinaryCandidate],
			elapsedSinceLastSkillMilliseconds: 1000);
		var delayedQueued = service.SelectMercenaryNextNpcSkillCandidate(
			isInCastSubState: false,
			initialSkillDelayElapsed: true,
			canUseNextSkill: true,
			queuedCandidate: queuedDelayed,
			lastSkill: lastSkill,
			candidates: [chainCandidate, ordinaryCandidate],
			elapsedSinceLastSkillMilliseconds: 1000);
		var chainSelected = service.SelectMercenaryNextNpcSkillCandidate(
			isInCastSubState: false,
			initialSkillDelayElapsed: true,
			canUseNextSkill: true,
			queuedCandidate: Candidate(4, priority: 1, chainId: 0, nextSkillTime: 0, blockedTiming),
			lastSkill: lastSkill,
			candidates: [chainCandidate, ordinaryCandidate],
			elapsedSinceLastSkillMilliseconds: 1000);
		var ordinarySelected = service.SelectMercenaryNextNpcSkillCandidate(
			isInCastSubState: false,
			initialSkillDelayElapsed: true,
			canUseNextSkill: true,
			queuedCandidate: null,
			lastSkill: null,
			candidates: [ordinaryCandidate],
			elapsedSinceLastSkillMilliseconds: 0);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.InCastSubState, castBlocked.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.ChooseNextSkillGate, castBlocked.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready, immediateQueued.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.ImmediateQueuedSkill, immediateQueued.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.WaitingForDelayGate, waitingForGate.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.ChooseNextSkillGate, waitingForGate.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready, delayedQueued.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.DelayedQueuedSkill, delayedQueued.Source);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready, chainSelected.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.ChainSkill, chainSelected.Source);
		Assert.Equal(2, chainSelected.Candidate?.Position);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready, ordinarySelected.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillSelectionSource.OrdinaryPriority, ordinarySelected.Source);
		Assert.Equal(3, ordinarySelected.Candidate?.Position);
	}

	[Fact]
	public void EvaluateMercenaryNpcSkillConditionReadiness_ProjectsSimpleJavaConditionBranches()
	{
		var service = new PlayerSummonSkillExecutionService();
		var playerTarget = new PlayerSummonKnownObjectNpcSkillConditionTarget(
			PlayerSummonKnownObjectNpcSkillConditionTargetKind.Player,
			PlayerAbnormalState.Stun | PlayerAbnormalState.Poison,
			IsFlying: true,
			IsPhysicalClass: false,
			IsInRange: true);
		var npcTarget = new PlayerSummonKnownObjectNpcSkillConditionTarget(
			PlayerSummonKnownObjectNpcSkillConditionTargetKind.Npc,
			PlayerAbnormalState.Sleep,
			IsInRange: false);
		var gateTarget = new PlayerSummonKnownObjectNpcSkillConditionTarget(
			PlayerSummonKnownObjectNpcSkillConditionTargetKind.Gate);

		var ownerDead = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.None,
			playerTarget,
			ownerIsDead: true);
		var none = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.None,
			null);
		var missingTarget = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsStunned,
			null);
		var stunned = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsStunned,
			playerTarget);
		var sleepingNpc = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsSleeping,
			npcTarget);
		var bleeding = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsBleeding,
			playerTarget);
		var flying = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsFlying,
			playerTarget);
		var player = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsPlayer,
			playerTarget);
		var npc = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsNpc,
			npcTarget);
		var gate = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsGate,
			gateTarget);
		var magicalClass = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsMagicalClass,
			playerTarget);
		var physicalClass = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsPhysicalClass,
			playerTarget);
		var inRange = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsInRange,
			playerTarget);
		var outOfRange = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsInRange,
			npcTarget);
		var unsupported = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.HelpFriend,
			playerTarget);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.OwnerNotReady, ownerDead.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, none.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.MissingTarget, missingTarget.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, stunned.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, sleepingNpc.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.NotReady, bleeding.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, flying.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, player.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, npc.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, gate.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, magicalClass.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.NotReady, physicalClass.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, inRange.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.NotReady, outOfRange.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Unsupported, unsupported.Status);
	}

	[Fact]
	public void EvaluateMercenaryNpcSkillEntryReadiness_ProjectsJavaHpTimeCooldownAndConjunctions()
	{
		var service = new PlayerSummonSkillExecutionService();
		var defaultTiming = new PlayerSummonKnownObjectNpcSkillEntryTiming();
		var gatedTiming = new PlayerSummonKnownObjectNpcSkillEntryTiming(
			MinHpPercentage: 25,
			MaxHpPercentage: 75,
			MinTimeMilliseconds: 1_000,
			MaxTimeMilliseconds: 5_000);

		var defaultReady = service.EvaluateMercenaryNpcSkillEntryReadiness(
			defaultTiming,
			hpPercentage: 1,
			elapsedFightTimeMilliseconds: 0,
			currentTimeMilliseconds: 10_000);
		var onCooldown = service.EvaluateMercenaryNpcSkillEntryReadiness(
			defaultTiming with { CooldownMilliseconds = 5_000, LastTimeUsedMilliseconds = 8_000 },
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);
		var chanceNotReady = service.EvaluateMercenaryNpcSkillEntryReadiness(
			defaultTiming,
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000,
			chanceReady: false);
		var andReady = service.EvaluateMercenaryNpcSkillEntryReadiness(
			gatedTiming,
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);
		var andHpOutOfRange = service.EvaluateMercenaryNpcSkillEntryReadiness(
			gatedTiming,
			hpPercentage: 90,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);
		var minOnlyTimeReady = service.EvaluateMercenaryNpcSkillEntryReadiness(
			gatedTiming with { MaxTimeMilliseconds = 0 },
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 7_000,
			currentTimeMilliseconds: 10_000);
		var orReady = service.EvaluateMercenaryNpcSkillEntryReadiness(
			gatedTiming with { ConjunctionType = PlayerSummonKnownObjectNpcSkillConjunction.Or },
			hpPercentage: 90,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);
		var xorReady = service.EvaluateMercenaryNpcSkillEntryReadiness(
			gatedTiming with { ConjunctionType = PlayerSummonKnownObjectNpcSkillConjunction.Xor },
			hpPercentage: 90,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);
		var xorNotReady = service.EvaluateMercenaryNpcSkillEntryReadiness(
			gatedTiming with { ConjunctionType = PlayerSummonKnownObjectNpcSkillConjunction.Xor },
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready, defaultReady.Status);
		Assert.True(defaultReady.HpReady);
		Assert.True(defaultReady.TimeReady);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.OnCooldown, onCooldown.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.ChanceNotReady, chanceNotReady.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready, andReady.Status);
		Assert.True(andReady.HpReady);
		Assert.True(andReady.TimeReady);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.NotReady, andHpOutOfRange.Status);
		Assert.False(andHpOutOfRange.HpReady);
		Assert.True(andHpOutOfRange.TimeReady);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready, minOnlyTimeReady.Status);
		Assert.True(minOnlyTimeReady.TimeReady);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready, orReady.Status);
		Assert.False(orReady.HpReady);
		Assert.True(orReady.TimeReady);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready, xorReady.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.NotReady, xorNotReady.Status);
		Assert.True(xorNotReady.HpReady);
		Assert.True(xorNotReady.TimeReady);
	}

	[Fact]
	public void ApplyMercenaryTargetRangeDelay_ProjectsJavaTargetTooFarDelay()
	{
		var service = new PlayerSummonSkillExecutionService();
		var player = new Player
		{
			ObjectId = 1,
		};
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary);
		player.SetSummonKnownObject(knownObject);

		var notRequired = service.EvaluateMercenaryTargetRange(
			knownObject,
			requiresCreatureTargetCheck: false,
			hasCreatureTarget: false);
		var noneTarget = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.None,
			hasCreatureTarget: false);
		var mostHatedTarget = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.MostHated,
			hasCreatureTarget: false);
		var selfTarget = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.Self,
			hasCreatureTarget: false);
		var areaSkillReady = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
			hasCreatureTarget: true,
			isAreaTarget: true,
			isInRange: false);
		var missingCreatureTarget = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
			hasCreatureTarget: false);
		var deadTarget = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
			hasCreatureTarget: true,
			targetIsDead: true);
		var unseenTarget = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
			hasCreatureTarget: true,
			canSeeTarget: false);
		var outOfRange = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
			hasCreatureTarget: true,
			isInRange: false);

		var missingEvaluation = service.ApplyMercenaryTargetRangeDelay(player, mercenaryObjectId: 8002, null);
		var noDelay = service.ApplyMercenaryTargetRangeDelay(player, mercenaryObjectId: 8002, areaSkillReady);
		var missingKnownObject = service.ApplyMercenaryTargetRangeDelay(new Player { ObjectId = 1 }, mercenaryObjectId: 8002, outOfRange);
		var delayed = service.ApplyMercenaryTargetRangeDelay(player, mercenaryObjectId: 8002, outOfRange);

		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.NotRequired, notRequired.Status);
		Assert.False(notRequired.ShouldSetNextSkillDelay);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.NotRequired, noneTarget.Status);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.NotRequired, mostHatedTarget.Status);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.NotRequired, selfTarget.Status);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.Ready, areaSkillReady.Status);
		Assert.False(areaSkillReady.ShouldSetNextSkillDelay);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.MissingCreatureTarget, missingCreatureTarget.Status);
		Assert.Equal(5_000, missingCreatureTarget.NextSkillDelayMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.TargetDead, deadTarget.Status);
		Assert.Equal(5_000, deadTarget.NextSkillDelayMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.CannotSeeTarget, unseenTarget.Status);
		Assert.Equal(5_000, unseenTarget.NextSkillDelayMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.TargetOutOfRange, outOfRange.Status);
		Assert.Equal(5_000, outOfRange.NextSkillDelayMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeDelayStatus.MissingRangeEvaluation, missingEvaluation.Status);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeDelayStatus.NotRequired, noDelay.Status);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeDelayStatus.MissingKnownObject, missingKnownObject.Status);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeDelayStatus.Set, delayed.Status);
		Assert.Equal(5_000, delayed.StoredDelayMilliseconds);
		Assert.True(player.TryGetSummonKnownObject(8002, out var storedKnownObject));
		Assert.Equal(5_000, storedKnownObject.NextSkillDelayMilliseconds);
	}

	[Fact]
	public void EvaluateMercenarySkillReadiness_ProjectsJavaAbnormalAndTransformGates()
	{
		var service = new PlayerSummonSkillExecutionService();
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary);
		var magicalSkill = CreateSkillTemplate(skillType: "MAGICAL");
		var physicalSkill = CreateSkillTemplate(skillType: "PHYSICAL");

		var timingNotReady = service.EvaluateMercenarySkillReadiness(knownObject, magicalSkill, entryTimingReady: false);
		var conditionNotReady = service.EvaluateMercenarySkillReadiness(knownObject, magicalSkill, entryConditionReady: false);
		var missingTemplate = service.EvaluateMercenarySkillReadiness(knownObject, null);
		var silencedMagical = service.EvaluateMercenarySkillReadiness(
			knownObject with { AbnormalState = PlayerAbnormalState.Silence },
			magicalSkill);
		var boundPhysical = service.EvaluateMercenarySkillReadiness(
			knownObject with { AbnormalState = PlayerAbnormalState.Bind },
			physicalSkill);
		var stunnedMagical = service.EvaluateMercenarySkillReadiness(
			knownObject with { AbnormalState = PlayerAbnormalState.Stun },
			magicalSkill);
		var transformedBan = service.EvaluateMercenarySkillReadiness(
			knownObject with { IsTransformed = true, TransformBansSkillUse = true },
			physicalSkill);
		var ready = service.EvaluateMercenarySkillReadiness(knownObject, magicalSkill);

		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.EntryTimingNotReady, timingNotReady.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.EntryConditionNotReady, conditionNotReady.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.MissingSkillTemplate, missingTemplate.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.BlockedBySilence, silencedMagical.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.BlockedByBind, boundPhysical.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.BlockedByCantAttackState, stunnedMagical.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.BlockedByTransformSkillBan, transformedBan.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.Ready, ready.Status);
		Assert.Same(magicalSkill, ready.SkillTemplate);
	}

	[Fact]
	public void EvaluateMercenarySkillReadiness_ConsumesTypedNpcSkillEntryTiming()
	{
		var service = new PlayerSummonSkillExecutionService();
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary);
		var magicalSkill = CreateSkillTemplate(skillType: "MAGICAL");
		var timing = new PlayerSummonKnownObjectNpcSkillEntryTiming(
			MinHpPercentage: 25,
			MaxHpPercentage: 75,
			MinTimeMilliseconds: 1_000,
			MaxTimeMilliseconds: 5_000);
		var blockedTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			timing,
			hpPercentage: 90,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);
		var readyTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			timing,
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);

		var blocked = service.EvaluateMercenarySkillReadiness(knownObject, magicalSkill, blockedTiming);
		var ready = service.EvaluateMercenarySkillReadiness(knownObject, magicalSkill, readyTiming);
		var conditionBlocked = service.EvaluateMercenarySkillReadiness(
			knownObject,
			magicalSkill,
			readyTiming,
			entryConditionReady: false);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.NotReady, blockedTiming.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.EntryTimingNotReady, blocked.Status);
		Assert.Same(blockedTiming, blocked.EntryTimingReadiness);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.Ready, ready.Status);
		Assert.Same(readyTiming, ready.EntryTimingReadiness);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.EntryConditionNotReady, conditionBlocked.Status);
		Assert.Same(readyTiming, conditionBlocked.EntryTimingReadiness);
	}

	[Fact]
	public void EvaluateMercenarySkillReadiness_ConsumesTypedNpcSkillConditionReadiness()
	{
		var service = new PlayerSummonSkillExecutionService();
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary);
		var magicalSkill = CreateSkillTemplate(skillType: "MAGICAL");
		var readyTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			new PlayerSummonKnownObjectNpcSkillEntryTiming(),
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 0,
			currentTimeMilliseconds: 10_000);
		var stunnedTarget = new PlayerSummonKnownObjectNpcSkillConditionTarget(
			PlayerSummonKnownObjectNpcSkillConditionTargetKind.Player,
			PlayerAbnormalState.Stun);
		var readyCondition = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsStunned,
			stunnedTarget);
		var blockedCondition = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsBleeding,
			stunnedTarget);

		var blocked = service.EvaluateMercenarySkillReadiness(
			knownObject,
			magicalSkill,
			readyTiming,
			blockedCondition);
		var ready = service.EvaluateMercenarySkillReadiness(
			knownObject,
			magicalSkill,
			readyTiming,
			readyCondition);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.NotReady, blockedCondition.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.EntryConditionNotReady, blocked.Status);
		Assert.Same(readyTiming, blocked.EntryTimingReadiness);
		Assert.Same(blockedCondition, blocked.EntryConditionReadiness);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.Ready, ready.Status);
		Assert.Same(readyTiming, ready.EntryTimingReadiness);
		Assert.Same(readyCondition, ready.EntryConditionReadiness);
	}

	[Fact]
	public void PreviewMercenarySkillAttack_ProjectsSkillAttackManagerGates()
	{
		var service = new PlayerSummonSkillExecutionService();
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary,
			LastSkillTimeMilliseconds: 10_000,
			NextSkillDelayMilliseconds: 5_000);

		var casting = service.PreviewMercenarySkillAttack(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 1_000,
			currentTimeMilliseconds: 20_000,
			isCasting: true);
		var queuedInstant = service.PreviewMercenarySkillAttack(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 20_000,
			currentTimeMilliseconds: 10_001,
			isCasting: false,
			hasReadyQueuedInstantSkill: true);
		var initialDelay = service.PreviewMercenarySkillAttack(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 1_000,
			currentTimeMilliseconds: 11_000,
			isCasting: false);
		var nextSkillNotReady = service.PreviewMercenarySkillAttack(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 1_000,
			currentTimeMilliseconds: 14_999,
			isCasting: false);
		var wouldEvaluate = service.PreviewMercenarySkillAttack(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 1_000,
			currentTimeMilliseconds: 15_000,
			isCasting: false);
		var defaultDelayReady = service.PreviewMercenarySkillAttack(
			knownObject with { NextSkillDelayMilliseconds = null },
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 1_000,
			currentTimeMilliseconds: 11_001,
			isCasting: false);

		Assert.Equal(PlayerSummonKnownObjectSkillAttackPreviewStatus.BlockedCasting, casting.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillAttackPreviewStatus.WouldUseQueuedInstantSkill, queuedInstant.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillAttackPreviewStatus.InitialDelayNotElapsed, initialDelay.Status);
		Assert.Equal(1_000, initialDelay.ElapsedFightTimeMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectSkillAttackPreviewStatus.NextSkillNotReady, nextSkillNotReady.Status);
		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.NotReady, nextSkillNotReady.Readiness?.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillAttackPreviewStatus.WouldEvaluateSkills, wouldEvaluate.Status);
		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.Ready, wouldEvaluate.Readiness?.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillAttackPreviewStatus.WouldEvaluateSkills, defaultDelayReady.Status);
		Assert.Equal(0, defaultDelayReady.Readiness?.NextSkillDelayMilliseconds);
	}

	[Fact]
	public void SetMercenaryNextSkillDelay_StoresConcreteDelayAndRejectsRandomSentinel()
	{
		var service = new PlayerSummonSkillExecutionService();
		var player = new Player
		{
			ObjectId = 1,
		};
		player.SetSummonKnownObject(new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary));

		var missingKnownObject = service.SetMercenaryNextSkillDelay(player, mercenaryObjectId: 9003, nextSkillDelayMilliseconds: 5_000);
		var randomDelay = service.SetMercenaryNextSkillDelay(player, mercenaryObjectId: 8002, nextSkillDelayMilliseconds: -1);
		var zeroDelay = service.SetMercenaryNextSkillDelay(player, mercenaryObjectId: 8002, nextSkillDelayMilliseconds: 0);
		var concreteDelay = service.SetMercenaryNextSkillDelay(player, mercenaryObjectId: 8002, nextSkillDelayMilliseconds: 5_000);

		Assert.Equal(PlayerSummonKnownObjectNextSkillDelayStatus.MissingKnownObject, missingKnownObject.Status);
		Assert.Null(missingKnownObject.StoredDelayMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNextSkillDelayStatus.RandomDelayUnsupported, randomDelay.Status);
		Assert.Null(randomDelay.StoredDelayMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNextSkillDelayStatus.Set, zeroDelay.Status);
		Assert.Equal(0, zeroDelay.StoredDelayMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNextSkillDelayStatus.Set, concreteDelay.Status);
		Assert.Equal(5_000, concreteDelay.StoredDelayMilliseconds);
		Assert.True(player.TryGetSummonKnownObject(8002, out var knownObject));
		Assert.Equal(5_000, knownObject.NextSkillDelayMilliseconds);
	}

	[Fact]
	public void EvaluateMercenaryNextSkillReadiness_ProjectsJavaDelayCheck()
	{
		var service = new PlayerSummonSkillExecutionService();
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary,
			LastSkillTimeMilliseconds: 10_000);
		var noDelay = service.EvaluateMercenaryNextSkillReadiness(
			knownObject,
			nextSkillDelayMilliseconds: 0,
			currentTimeMilliseconds: 10_001);
		var notReady = service.EvaluateMercenaryNextSkillReadiness(
			knownObject,
			nextSkillDelayMilliseconds: 5_000,
			currentTimeMilliseconds: 14_999);
		var ready = service.EvaluateMercenaryNextSkillReadiness(
			knownObject,
			nextSkillDelayMilliseconds: 5_000,
			currentTimeMilliseconds: 15_000);
		var defaultLastSkillTime = service.EvaluateMercenaryNextSkillReadiness(
			knownObject with { LastSkillTimeMilliseconds = null },
			nextSkillDelayMilliseconds: 5_000,
			currentTimeMilliseconds: 4_999);
		var randomDelay = service.EvaluateMercenaryNextSkillReadiness(
			knownObject,
			nextSkillDelayMilliseconds: -1,
			currentTimeMilliseconds: 15_000);

		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.Ready, noDelay.Status);
		Assert.Null(noDelay.ReadyAtMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.NotReady, notReady.Status);
		Assert.Equal(15_000, notReady.ReadyAtMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.Ready, ready.Status);
		Assert.Equal(15_000, ready.ReadyAtMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.NotReady, defaultLastSkillTime.Status);
		Assert.Equal(5_000, defaultLastSkillTime.ReadyAtMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.RandomDelayUnsupported, randomDelay.Status);
	}

	[Fact]
	public void RenewMercenaryLastSkillTime_UpdatesRepresentedKnownObject()
	{
		var service = new PlayerSummonSkillExecutionService();
		var player = new Player
		{
			ObjectId = 1,
		};
		player.SetSummonKnownObject(new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary));
		var mercenaryPlan = new PlayerSummonSkillInvocationPlan(
			PlayerSummonSkillInvocationActorKind.Mercenary,
			ActorObjectId: 8002,
			ActorTemplateId: 833288,
			SkillId: 22107,
			SkillLevel: 1,
			Target: new PlayerSummonCastSpellTarget(8002, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: true),
			Hate: 0,
			ReleaseOnSuccess: false);
		var summonPlan = mercenaryPlan with
		{
			ActorKind = PlayerSummonSkillInvocationActorKind.Summon,
			ActorObjectId = 8001,
			Hate = 5,
			ReleaseOnSuccess = true,
		};
		var mercenaryExecution = PlayerSummonSkillInvocationExecutionResult.WouldUseSkill(
			mercenaryPlan,
			skillTemplateId: 22107,
			skillCooldownId: 0);
		var summonExecution = PlayerSummonSkillInvocationExecutionResult.WouldUseSkill(
			summonPlan,
			skillTemplateId: 22107,
			skillCooldownId: 0);

		var missingExecution = service.RenewMercenaryLastSkillTime(player, null, currentTimeMilliseconds: 12_000);
		var notRenewable = service.RenewMercenaryLastSkillTime(player, summonExecution, currentTimeMilliseconds: 12_000);
		var missingKnownObject = service.RenewMercenaryLastSkillTime(new Player { ObjectId = 1 }, mercenaryExecution, currentTimeMilliseconds: 12_000);
		var renewed = service.RenewMercenaryLastSkillTime(player, mercenaryExecution, currentTimeMilliseconds: 12_345);

		Assert.Equal(PlayerSummonKnownObjectLastSkillTimeRenewalStatus.MissingExecution, missingExecution.Status);
		Assert.Equal(PlayerSummonKnownObjectLastSkillTimeRenewalStatus.NotRenewable, notRenewable.Status);
		Assert.Equal(PlayerSummonKnownObjectLastSkillTimeRenewalStatus.MissingKnownObject, missingKnownObject.Status);
		Assert.Equal(PlayerSummonKnownObjectLastSkillTimeRenewalStatus.Renewed, renewed.Status);
		Assert.Equal(12_345, renewed.LastSkillTimeMilliseconds);
		Assert.True(player.TryGetSummonKnownObject(8002, out var knownObject));
		Assert.Equal(12_345, knownObject.LastSkillTimeMilliseconds);
	}

	[Fact]
	public void PreviewInvocationUse_GatesReleaseOnSuccessfulSkillUse()
	{
		var service = new PlayerSummonSkillExecutionService();
		var target = new PlayerSummonCastSpellTarget(7001, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: false);
		var releasePlan = new PlayerSummonSkillInvocationPlan(
			PlayerSummonSkillInvocationActorKind.Summon,
			ActorObjectId: 8001,
			ActorTemplateId: 833288,
			SkillId: 22107,
			SkillLevel: 1,
			target,
			Hate: 5,
			ReleaseOnSuccess: true);
		var noReleasePlan = releasePlan with { ReleaseOnSuccess = false };
		var mercenaryPlan = releasePlan with
		{
			ActorKind = PlayerSummonSkillInvocationActorKind.Mercenary,
			ActorObjectId = 8002,
			Hate = 0,
			ReleaseOnSuccess = false,
		};

		var missingExecution = service.PreviewInvocationUse(null, skillUseSucceeded: true);
		var notReady = service.PreviewInvocationUse(
			PlayerSummonSkillInvocationExecutionResult.MissingSkillTemplate(releasePlan),
			skillUseSucceeded: true);
		var failedUse = service.PreviewInvocationUse(
			PlayerSummonSkillInvocationExecutionResult.WouldUseSkill(releasePlan, skillTemplateId: 22107, skillCooldownId: 0),
			skillUseSucceeded: false);
		var wouldRelease = service.PreviewInvocationUse(
			PlayerSummonSkillInvocationExecutionResult.WouldUseSkill(releasePlan, skillTemplateId: 22107, skillCooldownId: 0),
			skillUseSucceeded: true);
		var wouldNotRelease = service.PreviewInvocationUse(
			PlayerSummonSkillInvocationExecutionResult.WouldUseSkill(noReleasePlan, skillTemplateId: 22107, skillCooldownId: 0),
			skillUseSucceeded: true);
		var mercenaryUse = service.PreviewInvocationUse(
			PlayerSummonSkillInvocationExecutionResult.WouldUseSkill(mercenaryPlan, skillTemplateId: 22107, skillCooldownId: 0),
			skillUseSucceeded: true);

		Assert.Equal(PlayerSummonSkillInvocationUseStatus.MissingExecution, missingExecution.Status);
		Assert.Equal(PlayerSummonSkillInvocationUseStatus.NotReadyToUseSkill, notReady.Status);
		Assert.Equal(PlayerSummonSkillInvocationUseStatus.SkillUseFailed, failedUse.Status);
		Assert.False(failedUse.ShouldReleaseSummon);
		Assert.Equal(PlayerSummonSkillInvocationUseStatus.WouldReleaseSummon, wouldRelease.Status);
		Assert.True(wouldRelease.SkillUseSucceeded);
		Assert.True(wouldRelease.ShouldReleaseSummon);
		Assert.Equal(PlayerSummonSkillInvocationUseStatus.WouldCompleteWithoutRelease, wouldNotRelease.Status);
		Assert.True(wouldNotRelease.SkillUseSucceeded);
		Assert.False(wouldNotRelease.ShouldReleaseSummon);
		Assert.Equal(PlayerSummonSkillInvocationUseStatus.WouldCompleteWithoutRelease, mercenaryUse.Status);
		Assert.False(mercenaryUse.ShouldReleaseSummon);
	}

	[Fact]
	public async Task ValidateExecution_AllowsPetSkillBeforeRepresentedSkillEngineInvocation()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var player = new Player
		{
			HasPetSummon = true,
			PetSummonObjectId = 8001,
			PetSummonNpcId = 833288,
		};
		var order = new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 5, Release: true);

		var service = new PlayerSummonSkillExecutionService();
		var result = service.ValidateExecution(
			player,
			order,
			dataManager.StaticData.PetSkills,
			new PlayerSummonCastSpellTarget(7001, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: false));
		var invocationExecution = service.PlanInvocationExecution(result.InvocationPlan, dataManager.StaticData.SkillTemplates);

		Assert.Equal(PlayerSummonSkillExecutionStatus.WouldInvokeSkillEngine, result.Status);
		Assert.Equal(833288, result.PetSummonNpcId);
		Assert.Same(order, result.Order);
		Assert.Equal(7001, result.ResolvedTarget?.ObjectId);
		Assert.False(result.ResolvedTarget?.IsActorSelfTarget);
		Assert.Equal(PlayerSummonSkillInvocationActorKind.Summon, result.InvocationPlan?.ActorKind);
		Assert.Equal(8001, result.InvocationPlan?.ActorObjectId);
		Assert.Equal(833288, result.InvocationPlan?.ActorTemplateId);
		Assert.Equal(22107, result.InvocationPlan?.SkillId);
		Assert.Equal(1, result.InvocationPlan?.SkillLevel);
		Assert.Equal(7001, result.InvocationPlan?.Target?.ObjectId);
		Assert.Equal(5, result.InvocationPlan?.Hate);
		Assert.True(result.InvocationPlan?.ReleaseOnSuccess);
		Assert.Equal(PlayerSummonSkillInvocationExecutionStatus.WouldUseSkill, invocationExecution.Status);
		Assert.Equal(22107, invocationExecution.SkillTemplateId);
		Assert.Equal(result.InvocationPlan, invocationExecution.InvocationPlan);
		Assert.Equal(
			[
				PlayerSummonSkillInvocationExecutionAction.ResolveSkillTemplate,
				PlayerSummonSkillInvocationExecutionAction.SetHate,
				PlayerSummonSkillInvocationExecutionAction.UseSkill,
				PlayerSummonSkillInvocationExecutionAction.ReleaseOnSuccessfulUse,
			],
			invocationExecution.Actions);
		Assert.True(result.Order.Release);
		Assert.Equal(5, result.Order.Hate);
		Assert.Equal(
			[
				PlayerSummonSkillExecutionAction.GetSkill,
				PlayerSummonSkillExecutionAction.SetHate,
				PlayerSummonSkillExecutionAction.UseSkill,
				PlayerSummonSkillExecutionAction.ReleaseOnSuccess,
			],
			result.Actions);
	}

	[Fact]
	public async Task ValidateExecution_PlansNoReleaseWhenQueuedOrderDoesNotRelease()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var player = new Player
		{
			HasPetSummon = true,
			PetSummonObjectId = 8001,
			PetSummonNpcId = 833288,
		};

		var service = new PlayerSummonSkillExecutionService();
		var result = service.ValidateExecution(
			player,
			new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 0, Release: false),
			dataManager.StaticData.PetSkills,
			new PlayerSummonCastSpellTarget(7001, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: false));
		var invocationExecution = service.PlanInvocationExecution(result.InvocationPlan, dataManager.StaticData.SkillTemplates);
		var missingTemplate = service.PlanInvocationExecution(result.InvocationPlan, new SkillTemplateTable([]));

		Assert.Equal(PlayerSummonSkillExecutionStatus.WouldInvokeSkillEngine, result.Status);
		Assert.Equal(7001, result.ResolvedTarget?.ObjectId);
		Assert.Equal(PlayerSummonSkillInvocationActorKind.Summon, result.InvocationPlan?.ActorKind);
		Assert.Equal(8001, result.InvocationPlan?.ActorObjectId);
		Assert.Equal(1, result.InvocationPlan?.SkillLevel);
		Assert.False(result.InvocationPlan?.ReleaseOnSuccess);
		Assert.Equal(PlayerSummonSkillInvocationExecutionStatus.WouldUseSkill, invocationExecution.Status);
		Assert.Equal(
			[
				PlayerSummonSkillInvocationExecutionAction.ResolveSkillTemplate,
				PlayerSummonSkillInvocationExecutionAction.SetHate,
				PlayerSummonSkillInvocationExecutionAction.UseSkill,
			],
			invocationExecution.Actions);
		Assert.Equal(PlayerSummonSkillInvocationExecutionStatus.MissingSkillTemplate, missingTemplate.Status);
		Assert.Empty(missingTemplate.Actions);
		Assert.Equal(
			[
				PlayerSummonSkillExecutionAction.GetSkill,
				PlayerSummonSkillExecutionAction.SetHate,
				PlayerSummonSkillExecutionAction.UseSkill,
			],
			result.Actions);
	}

	[Fact]
	public async Task ValidateExecution_RejectsMissingSummonAndInvalidPetSkill()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var service = new PlayerSummonSkillExecutionService();
		var order = new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 0, Release: false);

		var resolvedTarget = new PlayerSummonCastSpellTarget(7001, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: false);
		var missingSummon = service.ValidateExecution(new Player(), order, dataManager.StaticData.PetSkills, resolvedTarget);
		var invalidSkill = service.ValidateExecution(
			new Player
			{
				HasPetSummon = true,
				PetSummonNpcId = 833288,
			},
			order with { SkillId = 9999 },
			dataManager.StaticData.PetSkills,
			resolvedTarget);
		var missingPlan = service.PlanInvocationExecution(invalidSkill.InvocationPlan, dataManager.StaticData.SkillTemplates);

		Assert.Equal(PlayerSummonSkillExecutionStatus.MissingSummon, missingSummon.Status);
		Assert.Equal(PlayerSummonSkillExecutionStatus.InvalidPetSkill, invalidSkill.Status);
		Assert.Equal(9999, invalidSkill.Order.SkillId);
		Assert.Equal(7001, missingSummon.ResolvedTarget?.ObjectId);
		Assert.Equal(7001, invalidSkill.ResolvedTarget?.ObjectId);
		Assert.Null(missingSummon.InvocationPlan);
		Assert.Null(invalidSkill.InvocationPlan);
		Assert.Equal(PlayerSummonSkillInvocationExecutionStatus.MissingPlan, missingPlan.Status);
		Assert.Empty(missingSummon.Actions);
		Assert.Empty(invalidSkill.Actions);
	}

	[Fact]
	public async Task ValidateMercenaryExecution_PlansControllerUseAndAuditsInvalidSkill()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var service = new PlayerSummonSkillExecutionService();
		var player = new Player
		{
			ObjectId = 1,
		};
		player.SetSummonKnownObject(new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary));

		var valid = service.ValidateMercenaryExecution(
			player,
			CreateSummonCastSpell(summonObjectId: 8002, skillId: 22107, skillLevel: 1, targetObjectId: 8002),
			dataManager.StaticData.PetSkills,
			new PlayerSummonCastSpellTarget(8002, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: true));
		var invocationExecution = service.PlanInvocationExecution(valid.InvocationPlan, dataManager.StaticData.SkillTemplates, player);
		var invalid = service.ValidateMercenaryExecution(
			player,
			CreateSummonCastSpell(summonObjectId: 8002, skillId: 9999, skillLevel: 1, targetObjectId: 8002),
			dataManager.StaticData.PetSkills,
			new PlayerSummonCastSpellTarget(8002, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: true));

		Assert.Equal(PlayerMercenarySkillExecutionStatus.WouldInvokeController, valid.Status);
		Assert.Equal(8002, valid.ResolvedTarget?.ObjectId);
		Assert.True(valid.ResolvedTarget?.IsActorSelfTarget);
		Assert.Equal(PlayerSummonSkillInvocationActorKind.Mercenary, valid.InvocationPlan?.ActorKind);
		Assert.Equal(8002, valid.InvocationPlan?.ActorObjectId);
		Assert.Equal(833288, valid.InvocationPlan?.ActorTemplateId);
		Assert.Equal(22107, valid.InvocationPlan?.SkillId);
		Assert.Equal(1, valid.InvocationPlan?.SkillLevel);
		Assert.Equal(8002, valid.InvocationPlan?.Target?.ObjectId);
		Assert.Equal(0, valid.InvocationPlan?.Hate);
		Assert.False(valid.InvocationPlan?.ReleaseOnSuccess);
		Assert.Equal(PlayerSummonSkillInvocationExecutionStatus.WouldUseSkill, invocationExecution.Status);
		Assert.True(invocationExecution.WouldRenewLastSkillTime);
		Assert.Equal(
			[
				PlayerSummonSkillInvocationExecutionAction.SetTarget,
				PlayerSummonSkillInvocationExecutionAction.ResolveSkillTemplate,
				PlayerSummonSkillInvocationExecutionAction.RenewLastSkillTime,
				PlayerSummonSkillInvocationExecutionAction.UseSkill,
			],
			invocationExecution.Actions);
		Assert.Equal(
			[
				PlayerMercenarySkillExecutionAction.SetTarget,
				PlayerMercenarySkillExecutionAction.UseSkill,
			],
			valid.Actions);
		Assert.Equal(PlayerMercenarySkillExecutionStatus.InvalidMercenarySkill, invalid.Status);
		Assert.Equal(8002, invalid.ResolvedTarget?.ObjectId);
		Assert.Null(invalid.InvocationPlan);
		var audit = Assert.IsType<PlayerMercenarySkillExecutionAudit>(invalid.Audit);
		Assert.Equal(PlayerMercenarySkillExecutionAuditKind.InvalidMercenarySkill, audit.Kind);
		Assert.Empty(invalid.Actions);
	}

	[Fact]
	public async Task PlanInvocationExecution_BlocksDisabledMercenarySkillBeforeControllerUse()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var service = new PlayerSummonSkillExecutionService();
		var skillTemplate = dataManager.StaticData.SkillTemplates.GetSkillTemplate(22107)
			?? throw new InvalidOperationException("Expected test skill template.");
		var player = new Player
		{
			ObjectId = 1,
		};
		player.SetSummonKnownObject(new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary,
			DisabledSkillCooldownIds: new HashSet<int> { skillTemplate.CooldownId }));
		var valid = service.ValidateMercenaryExecution(
			player,
			CreateSummonCastSpell(summonObjectId: 8002, skillId: 22107, skillLevel: 1, targetObjectId: 8002),
			dataManager.StaticData.PetSkills,
			new PlayerSummonCastSpellTarget(8002, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: true));

		var invocationExecution = service.PlanInvocationExecution(valid.InvocationPlan, dataManager.StaticData.SkillTemplates, player);

		Assert.Equal(PlayerMercenarySkillExecutionStatus.WouldInvokeController, valid.Status);
		Assert.Equal(PlayerSummonSkillInvocationExecutionStatus.DisabledNpcSkill, invocationExecution.Status);
		Assert.Equal(22107, invocationExecution.SkillTemplateId);
		Assert.Equal(skillTemplate.CooldownId, invocationExecution.SkillCooldownId);
		Assert.False(invocationExecution.WouldRenewLastSkillTime);
		Assert.Empty(invocationExecution.Actions);
	}

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "game-server")))
			directory = directory.Parent;

		return directory?.FullName ?? throw new InvalidOperationException("Unable to locate repository root.");
	}

	private static SkillTemplateSummary CreateSkillTemplate(string skillType)
	{
		return new SkillTemplateSummary(
			SkillId: 22107,
			Name: "summon_skill",
			NameId: 0,
			Level: 1,
			Group: "",
			Stack: "",
			SkillType: skillType,
			SkillSubType: "",
			CooldownId: 0,
			Cooldown: 0);
	}

	private static CmSummonCastSpell CreateSummonCastSpell(int summonObjectId, int skillId, int skillLevel, int targetObjectId)
	{
		var packet = new CmSummonCastSpell(205, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(summonObjectId);
		buffer.WriteH(skillId);
		buffer.WriteC(skillLevel);
		buffer.WriteD(targetObjectId);
		buffer.WriteD(0);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}
}
