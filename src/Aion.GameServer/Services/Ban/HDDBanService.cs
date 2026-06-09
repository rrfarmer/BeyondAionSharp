using System;
using System.Collections.Generic;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Network.LoginServer.ServerPackets;

namespace Aion.GameServer.Services.Ban;

/// <summary>Java parity: services/ban/HDDBanService (ViAl). java.sql.Timestamp→DateTimeOffset; getTime()→ToUnixTimeMilliseconds; System.currentTimeMillis()→DateTimeOffset.UtcNow.ToUnixTimeMilliseconds.</summary>
public class HDDBanService
{
    /// <summary>HDD serial - ban time.</summary>
    private readonly Dictionary<string, DateTimeOffset> bannedSerials = new();

    public static HDDBanService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly HDDBanService instance = new HDDBanService();
    }

    public void AddBan(string serial, DateTimeOffset banTime)
    {
        bannedSerials[serial] = banTime;
        LoginServer.GetInstance().SendPacket(new SM_HDDBAN_CONTROL(BanAction.BAN, serial, banTime.ToUnixTimeMilliseconds()));
    }

    public void RemoveBan(string serial)
    {
        this.bannedSerials.Remove(serial);
        LoginServer.GetInstance().SendPacket(new SM_HDDBAN_CONTROL(BanAction.UNBAN, serial, 0));
    }

    public void LoadBan(string serial, long banTime)
    {
        this.bannedSerials[serial] = DateTimeOffset.FromUnixTimeMilliseconds(banTime);
    }

    public bool IsBanned(string serial)
    {
        if (!this.bannedSerials.ContainsKey(serial))
            return false;
        DateTimeOffset banTime = bannedSerials[serial];
        if (banTime.ToUnixTimeMilliseconds() > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            return true;
        else
            return false;
    }
}
