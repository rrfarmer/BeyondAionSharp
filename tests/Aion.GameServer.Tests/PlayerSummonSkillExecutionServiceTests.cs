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

		var result = new PlayerSummonSkillExecutionService().ValidateExecution(
			player,
			order,
			dataManager.StaticData.PetSkills,
			new PlayerSummonCastSpellTarget(7001, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: false));

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

		var result = new PlayerSummonSkillExecutionService().ValidateExecution(
			player,
			new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 0, Release: false),
			dataManager.StaticData.PetSkills,
			new PlayerSummonCastSpellTarget(7001, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: false));

		Assert.Equal(PlayerSummonSkillExecutionStatus.WouldInvokeSkillEngine, result.Status);
		Assert.Equal(7001, result.ResolvedTarget?.ObjectId);
		Assert.Equal(PlayerSummonSkillInvocationActorKind.Summon, result.InvocationPlan?.ActorKind);
		Assert.Equal(8001, result.InvocationPlan?.ActorObjectId);
		Assert.Equal(1, result.InvocationPlan?.SkillLevel);
		Assert.False(result.InvocationPlan?.ReleaseOnSuccess);
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

		Assert.Equal(PlayerSummonSkillExecutionStatus.MissingSummon, missingSummon.Status);
		Assert.Equal(PlayerSummonSkillExecutionStatus.InvalidPetSkill, invalidSkill.Status);
		Assert.Equal(9999, invalidSkill.Order.SkillId);
		Assert.Equal(7001, missingSummon.ResolvedTarget?.ObjectId);
		Assert.Equal(7001, invalidSkill.ResolvedTarget?.ObjectId);
		Assert.Null(missingSummon.InvocationPlan);
		Assert.Null(invalidSkill.InvocationPlan);
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
