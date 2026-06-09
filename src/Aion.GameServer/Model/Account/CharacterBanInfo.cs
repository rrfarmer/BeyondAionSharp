namespace Aion.GameServer.Model.Account;

/// <summary>Java parity: model/account/CharacterBanInfo.</summary>
public class CharacterBanInfo
{
    private readonly long start;
    private readonly long end;
    private readonly string reason;

    public CharacterBanInfo(long start, long duration, string reason)
    {
        this.start = start;
        this.end = duration + start;
        this.reason = reason;
    }

    public long GetStart()
    {
        return start;
    }

    public long GetEnd()
    {
        return end;
    }

    public string GetReason()
    {
        return reason;
    }
}
