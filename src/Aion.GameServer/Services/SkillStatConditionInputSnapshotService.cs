using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class SkillStatConditionInputSnapshotService
{
	private const int CubeStorageId = 0;
	private const long MainHandSlot = 1L;

	public static SkillStatConditionCreatureInputSnapshot CreateCreatureSnapshot(
		Player? player,
		ItemTemplateTable? itemTemplates)
	{
		var missingInputs = new List<string>();
		if (player == null)
		{
			missingInputs.Add("Stat2 owner Player/Creature");
			return new SkillStatConditionCreatureInputSnapshot(
				HasPlayerOwner: false,
				MainHandWeaponItemGroup: null,
				IsFlying: false,
				missingInputs,
				"WeaponCondition.validate(stat): non-player owners pass; OnFlyCondition requires stat.getOwner().isFlying()");
		}

		if (itemTemplates == null)
			missingInputs.Add("item_templates for main-hand ItemGroup lookup");

		var mainHand = player.InventoryItems.FirstOrDefault(item =>
			item.Location == CubeStorageId
			&& item.IsEquipped
			&& (item.Slot & MainHandSlot) != 0);
		if (mainHand == null)
			missingInputs.Add("equipped main-hand item");

		var mainHandTemplate = mainHand == null || itemTemplates == null
			? null
			: itemTemplates.GetItemTemplate(mainHand.ItemId);
		if (mainHand != null && mainHandTemplate == null)
			missingInputs.Add("main-hand item template");

		return new SkillStatConditionCreatureInputSnapshot(
			HasPlayerOwner: true,
			MainHandWeaponItemGroup: mainHandTemplate?.ItemGroup,
			player.IsFlying(),
			missingInputs,
			"WeaponCondition.validate(stat) uses player.getEquipment().getMainHandWeaponType(); OnFlyCondition.validate(stat) uses stat.getOwner().isFlying()");
	}

	public static SkillStatConditionItemOwnerInputSnapshot CreateItemOwnerSnapshot(InventoryItem? item)
	{
		if (item == null)
		{
			return new SkillStatConditionItemOwnerInputSnapshot(
				HasItemOwner: false,
				ChargeLevel: 0,
				["IStatFunction Item owner"],
				"ItemChargeCondition.validate(stat) returns false when statFunction.getOwner() is not an Item");
		}

		return new SkillStatConditionItemOwnerInputSnapshot(
			HasItemOwner: true,
			GetJavaChargeLevel(item.Charge),
			Array.Empty<string>(),
			"ItemChargeCondition.validate(stat) uses item.getChargeLevel(); Item.getChargeLevel returns 0 for no charge, 1 up to ChargeInfo.LEVEL1, and 2 above ChargeInfo.LEVEL1");
	}

	public static int GetJavaChargeLevel(int chargePoints)
	{
		if (chargePoints <= 0)
			return 0;
		return chargePoints > ItemChargeService.Level1ChargePoints ? 2 : 1;
	}
}

public sealed record SkillStatConditionCreatureInputSnapshot(
	bool HasPlayerOwner,
	string? MainHandWeaponItemGroup,
	bool IsFlying,
	IReadOnlyList<string> MissingInputs,
	string JavaSource);

public sealed record SkillStatConditionItemOwnerInputSnapshot(
	bool HasItemOwner,
	int ChargeLevel,
	IReadOnlyList<string> MissingInputs,
	string JavaSource);
