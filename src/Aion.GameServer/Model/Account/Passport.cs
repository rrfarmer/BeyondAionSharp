using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Event;

namespace Aion.GameServer.Model.Account;

/// <summary>
/// Java parity: model/account/Passport (ViAl, SVDNESS).
/// java.sql.Timestamp arriveDate is modeled as DateTime (truncated to whole seconds, mirroring normTs).
/// </summary>
public class Passport : IPersistable
{
	private int _id;
	private bool _rewarded;
	private DateTime _arriveDate;
	private IPersistable.PersistentState _state = IPersistable.PersistentState.NOACTION;
	private bool _fakeStamp = false;

	public Passport(int id, bool rewarded, DateTime arriveDate)
	{
		_id = id;
		_rewarded = rewarded;
		_arriveDate = NormTs(arriveDate);
	}

	public int GetId()
	{
		return _id;
	}

	public void SetId(int id)
	{
		_id = id;
	}

	public bool IsRewarded()
	{
		return _rewarded;
	}

	public void SetRewarded(bool rewarded)
	{
		_rewarded = rewarded;
	}

	public RewardStatus GetRewardStatus()
	{
		if (_fakeStamp)
		{
			return _rewarded ? RewardStatus.TAKEN : RewardStatus.UPCOMING;
		}
		return _rewarded ? RewardStatus.TAKEN : RewardStatus.AVAILABLE;
	}

	public DateTime GetArriveDate()
	{
		return _arriveDate;
	}

	public void SetArriveDate(DateTime arriveDate)
	{
		_arriveDate = arriveDate;
	}

	public IPersistable.PersistentState GetPersistentState()
	{
		return _state;
	}

	public void SetPersistentState(IPersistable.PersistentState newState)
	{
		if (this._state == IPersistable.PersistentState.NEW && newState == IPersistable.PersistentState.UPDATE_REQUIRED)
		{
			this._state = IPersistable.PersistentState.UPDATE_REQUIRED;
			return;
		}
		this._state = newState;
	}

	public bool IsFakeStamp()
	{
		return _fakeStamp;
	}

	public void SetFakeStamp(bool fakeStamp)
	{
		_fakeStamp = fakeStamp;
	}

	private static DateTime NormTs(DateTime ts)
	{
		// Java parity: Timestamp.from(ts.toInstant().truncatedTo(ChronoUnit.SECONDS)).
		return ts.AddTicks(-(ts.Ticks % TimeSpan.TicksPerSecond));
	}

	public AtreianPassport GetTemplate()
	{
		// Java parity: DataManager.ATREIAN_PASSPORT_DATA.getAtreianPassportId(id).
		return DataManager.ATREIAN_PASSPORT_DATA.GetAtreianPassportId(_id);
	}

	public enum RewardStatus
	{
		UPCOMING = 0,
		AVAILABLE = 1,
		TAKEN = 2,
		EXPIRED = 3,
	}
}

public static class PassportRewardStatusExtensions
{
	// Java parity: Passport.RewardStatus.getId().
	public static int GetId(this Passport.RewardStatus status)
	{
		return status switch
		{
			Passport.RewardStatus.UPCOMING => 0,
			Passport.RewardStatus.AVAILABLE => 1,
			Passport.RewardStatus.TAKEN => 2,
			Passport.RewardStatus.EXPIRED => 3,
			_ => 0,
		};
	}
}
