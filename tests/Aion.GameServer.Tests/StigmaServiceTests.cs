using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class StigmaServiceTests
{
	[Fact]
	public void CreateChargePlan_SuccessConsumesStoneAndRaisesEquippedStigmaSkillLevel()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = StigmaId, Count = 1, Location = 0, IsEquipped = true, Slot = StigmaSlot1, Enchant = 2 },
			new InventoryItem { ObjectId = 1002, ItemId = StigmaId, Count = 1, Location = 0, Slot = 65535 },
		];
		player.Skills =
		[
			new PlayerSkill { SkillId = 500, SkillLevel = 3, SkillType = 1 },
			new PlayerSkill { SkillId = 662, SkillLevel = 3, SkillType = 3 },
		];

		var plan = StigmaService.CreateChargePlan(
			player,
			targetItemObjectId: 1001,
			chargeStoneObjectId: 1002,
			CreateItemTemplates(),
			CreateSkillTemplates(),
			CreateSkillTree(),
			CreateExperienceTable(),
			rollPercent: () => 0);

		Assert.Equal(StigmaChargeResult.Success, plan.Result);
		Assert.True(plan.EnchantSucceeded);
		Assert.Equal(3, plan.TargetItemUpdate?.Enchant);
		Assert.Equal(1002, plan.DeletedSourceItemObjectId);
		Assert.Null(plan.SourceItemUpdate);
		Assert.Null(plan.DeletedTargetItemObjectId);
		Assert.Equal([(500, 3), (662, 3)], plan.RemovedSkills.Select(skill => (skill.SkillId, skill.SkillLevel)).ToArray());
		var added = Assert.Single(plan.AddedSkills);
		Assert.Equal((500, 4, 1), (added.SkillId, added.SkillLevel, added.SkillType));
		Assert.Equal([(500, 4, 1)], plan.Skills.Select(skill => (skill.SkillId, skill.SkillLevel, skill.SkillType)).ToArray());
	}

	[Fact]
	public void CreateChargePlan_FailureConsumesStoneDestroysStigmaAndRemovesSkills()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = StigmaId, Count = 1, Location = 0, IsEquipped = true, Slot = StigmaSlot1, Enchant = 9 },
			new InventoryItem { ObjectId = 1002, ItemId = StigmaId, Count = 1, Location = 0, Slot = 65535 },
		];
		player.Skills =
		[
			new PlayerSkill { SkillId = 500, SkillLevel = 10, SkillType = 1 },
		];

		var plan = StigmaService.CreateChargePlan(
			player,
			targetItemObjectId: 1001,
			chargeStoneObjectId: 1002,
			CreateItemTemplates(),
			CreateSkillTemplates(),
			CreateSkillTree(),
			CreateExperienceTable(),
			rollPercent: () => 99);

		Assert.Equal(StigmaChargeResult.Success, plan.Result);
		Assert.False(plan.EnchantSucceeded);
		Assert.Equal(1001, plan.DeletedTargetItemObjectId);
		Assert.Equal(1002, plan.DeletedSourceItemObjectId);
		Assert.Empty(plan.InventoryItems);
		var removed = Assert.Single(plan.RemovedSkills);
		Assert.Equal((500, 10), (removed.SkillId, removed.SkillLevel));
		Assert.Empty(plan.Skills);
	}

	[Fact]
	public void CreateChargePlan_RejectsWrongStoneOrMaxEnchant()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = StigmaId, Count = 1, Location = 0, Enchant = 10 },
			new InventoryItem { ObjectId = 1002, ItemId = OtherStigmaId, Count = 1, Location = 0 },
		];

		var plan = StigmaService.CreateChargePlan(
			player,
			targetItemObjectId: 1001,
			chargeStoneObjectId: 1002,
			CreateItemTemplates(),
			CreateSkillTemplates(),
			CreateSkillTree(),
			CreateExperienceTable(),
			rollPercent: () => 0);

		Assert.Equal(StigmaChargeResult.Invalid, plan.Result);
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 7001,
			Name = "Tester",
			PlayerClass = "GLADIATOR",
			Race = "ELYOS",
			Gender = "MALE",
			Exp = 60,
		};
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			new ItemTemplateSummary(StigmaId, "Practice Stigma", 0, 1, 20, "STIGMA", "NORMAL", "COMMON", "PC_ALL", 1, 0, StigmaSlot1, StigmaInfo: new ItemStigmaInfo(["STIGMA_TEST"], Chargeable: true)),
			new ItemTemplateSummary(OtherStigmaId, "Other Stigma", 0, 1, 20, "STIGMA", "NORMAL", "COMMON", "PC_ALL", 1, 0, StigmaSlot1, StigmaInfo: new ItemStigmaInfo(["STIGMA_TEST"], Chargeable: true)),
		]);
	}

	private static SkillTemplateTable CreateSkillTemplates()
	{
		return new SkillTemplateTable(
		[
			new SkillTemplateSummary(500, "Practice Stigma Skill", 200, 1, "STIGMA_TEST", "STIGMA_TEST", "PHYSICAL", "ATTACK", 0, 0, StigmaType: "NORMAL"),
			new SkillTemplateSummary(662, "Practice Linked Stigma Skill", 201, 1, "LINKED_STIGMA_TEST", "LINKED_STIGMA_TEST", "PHYSICAL", "ATTACK", 0, 0, StigmaType: "LINKED"),
		]);
	}

	private static SkillTreeTable CreateSkillTree()
	{
		return new SkillTreeTable(
		[
			new SkillLearnSummary("GLADIATOR", 500, null, "PC_ALL", 20, AutoLearn: false, Stigma: 1, SkillLevel: 0),
			new SkillLearnSummary("GLADIATOR", 662, null, "ELYOS", 55, AutoLearn: false, Stigma: 4, SkillLevel: 0),
		], CreateSkillTemplates());
	}

	private static PlayerExperienceTable CreateExperienceTable()
	{
		return new PlayerExperienceTable(Enumerable.Range(0, 70).Select(level => (long)level).ToArray());
	}

	private const int StigmaId = 140001001;
	private const int OtherStigmaId = 140001002;
	private const long StigmaSlot1 = 1L << 30;
}
