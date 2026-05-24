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
