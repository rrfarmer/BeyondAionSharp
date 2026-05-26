using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListAttackSpeedFactResolverServiceTests
{
	[Fact]
	public void Resolve_WithoutWeaponUsesJavaDefaultAttackSpeedAsApproximation()
	{
		var service = new PlayerKnownListAttackSpeedFactResolverService();
		var player = CreatePlayer();

		var result = service.Resolve(player, CreateItemTemplates());

		Assert.Equal(PlayerKnownListAttackSpeedFactResolutionStatus.ResolvedApproximation, result.Status);
		Assert.NotNull(result.Facts);
		Assert.Equal(1500, result.Facts.BaseAttackSpeed);
		Assert.Equal(1500, result.Facts.CurrentAttackSpeed);
		Assert.True(result.NeedsJavaStatParity);
		Assert.False(result.IsLive);
		Assert.False(result.IsJavaStatParity);
		Assert.Contains("PlayerGameStats.getAttackSpeed", result.JavaSource);
	}

	[Fact]
	public void Resolve_UsesMainHandAndQuarterOffHandWeaponAttackSpeed()
	{
		var service = new PlayerKnownListAttackSpeedFactResolverService();
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = MainHandSwordId, Location = 0, IsEquipped = true, Slot = MainHandSlot },
			new InventoryItem { ObjectId = 1002, ItemId = OffHandSwordId, Location = 0, IsEquipped = true, Slot = SubHandSlot },
		];

		var result = service.Resolve(player, CreateItemTemplates());

		Assert.Equal(PlayerKnownListAttackSpeedFactResolutionStatus.ResolvedApproximation, result.Status);
		Assert.NotNull(result.Facts);
		Assert.Equal(1400 + 1600 / 4, result.Facts.BaseAttackSpeed);
		Assert.Equal(result.Facts.BaseAttackSpeed, result.Facts.CurrentAttackSpeed);
		Assert.Contains("Current attack speed equals base", result.Notes);
	}

	[Fact]
	public void Resolve_IgnoresOffHandCandidateWhenItOccupiesTwoHandedSlot()
	{
		var service = new PlayerKnownListAttackSpeedFactResolverService();
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = MainHandSwordId, Location = 0, IsEquipped = true, Slot = MainHandSlot },
			new InventoryItem { ObjectId = 1002, ItemId = GreatswordId, Location = 0, IsEquipped = true, Slot = MainHandSlot | SubHandSlot },
		];

		var result = service.Resolve(player, CreateItemTemplates());

		Assert.Equal(PlayerKnownListAttackSpeedFactResolutionStatus.ResolvedApproximation, result.Status);
		Assert.NotNull(result.Facts);
		Assert.Equal(1400, result.Facts.BaseAttackSpeed);
		Assert.Equal(1400, result.Facts.CurrentAttackSpeed);
	}

	[Fact]
	public void Resolve_MissingInputsReturnExplicitBlockedMetadata()
	{
		var service = new PlayerKnownListAttackSpeedFactResolverService();

		var missingPlayer = service.Resolve(player: null, CreateItemTemplates());
		var missingTemplates = service.Resolve(CreatePlayer(), itemTemplates: null);

		Assert.Equal(PlayerKnownListAttackSpeedFactResolutionStatus.MissingPlayer, missingPlayer.Status);
		Assert.Null(missingPlayer.Facts);
		Assert.True(missingPlayer.NeedsJavaStatParity);
		Assert.Equal(PlayerKnownListAttackSpeedFactResolutionStatus.MissingItemTemplates, missingTemplates.Status);
		Assert.Null(missingTemplates.Facts);
		Assert.True(missingTemplates.NeedsJavaStatParity);
	}

	private static Player CreatePlayer() =>
		new()
		{
			ObjectId = 9001,
			Race = "ELYOS",
			Gender = "MALE",
			PlayerClass = "GLADIATOR",
		};

	private static ItemTemplateTable CreateItemTemplates() =>
		new(
		[
			CreateWeapon(MainHandSwordId, "SWORD", 1400),
			CreateWeapon(OffHandSwordId, "SWORD", 1600),
			CreateWeapon(GreatswordId, "GREATSWORD", 2200),
		]);

	private static ItemTemplateSummary CreateWeapon(int itemId, string itemGroup, int attackSpeed) =>
		new(
			itemId,
			$"weapon {itemId}",
			DescriptionId: 0,
			Mask: 0,
			Level: 1,
			itemGroup,
			ItemType: "WEAPON",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: 1,
			ValidEquipmentSlots: MainHandSlot | SubHandSlot,
			WeaponStats: new ItemWeaponStats(
				MinDamage: 1,
				MaxDamage: 2,
				attackSpeed,
				PhysicalCritical: 0,
				PhysicalAccuracy: 0,
				Parry: 0,
				MagicalAccuracy: 0,
				MagicalBoost: 0,
				AttackRange: 1500,
				HitCount: 1,
				ReduceMax: 0));

	private const int MainHandSwordId = 100000001;
	private const int OffHandSwordId = 100000002;
	private const int GreatswordId = 100000003;
	private const long MainHandSlot = 1L;
	private const long SubHandSlot = 1L << 1;
}
