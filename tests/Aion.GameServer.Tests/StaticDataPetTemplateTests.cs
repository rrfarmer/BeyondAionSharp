using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.Pet;

namespace Aion.GameServer.Tests;

public sealed class StaticDataPetTemplateTests
{
	[Fact]
	public async Task LoadFromCacheAsync_ParsesPetMerchantFunctionRatePrice()
	{
		var tempDirectory = Path.Combine(Path.GetTempPath(), "aion-static-pet-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDirectory);
		var cacheFile = Path.Combine(tempDirectory, "static_data.xml");
		try
		{
			await File.WriteAllTextAsync(
				cacheFile,
				"""
				<static_data>
					<pets>
						<pet id="900210" name="merchant pet" nameid="1600210" condition_reward="0">
							<petfunction id="3" type="MERCHANT" rate_price="15"/>
						</pet>
					</pets>
				</static_data>
				""");

			var staticData = await StaticData.LoadFromCacheAsync(cacheFile, Array.Empty<string>());

			Assert.Equal(1, staticData.PetTemplates.Count);
			var template = staticData.PetTemplates.GetPetTemplate(900210);
			Assert.NotNull(template);
			Assert.True(template.ContainsFunction(PetFunctionType.Merchant));
			var function = template.GetFunction(PetFunctionType.Merchant);
			Assert.NotNull(function);
			Assert.Equal((3, 15), (function.Id, function.RatePrice));
			Assert.Equal(15, staticData.PetTemplates.GetMerchantSellModifier(900210));
		}
		finally
		{
			if (Directory.Exists(tempDirectory))
				Directory.Delete(tempDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task LoadFromCacheAsync_ParsesPetDopingEntries()
	{
		var tempDirectory = Path.Combine(Path.GetTempPath(), "aion-static-pet-doping-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDirectory);
		var cacheFile = Path.Combine(tempDirectory, "static_data.xml");
		try
		{
			await File.WriteAllTextAsync(
				cacheFile,
				"""
				<static_data>
					<dopings>
						<doping id="8" usedrink="false" usefood="false" usescroll="1"/>
						<doping id="27" usedrink="true" usefood="true" usescroll="2"/>
					</dopings>
				</static_data>
				""");

			var staticData = await StaticData.LoadFromCacheAsync(cacheFile, Array.Empty<string>());

			Assert.Equal(2, staticData.PetDopings.Count);
			var entry = staticData.PetDopings.GetDopingTemplate(27);
			Assert.NotNull(entry);
			Assert.Equal((27, true, true, 2), (entry.Id, entry.UseDrink, entry.UseFood, entry.ScrollsUsed));
			var scrollOnlyEntry = staticData.PetDopings.GetDopingTemplate(8);
			Assert.NotNull(scrollOnlyEntry);
			Assert.Equal((false, false, 1), (scrollOnlyEntry.UseDrink, scrollOnlyEntry.UseFood, scrollOnlyEntry.ScrollsUsed));
		}
		finally
		{
			if (Directory.Exists(tempDirectory))
				Directory.Delete(tempDirectory, recursive: true);
		}
	}
}
