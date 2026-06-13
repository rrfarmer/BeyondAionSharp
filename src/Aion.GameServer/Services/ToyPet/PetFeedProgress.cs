namespace Aion.GameServer.Services.ToyPet;

public sealed class PetFeedProgress
{
	private readonly short _lovedFoodMax;
	private int _totalPoints;
	private short _regularConsumed;
	private short _lovedConsumed;
	private bool _lovedFeeded;

	public PetFeedProgress(short lovedFoodLimit)
	{
		// Java parity: services/toypet/PetFeedProgress masks loved food limit to six packet bits.
		_lovedFoodMax = (short)(lovedFoodLimit & 0x3F);
	}

	public int TotalPoints
	{
		get => _totalPoints;
		set => _totalPoints = value & 0x3FFF;
	}

	public PetHungryLevel HungryLevel { get; set; } = PetHungryLevel.HUNGRY;

	// Java parity: getHungryLevel()/setHungryLevel()
	public PetHungryLevel GetHungryLevel() => HungryLevel;
	public void SetHungryLevel(PetHungryLevel level) => HungryLevel = level;

	public int RegularCount => _regularConsumed & 0xFF;

	public int LovedFoodRemaining => _lovedFoodMax - _lovedConsumed;

	// Java parity: services/toypet/PetFeedProgress.getLovedFoodRemaining().
	public int GetLovedFoodRemaining() => LovedFoodRemaining;

	public bool IsLovedFeeded => _lovedFeeded;

	public void SetRegularCount(short count)
	{
		_regularConsumed = count;
	}

	public void SetIsLovedFeeded()
	{
		_lovedFeeded = true;
	}

	public void IncrementCount(bool lovedFood)
	{
		if (lovedFood)
		{
			_lovedConsumed++;
		}
		else
		{
			_regularConsumed++;
		}
	}

	public void Reset()
	{
		if (_lovedFeeded)
		{
			_lovedFeeded = false;
		}
		else
		{
			_totalPoints = 0;
			_regularConsumed = 0;
		}
	}

	public int GetDataForPacket()
	{
		var value = RegularCount & 0xFF;
		value <<= 14;
		value |= _totalPoints >> 2;
		value <<= 6;
		value |= _lovedConsumed & 0x3F;
		value <<= 4;
		return value;
	}

	public void SetData(int savedData)
	{
		savedData >>= 4;
		_lovedConsumed = (short)(savedData & 0x3F);
		savedData >>= 6;
		_totalPoints = (savedData & 0x3FFF) << 2;
		savedData >>= 14;
		_regularConsumed = (short)(savedData & 0xFF);
	}
}
