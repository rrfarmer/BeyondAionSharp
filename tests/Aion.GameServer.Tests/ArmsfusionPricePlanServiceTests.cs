using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ArmsfusionPricePlanServiceTests
{
	[Theory]
	[InlineData("JUNK", 200)]
	[InlineData("COMMON", 200)]
	[InlineData("RARE", 250)]
	[InlineData("LEGEND", 300)]
	[InlineData("UNIQUE", 400)]
	[InlineData("EPIC", 500)]
	[InlineData("MYTHIC", 600)]
	[InlineData("ANCIENT", 600)]
	public void GetBasePricePerLevelSquared_UsesJavaQualityMapping(string quality, long expectedBasePrice)
	{
		Assert.Equal(expectedBasePrice, ArmsfusionPricePlanService.GetBasePricePerLevelSquared(quality));
	}

	[Fact]
	public void CreatePlan_UsesJavaPriceFormulaForMainWeaponLevelAndQuality()
	{
		var template = CreateWeaponTemplate(level: 10, quality: "UNIQUE");

		var plan = ArmsfusionPricePlanService.CreatePlan(
			template,
			"ELYOS",
			new GameServerPriceOptions(),
			new PriceInfluenceRates(Elyos: 0.3f, Asmodians: 0.5f));

		Assert.False(plan.IsLive);
		Assert.Equal(400, plan.BasePricePerLevelSquared);
		Assert.Equal(10, plan.MainWeaponLevel);
		Assert.Equal(40_000, plan.BasePrice);
		Assert.Equal(46_200, plan.FusionPrice);
		Assert.Contains("ArmsfusionService.fusionWeapons", plan.JavaSource, StringComparison.Ordinal);
	}

	private static ItemTemplateSummary CreateWeaponTemplate(int level, string quality)
	{
		return new ItemTemplateSummary(
			TemplateId: 1001,
			Name: "Fusion Sword",
			DescriptionId: 0,
			Mask: 1 << 11,
			Level: level,
			ItemGroup: "SWORD",
			ItemType: "NORMAL",
			Quality: quality,
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: 0,
			ValidEquipmentSlots: 1);
	}
}
