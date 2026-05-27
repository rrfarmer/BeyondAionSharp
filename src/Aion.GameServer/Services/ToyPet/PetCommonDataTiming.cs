namespace Aion.GameServer.Services.ToyPet;

public sealed class PetCommonDataTiming
{
	private const long MoodCooldownMillis = 600_000;
	private const long GiftCooldownMillis = 3_600_000;

	public long RefeedTimeMillis { get; private set; }
	public long StartMoodTimeMillis { get; private set; }
	public int ShuggleCounter { get; private set; }
	public int LastSentPoints { get; private set; }
	public long MoodCooldownStartedMillis { get; private set; }
	public long GiftCooldownStartedMillis { get; private set; }

	public static int ToBirthdayEpochSeconds(DateTimeOffset? birthday)
	{
		// Java parity: model/gameobjects/player/PetCommonData.getBirthday.
		return birthday is null ? 0 : checked((int)(birthday.Value.ToUnixTimeMilliseconds() / 1000));
	}

	public void SetRefeedTime(long currentTimeMillis)
	{
		// Java parity: PetCommonData.setRefeedTime stores the supplied millisecond timestamp verbatim.
		RefeedTimeMillis = currentTimeMillis;
	}

	public long GetRefeedDelay(long currentTimeMillis)
	{
		// Java parity: PetCommonData.getRefeedDelay mutates expired refeed timestamps back to zero.
		var time = RefeedTimeMillis - currentTimeMillis;
		if (time >= 0)
		{
			return time;
		}

		RefeedTimeMillis = 0;
		return 0;
	}

	public void SetStartMoodTime(long startMoodTimeMillis)
	{
		StartMoodTimeMillis = startMoodTimeMillis;
	}

	public void SetShuggleCounter(int shuggleCounter)
	{
		ShuggleCounter = shuggleCounter;
	}

	public int GetMoodPoints(bool forPacket, long currentTimeMillis)
	{
		// Java parity: PetCommonData.getMoodPoints lazily initializes startMoodTime and uses Math.round(float seconds).
		if (StartMoodTimeMillis == 0)
		{
			StartMoodTimeMillis = currentTimeMillis;
		}

		var elapsedSeconds = (float)(currentTimeMillis - StartMoodTimeMillis) / 1000f;
		var points = JavaRound(elapsedSeconds) + ShuggleCounter * 1000;
		return forPacket && points > 9000 ? 9000 : points;
	}

	public void SetLastSentPoints(int points)
	{
		LastSentPoints = points;
	}

	public bool IncreaseShuggleCounter(long currentTimeMillis)
	{
		if (GetMoodRemainingTime(currentTimeMillis) > 0)
		{
			return false;
		}

		MoodCooldownStartedMillis = currentTimeMillis;
		ShuggleCounter++;
		return true;
	}

	public void ClearMoodStatistics()
	{
		StartMoodTimeMillis = 0;
		ShuggleCounter = 0;
	}

	public void SetMoodCooldownStarted(long moodCooldownStartedMillis)
	{
		MoodCooldownStartedMillis = moodCooldownStartedMillis;
	}

	public int GetMoodRemainingTime(long currentTimeMillis)
	{
		return GetRemainingCooldownSeconds(
			MoodCooldownStartedMillis,
			MoodCooldownMillis,
			currentTimeMillis,
			static timing => timing.MoodCooldownStartedMillis = 0);
	}

	public void SetGiftCooldownStarted(long giftCooldownStartedMillis)
	{
		GiftCooldownStartedMillis = giftCooldownStartedMillis;
	}

	public int GetGiftRemainingTime(long currentTimeMillis)
	{
		return GetRemainingCooldownSeconds(
			GiftCooldownStartedMillis,
			GiftCooldownMillis,
			currentTimeMillis,
			static timing => timing.GiftCooldownStartedMillis = 0);
	}

	private int GetRemainingCooldownSeconds(
		long cooldownStartedMillis,
		long cooldownMillis,
		long currentTimeMillis,
		Action<PetCommonDataTiming> clearExpired)
	{
		var stop = cooldownStartedMillis + cooldownMillis;
		var remains = stop - currentTimeMillis;
		if (remains <= 0)
		{
			clearExpired(this);
			return 0;
		}

		return (int)(remains / 1000);
	}

	private static int JavaRound(float value)
	{
		return (int)MathF.Floor(value + 0.5f);
	}
}
