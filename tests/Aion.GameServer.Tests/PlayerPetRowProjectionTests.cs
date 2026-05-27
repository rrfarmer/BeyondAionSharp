using Aion.GameServer.Data;
using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Tests;

public sealed class PlayerPetRowProjectionTests
{
	[Fact]
	public void ProjectHydratesBasicPetFieldsLikeJavaGetPlayerPets()
	{
		var birthday = DateTimeOffset.FromUnixTimeSeconds(1234);
		var despawn = DateTimeOffset.FromUnixTimeSeconds(5678);

		var projection = PlayerPetRowProjection.Project(
			CreateRow(Birthday: birthday, DespawnTime: despawn),
			new PlayerPetProjectionOptions(HasFoodFunction: false, HasDopingFunction: false),
			() => DateTimeOffset.FromUnixTimeSeconds(9999));

		Assert.Equal(7001, projection.PetObjectId);
		Assert.Equal(900001, projection.TemplateId);
		Assert.Equal(1001, projection.PlayerObjectId);
		Assert.Equal(3600, projection.ExpireTime);
		Assert.Equal("Tog", projection.Name);
		Assert.Equal(12345, projection.Decoration);
		Assert.Equal(birthday, projection.Birthday);
		Assert.Equal(despawn, projection.DespawnTime);
		Assert.Null(projection.FeedProgress);
		Assert.Null(projection.DopingBag);
	}

	[Fact]
	public void ProjectUsesCurrentTimeForNullDespawnTimeLikeJava()
	{
		var fallback = DateTimeOffset.FromUnixTimeSeconds(9999);

		var projection = PlayerPetRowProjection.Project(
			CreateRow(DespawnTime: null),
			new PlayerPetProjectionOptions(HasFoodFunction: false),
			() => fallback);

		Assert.Equal(fallback, projection.DespawnTime);
	}

	[Fact]
	public void ProjectHydratesFeedProgressWhenTemplateHasFoodFunction()
	{
		var projection = PlayerPetRowProjection.Project(
			CreateRow(HungryLevel: 2, FeedProgressData: 0x12345550, ReuseTimeMillis: 123_456),
			new PlayerPetProjectionOptions(HasFoodFunction: true, LovedFoodLimit: 40),
			() => DateTimeOffset.FromUnixTimeSeconds(9999));

		Assert.NotNull(projection.FeedProgress);
		Assert.Equal(PetHungryLevel.Semifull, projection.FeedProgress.HungryLevel);
		Assert.Equal(0x12345550, projection.FeedProgress.GetDataForPacket());
		Assert.Equal(123_456, projection.Timing.RefeedTimeMillis);
	}

	[Fact]
	public void ProjectSkipsFeedProgressWhenTemplateDoesNotHaveFoodFunction()
	{
		var projection = PlayerPetRowProjection.Project(
			CreateRow(HungryLevel: 2, FeedProgressData: 0x12345550, ReuseTimeMillis: 123_456),
			new PlayerPetProjectionOptions(HasFoodFunction: false),
			() => DateTimeOffset.FromUnixTimeSeconds(9999));

		Assert.Null(projection.FeedProgress);
		Assert.Equal(123_456, projection.Timing.RefeedTimeMillis);
	}

	[Fact]
	public void ProjectHydratesMoodTimingFieldsLikeJava()
	{
		var projection = PlayerPetRowProjection.Project(
			CreateRow(
				MoodStartedMillis: 10_000,
				ShuggleCounter: 3,
				MoodCooldownStartedMillis: 20_000,
				GiftCooldownStartedMillis: 30_000),
			new PlayerPetProjectionOptions(HasFoodFunction: false),
			() => DateTimeOffset.FromUnixTimeSeconds(9999));

		Assert.Equal(10_000, projection.Timing.StartMoodTimeMillis);
		Assert.Equal(3, projection.Timing.ShuggleCounter);
		Assert.Equal(20_000, projection.Timing.MoodCooldownStartedMillis);
		Assert.Equal(30_000, projection.Timing.GiftCooldownStartedMillis);
	}

	[Fact]
	public void ProjectDopingBagParsesJavaCsvSlotOrder()
	{
		var projection = PlayerPetRowProjection.Project(
			CreateRow(Dopings: "166000001,162000001,164000001,164000002"),
			new PlayerPetProjectionOptions(HasFoodFunction: false, HasDopingFunction: true),
			() => DateTimeOffset.FromUnixTimeSeconds(9999));

		Assert.NotNull(projection.DopingBag);
		Assert.Equal(166000001, projection.DopingBag.FoodItem);
		Assert.Equal(162000001, projection.DopingBag.DrinkItem);
		Assert.Equal([164000001, 164000002], projection.DopingBag.GetScrollsUsed());
		Assert.Equal([166000001, 162000001, 164000001, 164000002], projection.DopingBag.GetItems());
		Assert.True(projection.DopingBag.IsDirty);
	}

	[Fact]
	public void ProjectDopingBagReturnsEmptyBagForNullCsvLikeJava()
	{
		var projection = PlayerPetRowProjection.Project(
			CreateRow(Dopings: null),
			new PlayerPetProjectionOptions(HasFoodFunction: false, HasDopingFunction: true),
			() => DateTimeOffset.FromUnixTimeSeconds(9999));

		Assert.NotNull(projection.DopingBag);
		Assert.Empty(projection.DopingBag.GetItems());
		Assert.Equal(0, projection.DopingBag.FoodItem);
		Assert.Equal(0, projection.DopingBag.DrinkItem);
		Assert.Empty(projection.DopingBag.GetScrollsUsed());
		Assert.False(projection.DopingBag.IsDirty);
	}

	[Fact]
	public void ProjectDopingBagZeroCsvExpandsWithoutDirtyLikeJavaSetItem()
	{
		var projection = PlayerPetRowProjection.Project(
			CreateRow(Dopings: "0,0,0"),
			new PlayerPetProjectionOptions(HasFoodFunction: false, HasDopingFunction: true),
			() => DateTimeOffset.FromUnixTimeSeconds(9999));

		Assert.NotNull(projection.DopingBag);
		Assert.Equal([0, 0, 0], projection.DopingBag.GetItems());
		Assert.Equal(0, projection.DopingBag.FoodItem);
		Assert.Equal(0, projection.DopingBag.DrinkItem);
		Assert.Equal([0], projection.DopingBag.GetScrollsUsed());
		Assert.False(projection.DopingBag.IsDirty);
	}

	[Fact]
	public void ProjectSkipsDopingBagWhenTemplateDoesNotHaveDopingFunction()
	{
		var projection = PlayerPetRowProjection.Project(
			CreateRow(Dopings: "166000001,162000001"),
			new PlayerPetProjectionOptions(HasFoodFunction: false, HasDopingFunction: false),
			() => DateTimeOffset.FromUnixTimeSeconds(9999));

		Assert.Null(projection.DopingBag);
	}

	[Fact]
	public void ProjectDopingBagRejectsMoreThanJavaMaxSlots()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => PlayerPetRowProjection.ProjectDopingBag("1,2,3,4,5,6,7,8,9"));
	}

	[Fact]
	public void ProjectDopingBagRejectsMalformedCsvLikeJavaParseInt()
	{
		Assert.Throws<FormatException>(() => PlayerPetRowProjection.ProjectDopingBag("1,nope"));
	}

	[Fact]
	public void ProjectRejectsUnknownHungryLevelLikeJavaEnumIndex()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => PlayerPetRowProjection.Project(
			CreateRow(HungryLevel: 4),
			new PlayerPetProjectionOptions(HasFoodFunction: true),
			() => DateTimeOffset.FromUnixTimeSeconds(9999)));
	}

	private static PlayerPetRepositoryRow CreateRow(
		DateTimeOffset? Birthday = null,
		DateTimeOffset? DespawnTime = null,
		int HungryLevel = 0,
		int FeedProgressData = 0,
		long ReuseTimeMillis = 0,
		string? Dopings = null,
		long MoodStartedMillis = 0,
		int ShuggleCounter = 0,
		long MoodCooldownStartedMillis = 0,
		long GiftCooldownStartedMillis = 0)
	{
		return new PlayerPetRepositoryRow(
			PetObjectId: 7001,
			TemplateId: 900001,
			PlayerObjectId: 1001,
			ExpireTime: 3600,
			Name: "Tog",
			Decoration: 12345,
			HungryLevel,
			FeedProgressData,
			ReuseTimeMillis,
			Dopings,
			Birthday,
			MoodStartedMillis,
			ShuggleCounter,
			MoodCooldownStartedMillis,
			GiftCooldownStartedMillis,
			DespawnTime);
	}
}
