using System;

namespace Aion.GameServer.Network;

/// <summary>Java parity: network/BannedMacEntry (KID). Timestamp → DateTime?.</summary>
public class BannedMacEntry
{
    private string mac, details;
    private DateTime? timeEnd;

    public BannedMacEntry(string address, long newTime)
    {
        this.mac = address;
        this.UpdateTime(newTime);
    }

    public BannedMacEntry(string address, DateTime? time, string details)
    {
        this.mac = address;
        this.timeEnd = time;
        this.details = details;
    }

    public void SetDetails(string details)
    {
        this.details = details;
    }

    public void UpdateTime(long newTime)
    {
        this.timeEnd = DateTimeOffset.FromUnixTimeMilliseconds(newTime).UtcDateTime;
    }

    public string GetMac()
    {
        return mac;
    }

    public DateTime? GetTime()
    {
        return timeEnd;
    }

    public bool IsActive()
    {
        return timeEnd != null && new DateTimeOffset(timeEnd.Value, TimeSpan.Zero).ToUnixTimeMilliseconds() > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public bool IsActiveTill(long time)
    {
        return timeEnd != null && new DateTimeOffset(timeEnd.Value, TimeSpan.Zero).ToUnixTimeMilliseconds() > time;
    }

    public string GetDetails()
    {
        return details;
    }
}
