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
	PetDopingBag? DopingBag);

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

	public static PetDopingBag ProjectDopingBag(string? dopings)
	{
		var bag = new PetDopingBag();
		if (dopings is null)
		{
			return bag;
		}

		var parts = dopings.Split(',');
		if (parts.Length > PetDopingBag.MaxItems)
		{
			throw new ArgumentOutOfRangeException(nameof(dopings), parts.Length, "Java PetDopingBag has exactly 8 packet slots.");
		}

		for (var i = 0; i < parts.Length; i++)
		{
			// Java parity: dao/PlayerPetsDAO.getPlayerPets calls PetDopingBag.setItem(Integer.parseInt(ids[i]), i).
			bag.SetItem(int.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture), i);
		}

		return bag;
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
