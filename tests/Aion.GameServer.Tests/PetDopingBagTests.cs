using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Tests;

public sealed class PetDopingBagTests
{
	[Fact]
	public void NewBagReturnsJavaZeroDefaultsAndIsNotDirty()
	{
		var bag = new PetDopingBag();

		Assert.Equal(0, bag.FoodItem);
		Assert.Equal(0, bag.DrinkItem);
		Assert.Empty(bag.GetScrollsUsed());
		Assert.Empty(bag.GetItems());
		Assert.False(bag.IsDirty);
	}

	[Fact]
	public void SetFoodAndDrinkUseJavaSlotsAndSetDirty()
	{
		var bag = new PetDopingBag();

		bag.SetFoodItem(166000001);
		bag.SetDrinkItem(162000001);

		Assert.Equal(166000001, bag.FoodItem);
		Assert.Equal(162000001, bag.DrinkItem);
		Assert.Equal([166000001, 162000001], bag.GetItems());
		Assert.True(bag.IsDirty);
	}

	[Fact]
	public void SetItemExpandsBackingArrayToTouchedSlotLikeJava()
	{
		var bag = new PetDopingBag();

		bag.SetItem(164000003, slot: 4);

		Assert.Equal([0, 0, 0, 0, 164000003], bag.GetItems());
		Assert.Equal([0, 0, 164000003], bag.GetScrollsUsed());
		Assert.True(bag.IsDirty);
	}

	[Fact]
	public void SetItemExpandsButDoesNotMarkDirtyWhenZeroValueIsUnchangedLikeJava()
	{
		var bag = new PetDopingBag();

		bag.SetItem(0, slot: 4);

		Assert.Equal([0, 0, 0, 0, 0], bag.GetItems());
		Assert.False(bag.IsDirty);
	}

	[Fact]
	public void SetItemLeavesDirtyStateTrueWhenAlreadyDirtyAndValueIsUnchangedLikeJava()
	{
		var bag = new PetDopingBag();
		bag.SetItem(164000003, slot: 4);
		var dirtyAfterFirstSet = bag.IsDirty;

		bag.SetItem(164000003, slot: 4);

		Assert.True(dirtyAfterFirstSet);
		Assert.True(bag.IsDirty);
		Assert.Equal([0, 0, 0, 0, 164000003], bag.GetItems());
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(PetDopingBag.MaxItems)]
	public void SetItemRejectsSlotsOutsideJavaRange(int slot)
	{
		var bag = new PetDopingBag();

		Assert.Throws<ArgumentOutOfRangeException>(() => bag.SetItem(1, slot));
	}

	[Fact]
	public void SwitchItemsIgnoresFoodAndDrinkSlotsLikeJava()
	{
		var bag = new PetDopingBag();
		bag.SetItem(166000001, slot: 0);
		bag.SetItem(164000001, slot: 2);

		bag.SwitchItems(0, 2);
		bag.SwitchItems(2, 1);

		Assert.Equal([166000001, 0, 164000001], bag.GetItems());
	}

	[Fact]
	public void SwitchItemsSwapsScrollSlotsAndExpandsMissingSlotLikeJava()
	{
		var bag = new PetDopingBag();
		bag.SetItem(164000001, slot: 2);

		bag.SwitchItems(2, 5);

		Assert.Equal([0, 0, 0, 0, 0, 164000001], bag.GetItems());
		Assert.Equal([0, 0, 0, 164000001], bag.GetScrollsUsed());
	}

	[Fact]
	public void SwitchItemsThrowsWhenStorageIsUninitializedLikeJavaNullDereference()
	{
		var bag = new PetDopingBag();

		Assert.Throws<NullReferenceException>(() => bag.SwitchItems(2, 3));
	}

	[Fact]
	public void GetItemsReturnsCopyToAvoidExternalMutation()
	{
		var bag = new PetDopingBag();
		bag.SetItem(164000001, slot: 2);
		var items = bag.GetItems();

		items[2] = 1;

		Assert.Equal([0, 0, 164000001], bag.GetItems());
	}
}
