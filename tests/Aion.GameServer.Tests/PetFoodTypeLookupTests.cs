using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Tests;

public sealed class PetFoodTypeLookupTests
{
	[Fact]
	public void IsFoodRejectsExcludedAndStinkyItemsBeforeSpecificTypeLikeJava()
	{
		var itemGroups = PetFoodItemGroups.From(
			(PetFoodType.Armor, Set(1001, 1002)),
			(PetFoodType.Excludes, Set(1001)),
			(PetFoodType.Stinky, Set(1002)));

		Assert.False(itemGroups.IsFood(1001, PetFoodType.Armor));
		Assert.False(itemGroups.IsFood(1002, PetFoodType.Armor));
	}

	[Fact]
	public void IsFoodMatchesSpecificFoodTypeLikeJava()
	{
		var itemGroups = PetFoodItemGroups.From(
			(PetFoodType.Armor, Set(1001)),
			(PetFoodType.Bones, Set(2001)));

		Assert.True(itemGroups.IsFood(1001, PetFoodType.Armor));
		Assert.False(itemGroups.IsFood(1001, PetFoodType.Bones));
		Assert.True(itemGroups.IsFood(2001, PetFoodType.Bones));
	}

	[Fact]
	public void IsFoodMiscellaneousMatchesJavaJunkFoodGroupsOnly()
	{
		var itemGroups = PetFoodItemGroups.From(
			(PetFoodType.Armor, Set(1001)),
			(PetFoodType.BalaurScales, Set(1002)),
			(PetFoodType.Bones, Set(1003)),
			(PetFoodType.Fluids, Set(1004)),
			(PetFoodType.Souls, Set(1005)),
			(PetFoodType.Thorns, Set(1006)),
			(PetFoodType.AetherCherry, Set(2001)));

		foreach (var itemId in new[] { 1001, 1002, 1003, 1004, 1005, 1006 })
		{
			Assert.True(itemGroups.IsFood(itemId, PetFoodType.Miscellaneous));
		}

		Assert.False(itemGroups.IsFood(2001, PetFoodType.Miscellaneous));
	}

	[Fact]
	public void GetFoodTypeReturnsFirstRewardGroupWhoseItemGroupMatchesLikeJava()
	{
		var itemGroups = PetFoodItemGroups.From(
			(PetFoodType.Armor, Set(1001)),
			(PetFoodType.Miscellaneous, Set(9999)));
		var rewards = new[]
		{
			new PetFeedRewardGroup(PetFoodType.Miscellaneous, IsLoved: false, []),
			new PetFeedRewardGroup(PetFoodType.Armor, IsLoved: false, []),
		};

		var foodType = PetFoodTypeLookup.GetFoodType(1001, rewards, itemGroups);

		Assert.Equal(PetFoodType.Miscellaneous, foodType);
	}

	[Fact]
	public void GetFoodTypeReturnsNullWhenNoRewardGroupMatchesLikeJava()
	{
		var itemGroups = PetFoodItemGroups.From((PetFoodType.Armor, Set(1001)));
		var rewards = new[]
		{
			new PetFeedRewardGroup(PetFoodType.Bones, IsLoved: false, []),
		};

		Assert.Null(PetFoodTypeLookup.GetFoodType(1001, rewards, itemGroups));
	}

	private static IReadOnlySet<int> Set(params int[] itemIds)
	{
		return new HashSet<int>(itemIds);
	}
}
