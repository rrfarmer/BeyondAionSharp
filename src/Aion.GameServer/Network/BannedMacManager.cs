using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Network;

/// <summary>Java parity: network/BannedMacManager (KID).</summary>
public class BannedMacManager
{
    private static readonly BannedMacManager manager = new BannedMacManager();
    private readonly ILogger log = NullLogger.Instance;
    private readonly Dictionary<string, BannedMacEntry> bannedList = new Dictionary<string, BannedMacEntry>();

    public static BannedMacManager GetInstance()
    {
        return manager;
    }

    public void BanAddress(string address, long newTime, string details)
    {
        BannedMacEntry entry;
        if (bannedList.ContainsKey(address))
        {
            if (bannedList[address].IsActiveTill(newTime))
            {
                return;
            }
            else
            {
                entry = bannedList[address];
                entry.UpdateTime(newTime);
            }
        }
        else
            entry = new BannedMacEntry(address, newTime);

        entry.SetDetails(details);

        bannedList[address] = entry;

        log.LogInformation("banned " + address + " to " + entry.GetTime().ToString() + " for " + details);
        global::Aion.GameServer.Network.LoginServer.LoginServer.GetInstance().SendPacket(new global::Aion.GameServer.Network.LoginServer.ServerPackets.SmMacbanControl((byte)1, address, newTime, details));
    }

    public bool UnbanAddress(string address, string details)
    {
        if (bannedList.Remove(address, out BannedMacEntry bannedMacEntry) && bannedMacEntry != null)
        {
            log.LogInformation("unbanned " + address + " for " + details);
            global::Aion.GameServer.Network.LoginServer.LoginServer.GetInstance().SendPacket(new global::Aion.GameServer.Network.LoginServer.ServerPackets.SmMacbanControl((byte)0, address, 0, details));
            return true;
        }
        else
            return false;
    }

    public bool IsBanned(string address)
    {
        BannedMacEntry bannedMacEntry = bannedList.TryGetValue(address, out BannedMacEntry e) ? e : null;
        return bannedMacEntry != null && bannedMacEntry.IsActive();
    }

    public void DbLoad(string address, long time, string details)
    {
        bannedList[address] = new BannedMacEntry(address, DateTimeOffset.FromUnixTimeMilliseconds(time).UtcDateTime, details);
    }

    public void OnEnd()
    {
        log.LogInformation("Loaded " + bannedList.Count + " banned mac addresses");
    }
}
