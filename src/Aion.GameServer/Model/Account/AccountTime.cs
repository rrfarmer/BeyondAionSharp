namespace Aion.GameServer.Model.Account;

/// <summary>Java parity: model/account/AccountTime (EvilSpirit). Stores account online and rest time (millis).</summary>
public class AccountTime
{
    private long accumulatedOnlineTime;
    private long accumulatedRestTime;

    public long GetAccumulatedOnlineTime()
    {
        return accumulatedOnlineTime;
    }

    public void SetAccumulatedOnlineTime(long accumulatedOnlineTime)
    {
        this.accumulatedOnlineTime = accumulatedOnlineTime;
    }

    public long GetAccumulatedRestTime()
    {
        return accumulatedRestTime;
    }

    public void SetAccumulatedRestTime(long accumulatedRestTime)
    {
        this.accumulatedRestTime = accumulatedRestTime;
    }

    /// <summary>Returns hour part rounded down (1 hr 32 min → 1).</summary>
    public int GetAccumulatedOnlineHours()
    {
        return ToHours(accumulatedOnlineTime);
    }

    /// <summary>Returns minutes part (1 hr 32 min → 32).</summary>
    public int GetAccumulatedOnlineMinutes()
    {
        return ToMinutes(accumulatedOnlineTime);
    }

    public int GetAccumulatedRestHours()
    {
        return ToHours(accumulatedRestTime);
    }

    public int GetAccumulatedRestMinutes()
    {
        return ToMinutes(accumulatedRestTime);
    }

    private static int ToHours(long millis)
    {
        return (int)(millis / 1000) / 3600;
    }

    private static int ToMinutes(long millis)
    {
        return (int)((millis / 1000) % 3600) / 60;
    }
}
