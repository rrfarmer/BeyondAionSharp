using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class SkillStatConditionInputSnapshotServiceTests
{
	[Fact]
	public void CreateCreatureSnapshot_ProjectsMainHandItemGroupAndFlyingState()
	{
		var player = new Player
		{
			ObjectId = 1,
			Position = new WorldPosition(1, 1, 2, 3, 0),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 100,
					ItemId = 10,
					Location = 0,
					IsEquipped = true,
					Slot = 1
				}
			]
		};
		player.StartFlying();

		var snapshot = SkillStatConditionInputSnapshotService.CreateCreatureSnapshot(
			player,
			new ItemTemplateTable([CreateTemplate(10, "ORB")]));

		Assert.True(snapshot.HasPlayerOwner);
		Assert.Equal("ORB", snapshot.MainHandWeaponItemGroup);
		Assert.True(snapshot.IsFlying);
		Assert.Empty(snapshot.MissingInputs);
		Assert.Contains("getMainHandWeaponType", snapshot.JavaSource, StringComparison.Ordinal);
		Assert.Contains("isFlying", snapshot.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateCreatureSnapshot_RecordsMissingMainHandAndTemplateInputs()
	{
		var player = new Player
		{
			ObjectId = 1,
			Position = new WorldPosition(1, 1, 2, 3, 0)
		};

		var missingTemplates = SkillStatConditionInputSnapshotService.CreateCreatureSnapshot(player, null);

		Assert.True(missingTemplates.HasPlayerOwner);
		Assert.Null(missingTemplates.MainHandWeaponItemGroup);
		Assert.False(missingTemplates.IsFlying);
		Assert.Contains("item_templates for main-hand ItemGroup lookup", missingTemplates.MissingInputs);
		Assert.Contains("equipped main-hand item", missingTemplates.MissingInputs);

		var missingOwner = SkillStatConditionInputSnapshotService.CreateCreatureSnapshot(null, null);

		Assert.False(missingOwner.HasPlayerOwner);
		Assert.Contains("Stat2 owner Player/Creature", missingOwner.MissingInputs);
		Assert.Contains("non-player owners pass", missingOwner.JavaSource, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(1, 1)]
	[InlineData(ItemChargeService.Level1ChargePoints, 1)]
	[InlineData(ItemChargeService.Level1ChargePoints + 1, 2)]
	[InlineData(ItemChargeService.Level2ChargePoints, 2)]
	public void GetJavaChargeLevel_UsesJavaItemChargeLevelThresholds(int chargePoints, int expectedLevel)
	{
		Assert.Equal(expectedLevel, SkillStatConditionInputSnapshotService.GetJavaChargeLevel(chargePoints));
	}

	[Fact]
	public void CreateItemOwnerSnapshot_ProjectsItemChargeLevelAndMissingOwner()
	{
		var itemSnapshot = SkillStatConditionInputSnapshotService.CreateItemOwnerSnapshot(new InventoryItem
		{
			ObjectId = 100,
			ItemId = 10,
			Charge = ItemChargeService.Level1ChargePoints + 1
		});

		Assert.True(itemSnapshot.HasItemOwner);
		Assert.Equal(2, itemSnapshot.ChargeLevel);
		Assert.Empty(itemSnapshot.MissingInputs);
		Assert.Contains("Item.getChargeLevel", itemSnapshot.JavaSource, StringComparison.Ordinal);

		var missingItem = SkillStatConditionInputSnapshotService.CreateItemOwnerSnapshot(null);

		Assert.False(missingItem.HasItemOwner);
		Assert.Equal(0, missingItem.ChargeLevel);
		Assert.Contains("IStatFunction Item owner", missingItem.MissingInputs);
		Assert.Contains("returns false", missingItem.JavaSource, StringComparison.Ordinal);
	}

	private static ItemTemplateSummary CreateTemplate(int itemId, string itemGroup)
	{
		return new ItemTemplateSummary(
			itemId,
			$"Item {itemId}",
			DescriptionId: 0,
			Mask: 0,
			Level: 1,
			itemGroup,
			ItemType: "normal",
			Quality: "COMMON",
			Race: "ALL",
			MaxStackCount: 1,
			Price: 0,
			ValidEquipmentSlots: 1);
	}
}
