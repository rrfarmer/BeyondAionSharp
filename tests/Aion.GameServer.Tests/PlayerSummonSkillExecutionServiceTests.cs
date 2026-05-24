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
			PetSummonNpcId = 833288,
		};
		var order = new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 5, Release: true);

		var result = new PlayerSummonSkillExecutionService().ValidateExecution(player, order, dataManager.StaticData.PetSkills);

		Assert.Equal(PlayerSummonSkillExecutionStatus.WouldInvokeSkillEngine, result.Status);
		Assert.Equal(833288, result.PetSummonNpcId);
		Assert.Same(order, result.Order);
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
			PetSummonNpcId = 833288,
		};

		var result = new PlayerSummonSkillExecutionService().ValidateExecution(
			player,
			new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 0, Release: false),
			dataManager.StaticData.PetSkills);

		Assert.Equal(PlayerSummonSkillExecutionStatus.WouldInvokeSkillEngine, result.Status);
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

		var missingSummon = service.ValidateExecution(new Player(), order, dataManager.StaticData.PetSkills);
		var invalidSkill = service.ValidateExecution(
			new Player
			{
				HasPetSummon = true,
				PetSummonNpcId = 833288,
			},
			order with { SkillId = 9999 },
			dataManager.StaticData.PetSkills);

		Assert.Equal(PlayerSummonSkillExecutionStatus.MissingSummon, missingSummon.Status);
		Assert.Equal(PlayerSummonSkillExecutionStatus.InvalidPetSkill, invalidSkill.Status);
		Assert.Equal(9999, invalidSkill.Order.SkillId);
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
		Assert.Equal(
			[
				PlayerMercenarySkillExecutionAction.SetTarget,
				PlayerMercenarySkillExecutionAction.UseSkill,
			],
			valid.Actions);
		Assert.Equal(PlayerMercenarySkillExecutionStatus.InvalidMercenarySkill, invalid.Status);
		Assert.Equal(8002, invalid.ResolvedTarget?.ObjectId);
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
