using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerOwnedPet(
	int ObjectId,
	int TemplateId,
	string Name,
	int Decoration,
	int MasterObjectId = 0,
	DateTimeOffset? Birthday = null,
	int ExpireTimeSeconds = 0,
	int FeedProgressData = 0,
	long RefeedTimeMillis = 0,
	IReadOnlyList<int>? DopingItemIds = null,
	bool CancelFeed = false,
	bool IsLooting = false,
	bool IsSelling = false,
	PetHungryLevel HungryLevel = PetHungryLevel.Hungry)
{
	public int BirthdayEpochSeconds => Birthday is null ? 0 : checked((int)(Birthday.Value.ToUnixTimeMilliseconds() / 1000));

	public int SecondsUntilExpiration(DateTimeOffset currentTime) =>
		ExpireTimeSeconds == 0 ? 0 : ExpireTimeSeconds - (int)currentTime.ToUnixTimeSeconds();

	public int RefeedDelaySeconds(DateTimeOffset currentTime)
	{
		if (RefeedTimeMillis <= 0)
			return 0;

		var delayMillis = RefeedTimeMillis - currentTime.ToUnixTimeMilliseconds();
		return delayMillis <= 0 ? 0 : checked((int)(delayMillis / 1000));
	}
}
