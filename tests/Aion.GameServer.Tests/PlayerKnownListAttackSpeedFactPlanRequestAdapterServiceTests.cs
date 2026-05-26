using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListAttackSpeedFactPlanRequestAdapterServiceTests
{
	[Fact]
	public void AttachRideAttackSpeedResolution_AddsResolverResultForRideRequest()
	{
		var service = new PlayerKnownListAttackSpeedFactPlanRequestAdapterService();
		var subject = CreatePlayer();
		subject.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = MainHandSwordId, Location = 0, IsEquipped = true, Slot = MainHandSlot },
		];
		var request = new PlayerKnownListPacketConstructionFactPlanRequest(
			CreatePlayer(),
			subject,
			new PlayerKnownListOperationSideEffectDirectionFacts(SubjectIsInRideMode: true));

		var adapted = service.AttachRideAttackSpeedResolution(request, CreateItemTemplates());

		Assert.NotSame(request, adapted);
		Assert.Null(adapted.RideAttackSpeedFacts);
		Assert.NotNull(adapted.RideAttackSpeedResolution);
		Assert.Equal(PlayerKnownListAttackSpeedFactResolutionStatus.ResolvedApproximation, adapted.RideAttackSpeedResolution.Status);
		Assert.Equal(1400, adapted.RideAttackSpeedResolution.Facts!.BaseAttackSpeed);
	}

	[Fact]
	public void AttachRideAttackSpeedResolution_PreservesSuppliedFactsAndExplicitResolution()
	{
		var service = new PlayerKnownListAttackSpeedFactPlanRequestAdapterService();
		var supplied = new PlayerKnownListPacketConstructionAttackSpeedFacts(1300, 1200);
		var explicitResolution = new PlayerKnownListAttackSpeedFactResolution(
			PlayerKnownListAttackSpeedFactResolutionStatus.MissingItemTemplates,
			Facts: null,
			NeedsJavaStatParity: true,
			IsLive: false,
			IsJavaStatParity: false,
			"com.aionemu.gameserver.model.stats.container.PlayerGameStats.getAttackSpeed",
			"Explicit test resolution.");
		var suppliedRequest = new PlayerKnownListPacketConstructionFactPlanRequest(
			CreatePlayer(),
			CreatePlayer(),
			new PlayerKnownListOperationSideEffectDirectionFacts(SubjectIsInRideMode: true),
			RideAttackSpeedFacts: supplied);
		var explicitRequest = suppliedRequest with
		{
			RideAttackSpeedFacts = null,
			RideAttackSpeedResolution = explicitResolution,
		};

		Assert.Same(suppliedRequest, service.AttachRideAttackSpeedResolution(suppliedRequest, CreateItemTemplates()));
		Assert.Same(explicitRequest, service.AttachRideAttackSpeedResolution(explicitRequest, CreateItemTemplates()));
	}

	[Fact]
	public void AttachRideAttackSpeedResolution_NonRideRequestIsUnchanged()
	{
		var service = new PlayerKnownListAttackSpeedFactPlanRequestAdapterService();
		var request = new PlayerKnownListPacketConstructionFactPlanRequest(
			CreatePlayer(),
			CreatePlayer(),
			new PlayerKnownListOperationSideEffectDirectionFacts());

		Assert.Same(request, service.AttachRideAttackSpeedResolution(request, CreateItemTemplates()));
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
			new ItemTemplateSummary(
				MainHandSwordId,
				"weapon",
				DescriptionId: 0,
				Mask: 0,
				Level: 1,
				"SWORD",
				ItemType: "WEAPON",
				Quality: "COMMON",
				Race: "PC_ALL",
				MaxStackCount: 1,
				Price: 1,
				ValidEquipmentSlots: MainHandSlot,
				WeaponStats: new ItemWeaponStats(
					MinDamage: 1,
					MaxDamage: 2,
					AttackSpeed: 1400,
					PhysicalCritical: 0,
					PhysicalAccuracy: 0,
					Parry: 0,
					MagicalAccuracy: 0,
					MagicalBoost: 0,
					AttackRange: 1500,
					HitCount: 1,
					ReduceMax: 0)),
		]);

	private const int MainHandSwordId = 100000001;
	private const long MainHandSlot = 1L;
}
