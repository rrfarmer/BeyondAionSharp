using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum PlayerKnownListAttackSpeedFactResolutionStatus
{
	ResolvedApproximation,
	MissingPlayer,
	MissingItemTemplates,
}

public sealed record PlayerKnownListAttackSpeedFactResolution(
	PlayerKnownListAttackSpeedFactResolutionStatus Status,
	PlayerKnownListPacketConstructionAttackSpeedFacts? Facts,
	bool NeedsJavaStatParity,
	bool IsLive,
	bool IsJavaStatParity,
	string JavaSource,
	string Notes);

public sealed class PlayerKnownListAttackSpeedFactResolverService
{
	private const long MainHand = 1L;
	private const long SubHand = 1L << 1;
	private const long MainOffHand = 1L << 17;
	private const long SubOffHand = 1L << 18;
	private const int DefaultBaseAttackSpeed = 1500;

	public PlayerKnownListAttackSpeedFactResolution Resolve(
		Player? player,
		ItemTemplateTable? itemTemplates)
	{
		// Java parity breadcrumb: PlayerGameStats.getAttackSpeed() uses normal
		// player weapon/stat state, not ride stats. This resolver intentionally
		// models only the current C# static item-template approximation.
		const string javaSource =
			"com.aionemu.gameserver.model.stats.container.PlayerGameStats.getAttackSpeed; "
			+ "com.aionemu.gameserver.model.stats.calc.functions.AttackSpeedFunction";

		if (player is null)
		{
			return new PlayerKnownListAttackSpeedFactResolution(
				PlayerKnownListAttackSpeedFactResolutionStatus.MissingPlayer,
				Facts: null,
				NeedsJavaStatParity: true,
				IsLive: false,
				IsJavaStatParity: false,
				javaSource,
				"No player snapshot was supplied for attack-speed fact resolution.");
		}

		if (itemTemplates is null)
		{
			return new PlayerKnownListAttackSpeedFactResolution(
				PlayerKnownListAttackSpeedFactResolutionStatus.MissingItemTemplates,
				Facts: null,
				NeedsJavaStatParity: true,
				IsLive: false,
				IsJavaStatParity: false,
				javaSource,
				"Item templates are required to approximate main/off-hand weapon attack speed.");
		}

		var equippedWeapons = player.InventoryItems
			.Where(item => item.IsEquipped && item.Location == 0)
			.Select(item => (Item: item, Template: itemTemplates.GetItemTemplate(item.ItemId)))
			.Where(item => item.Template?.IsWeapon == true)
			.Select(item => new EquippedWeapon(item.Item, item.Template!))
			.ToArray();
		var mainHand = equippedWeapons.FirstOrDefault(item => IsRightHandSlot(item.Item.Slot));
		var baseAttackSpeed = DefaultBaseAttackSpeed;
		if (mainHand?.Template.WeaponStats is { } mainWeaponStats)
		{
			var offHand = equippedWeapons.FirstOrDefault(item =>
				item != mainHand
				&& IsLeftHandSlot(item.Item.Slot)
				&& !IsTwoHandedSlot(item.Item.Slot));
			baseAttackSpeed = mainWeaponStats.AttackSpeed + (offHand?.Template.WeaponStats?.AttackSpeed / 4 ?? 0);
		}

		return new PlayerKnownListAttackSpeedFactResolution(
			PlayerKnownListAttackSpeedFactResolutionStatus.ResolvedApproximation,
			new PlayerKnownListPacketConstructionAttackSpeedFacts(baseAttackSpeed, baseAttackSpeed),
			NeedsJavaStatParity: true,
			IsLive: false,
			IsJavaStatParity: false,
			javaSource,
			"Resolved normal player attack speed from equipped weapon item templates only. Current attack speed equals base attack speed until Java Stat2 and duplicate-stat modifiers are ported.");
	}

	private static bool IsRightHandSlot(long slot)
	{
		return (slot & (MainHand | MainOffHand)) != 0;
	}

	private static bool IsLeftHandSlot(long slot)
	{
		return (slot & (SubHand | SubOffHand)) != 0;
	}

	private static bool IsTwoHandedSlot(long slot)
	{
		return (slot & (MainHand | SubHand)) == (MainHand | SubHand)
			|| (slot & (MainOffHand | SubOffHand)) == (MainOffHand | SubOffHand);
	}

	private sealed record EquippedWeapon(InventoryItem Item, ItemTemplateSummary Template);
}
