using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Data;

public sealed record PlayerPetRepositoryRow(
	int PetObjectId,
	int TemplateId,
	int PlayerObjectId,
	int ExpireTime,
	string Name,
	int Decoration,
	int HungryLevel,
	int FeedProgressData,
	long ReuseTimeMillis,
	string? Dopings,
	DateTimeOffset? Birthday,
	long MoodStartedMillis,
	int ShuggleCounter,
	long MoodCooldownStartedMillis,
	long GiftCooldownStartedMillis,
	DateTimeOffset? DespawnTime);

public sealed record PlayerPetProjectionOptions(
	bool HasFoodFunction,
	short LovedFoodLimit = 0,
	bool HasDopingFunction = false);

public sealed record PlayerPetDopingBagProjection(IReadOnlyList<int> ItemIds)
{
	public const int MaxItems = 8;

	public int FoodItem => ItemIds.Count >= 1 ? ItemIds[0] : 0;

	public int DrinkItem => ItemIds.Count >= 2 ? ItemIds[1] : 0;

	public IReadOnlyList<int> ScrollItems => ItemIds.Count < 3 ? [] : ItemIds.Skip(2).ToArray();

	public string ToJavaPersistenceCsv()
	{
		// Java parity: model/templates/pet/PetDopingBag.getFoodItem/getDrinkItem/getScrollsUsed for PlayerPetsDAO.saveDopingBag.
		var ids = new List<int> { FoodItem, DrinkItem };
		ids.AddRange(ScrollItems);
		return string.Join(",", ids);
	}
}

public sealed record PlayerPetLoadedProjection(
	int PetObjectId,
	int TemplateId,
	int PlayerObjectId,
	int ExpireTime,
	string Name,
	int Decoration,
	DateTimeOffset? Birthday,
	DateTimeOffset DespawnTime,
	PetFeedProgress? FeedProgress,
	PetCommonDataTiming Timing,
	PlayerPetDopingBagProjection? DopingBag);

public static class PlayerPetRowProjection
{
	public static PlayerPetLoadedProjection Project(
		PlayerPetRepositoryRow row,
		PlayerPetProjectionOptions options,
		Func<DateTimeOffset> currentTime)
	{
		// Java parity: dao/PlayerPetsDAO.getPlayerPets row materialization, without live DataManager template lookup.
		var timing = new PetCommonDataTiming();
		timing.SetRefeedTime(row.ReuseTimeMillis);
		timing.SetStartMoodTime(row.MoodStartedMillis);
		timing.SetShuggleCounter(row.ShuggleCounter);
		timing.SetMoodCooldownStarted(row.MoodCooldownStartedMillis);
		timing.SetGiftCooldownStarted(row.GiftCooldownStartedMillis);

		var feedProgress = options.HasFoodFunction
			? CreateFeedProgress(row, options.LovedFoodLimit)
			: null;

		var dopingBag = options.HasDopingFunction
			? ProjectDopingBag(row.Dopings)
			: null;

		return new PlayerPetLoadedProjection(
			row.PetObjectId,
			row.TemplateId,
			row.PlayerObjectId,
			row.ExpireTime,
			row.Name,
			row.Decoration,
			row.Birthday,
			row.DespawnTime ?? currentTime(),
			feedProgress,
			timing,
			dopingBag);
	}

	public static PlayerPetDopingBagProjection ProjectDopingBag(string? dopings)
	{
		if (dopings is null)
		{
			return new PlayerPetDopingBagProjection([]);
		}

		var parts = dopings.Split(',');
		if (parts.Length > PlayerPetDopingBagProjection.MaxItems)
		{
			throw new ArgumentOutOfRangeException(nameof(dopings), parts.Length, "Java PetDopingBag has exactly 8 packet slots.");
		}

		var items = new int[parts.Length];
		for (var i = 0; i < parts.Length; i++)
		{
			items[i] = int.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
		}

		return new PlayerPetDopingBagProjection(items);
	}

	private static PetFeedProgress CreateFeedProgress(PlayerPetRepositoryRow row, short lovedFoodLimit)
	{
		var feedProgress = new PetFeedProgress(lovedFoodLimit)
		{
			HungryLevel = PetHungryLevelExtensions.FromId(row.HungryLevel),
		};
		feedProgress.SetData(row.FeedProgressData);
		return feedProgress;
	}
}
