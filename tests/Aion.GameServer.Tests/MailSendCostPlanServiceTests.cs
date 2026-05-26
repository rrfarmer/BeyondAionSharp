using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class MailSendCostPlanServiceTests
{
	[Theory]
	[InlineData("MYTHIC", 0.05f)]
	[InlineData("EPIC", 0.05f)]
	[InlineData("UNIQUE", 0.04f)]
	[InlineData("LEGEND", 0.04f)]
	[InlineData("RARE", 0.03f)]
	[InlineData("COMMON", 0.02f)]
	[InlineData("JUNK", 0.02f)]
	public void GetQualityPriceRate_UsesJavaMailQualityMapping(string quality, float expectedRate)
	{
		Assert.Equal(expectedRate, MailSendCostPlanService.GetQualityPriceRate(quality));
	}

	[Fact]
	public void CreatePlan_UsesJavaMailCommissionsAndPricesService()
	{
		var itemTemplate = CreateItemTemplate(price: 10_000, quality: "UNIQUE");

		var plan = MailSendCostPlanService.CreatePlan(
			letterTypeId: MailSendCostPlanService.ExpressLetterTypeId,
			attachedKinah: 12_345,
			itemTemplate,
			attachedItemCount: 2,
			senderRace: "ELYOS",
			priceOptions: new GameServerPriceOptions(),
			influenceRates: new PriceInfluenceRates(Elyos: 0.3f, Asmodians: 0.5f));

		Assert.False(plan.IsLive);
		Assert.Equal(500, plan.BaseCost);
		Assert.Equal(5, plan.CostFactor);
		Assert.Equal(617, plan.KinahMailCommission);
		Assert.Equal(4_000, plan.ItemMailCommission);
		Assert.Equal(5_117, plan.ServiceBaseCost);
		Assert.Equal(5_909, plan.ServicePrice);
		Assert.Equal(12_345, plan.AttachedKinah);
		Assert.Equal(18_254, plan.FinalMailKinah);
		Assert.Contains("MailService.sendMail", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_DefaultNormalLetterMatchesJavaBaseline()
	{
		var plan = MailSendCostPlanService.CreatePlan(
			letterTypeId: MailSendCostPlanService.NormalLetterTypeId,
			attachedKinah: 0,
			attachedItemTemplate: null,
			attachedItemCount: 0,
			senderRace: "ELYOS");

		Assert.Equal(10, plan.BaseCost);
		Assert.Equal(1, plan.CostFactor);
		Assert.Equal(10, plan.ServiceBaseCost);
		Assert.Equal(10, plan.ServicePrice);
		Assert.Equal(10, plan.FinalMailKinah);
	}

	private static ItemTemplateSummary CreateItemTemplate(long price, string quality)
	{
		return new ItemTemplateSummary(
			TemplateId: 1001,
			Name: "Mail Sword",
			DescriptionId: 0,
			Mask: 0,
			Level: 1,
			ItemGroup: "SWORD",
			ItemType: "NORMAL",
			Quality: quality,
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: price,
			ValidEquipmentSlots: 1);
	}
}
