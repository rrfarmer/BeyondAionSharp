using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Tests;

public sealed class PetFeedXmlProjectionTests
{
	[Fact]
	public void ParseProjectsJavaPetFeedXmlDefaultsAndRewardGroups()
	{
		var flavours = PetFeedXmlProjection.Parse("""
			<pet_feed>
				<flavour id="7" cd="10">
					<food group="ARMOR">
						<result item="188050753" />
						<result item="188050754" />
					</food>
					<food group="AETHER_CRYSTAL_BISCUIT" loved="true">
						<result item="188050961" />
					</food>
				</flavour>
			</pet_feed>
			""");

		var flavour = Assert.Single(flavours).Value;
		Assert.Equal(7, flavour.Id);
		Assert.Equal(1, flavour.FullCount);
		Assert.Equal(0, flavour.LovedFoodLimit);
		Assert.Equal(10, flavour.CooldownSeconds);
		Assert.Equal(2, flavour.RewardGroups.Count);
		Assert.Equal(PetFoodType.Armor, flavour.RewardGroups[0].Type);
		Assert.False(flavour.RewardGroups[0].IsLoved);
		Assert.Equal([188050753, 188050754], flavour.RewardGroups[0].Results.Select(result => result.ItemId).ToArray());
		Assert.Equal(PetFoodType.AetherCrystalBiscuit, flavour.RewardGroups[1].Type);
		Assert.True(flavour.RewardGroups[1].IsLoved);
	}

	[Fact]
	public void ParseUsesLaterDuplicateFlavourLikeJavaMapPut()
	{
		var flavours = PetFeedXmlProjection.Parse("""
			<pet_feed>
				<flavour id="7" full_count="10" cd="1">
					<food group="ARMOR" />
				</flavour>
				<flavour id="7" full_count="25" loved_limit="3" cd="2">
					<food group="BONES" />
				</flavour>
			</pet_feed>
			""");

		var flavour = Assert.Single(flavours).Value;
		Assert.Equal(25, flavour.FullCount);
		Assert.Equal(3, flavour.LovedFoodLimit);
		Assert.Equal(2, flavour.CooldownSeconds);
		Assert.Equal(PetFoodType.Bones, Assert.Single(flavour.RewardGroups).Type);
	}

	[Fact]
	public void GetSortedFullCountsMatchesJavaTreeSetBehavior()
	{
		var flavours = PetFeedXmlProjection.Parse("""
			<pet_feed>
				<flavour id="1" full_count="100" cd="1"><food group="ARMOR" /></flavour>
				<flavour id="2" full_count="0" cd="1"><food group="BONES" /></flavour>
				<flavour id="3" full_count="25" cd="1"><food group="FLUIDS" /></flavour>
				<flavour id="4" full_count="100" cd="1"><food group="SOULS" /></flavour>
			</pet_feed>
			""");

		Assert.Equal([25, 100], PetFeedXmlProjection.GetSortedFullCounts(flavours));
	}

	[Fact]
	public void ParseCheckedInPetFeedXmlMatchesCurrentJavaStaticDataShape()
	{
		var repoRoot = LocateRepoRoot();
		var xml = File.ReadAllText(Path.Combine(repoRoot, "game-server", "data", "static_data", "pets", "pet_feed.xml"));

		var flavours = PetFeedXmlProjection.Parse(xml);

		Assert.Equal(33, flavours.Count);
		Assert.Equal([1, 10, 25, 40, 50, 100, 200], PetFeedXmlProjection.GetSortedFullCounts(flavours));
		Assert.Equal(64, flavours.Values.Sum(flavour => flavour.RewardGroups.Count));
		Assert.Equal(199, flavours.Values.Sum(flavour => flavour.RewardGroups.Sum(group => group.Results.Count)));
	}

	private static string LocateRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			var path = Path.Combine(directory.FullName, "game-server", "data", "static_data", "pets", "pet_feed.xml");
			if (File.Exists(path))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate repository root from " + AppContext.BaseDirectory);
	}
}
