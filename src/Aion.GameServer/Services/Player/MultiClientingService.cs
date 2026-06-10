using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Player;

/// <summary>Java parity: services/player/MultiClientingService. Enforces multi-clienting restriction modes: FULL (block same IP + (MAC|HDD) already online), SAME_FACTION (per-account session tracking with faction-switch cooldown). Nested AccountSession (synchronized identifier list, last-online-per-race) + Identifiers record. ConcurrentHashMap->ConcurrentDictionary; values().removeIf->ToArray()+TryRemove; computeIfAbsent->GetOrAdd; synchronized->lock; LinkedList addFirst/removeLast/getFirst; Duration.ofMinutes(x).toMillis()->x*60000; currentTimeMillis->UtcNow; Integer->int?; stream findAny.map.orElse->LINQ FirstOrDefault. SecurityConfig nested enum / AionConnection red-tolerated.</summary>
public class MultiClientingService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(MultiClientingService));
    private static readonly ConcurrentDictionary<int, AccountSession> sessionsByAccountId = new ConcurrentDictionary<int, AccountSession>();

    public static bool TryEnterWorld(Player player, AionConnection con)
    {
        if (SecurityConfig.MULTI_CLIENTING_RESTRICTION_MODE == SecurityConfig.MultiClientingRestrictionMode.FULL && !SecurityConfig.MULTI_CLIENTING_IGNORED_MAC_ADDRESSES.Contains(con.GetMacAddress()))
        {
            string mac = con.GetMacAddress();
            string hdd = con.GetHddSerial();
            string ip = con.GetIP();
            foreach (Player onlinePlayer in World.GetInstance().GetAllPlayers())
            {
                bool sameIp = ip.Equals(onlinePlayer.GetClientConnection().GetIP());
                bool sameMac = mac.Length != 0 && mac.Equals(onlinePlayer.GetClientConnection().GetMacAddress());
                bool sameHdd = hdd.Length != 0 && hdd.Equals(onlinePlayer.GetClientConnection().GetHddSerial());
                if (sameIp && (sameMac || sameHdd))
                {
                    log.LogInformation("Blocked {Player} from logging on (multi-clienting on {Match} with {OnlinePlayer})", player, sameMac ? "MAC address " + mac : "HDD " + hdd, onlinePlayer);
                    return false;
                }
            }
        }
        else if (SecurityConfig.MULTI_CLIENTING_RESTRICTION_MODE == SecurityConfig.MultiClientingRestrictionMode.SAME_FACTION)
        {
            foreach (KeyValuePair<int, AccountSession> kv in sessionsByAccountId.ToArray())
                if (kv.Value.IsExpired())
                    sessionsByAccountId.TryRemove(kv.Key, out _);
            lock (sessionsByAccountId)
            {
                int? matchedAccountId = CheckForFactionSwitchCooldownTime(player.GetRace(), con);
                if (matchedAccountId != null)
                {
                    log.LogInformation("Blocked {Player} from logging on (faction switch cooldown from account ID {AccountId})", player, matchedAccountId);
                    return false;
                }
                AccountSession accountSession = sessionsByAccountId.GetOrAdd(player.GetAccount().GetId(), k => new AccountSession(k));
                accountSession.PutIdentifiers(con);
                accountSession.EnterWorld(player);
            }
        }
        return true;
    }

    public static void OnLeaveWorld(Player player)
    {
        AccountSession session = sessionsByAccountId.GetValueOrDefault(player.GetAccount().GetId());
        if (session != null)
            session.LeaveWorld(player);
    }

    public static int? CheckForFactionSwitchCooldownTime(Race race, AionConnection con)
    {
        if (SecurityConfig.MULTI_CLIENTING_IGNORED_MAC_ADDRESSES.Contains(con.GetMacAddress()))
            return null;
        Race oppositeRace = race == Race.ELYOS ? Race.ASMODIANS : Race.ELYOS;
        long minLastOnlineMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - SecurityConfig.MULTI_CLIENTING_FACTION_SWITCH_COOLDOWN_MINUTES * 60000L;
        return sessionsByAccountId.Values
            .Where(s => !s.IsIgnored() && s.WasPlayingOnSameIpOrMac(oppositeRace, minLastOnlineMillis, con))
            .Select(s => (int?)s.accountId)
            .FirstOrDefault();
    }

    private class AccountSession
    {
        internal readonly int accountId;
        private readonly ConcurrentDictionary<Race, long> lastCharOnlineTimeMillis = new ConcurrentDictionary<Race, long>();
        private readonly LinkedList<Identifiers> identifiers = new LinkedList<Identifiers>();

        public AccountSession(int accountId)
        {
            this.accountId = accountId;
        }

        internal bool IsIgnored()
        {
            lock (this)
            {
                return identifiers.Count != 0 && SecurityConfig.MULTI_CLIENTING_IGNORED_MAC_ADDRESSES.Contains(identifiers.First.Value.mac);
            }
        }

        internal void PutIdentifiers(AionConnection connection)
        {
            lock (this)
            {
                Identifiers ids = new Identifiers(connection.GetIP(), connection.GetMacAddress());
                if (!identifiers.Contains(ids))
                {
                    identifiers.AddFirst(ids);
                    while (identifiers.Count > 3)
                        identifiers.RemoveLast();
                }
            }
        }

        internal bool HasAny(string ip, string mac)
        {
            lock (this)
            {
                return identifiers.Any(identifier => identifier.ip.Equals(ip) || identifier.mac.Equals(mac));
            }
        }

        internal bool IsExpired()
        {
            long minLastOnline = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - SecurityConfig.MULTI_CLIENTING_FACTION_SWITCH_COOLDOWN_MINUTES * 60000L;
            return !lastCharOnlineTimeMillis.Values.Any(t => t > minLastOnline);
        }

        internal void EnterWorld(Player player)
        {
            lastCharOnlineTimeMillis[player.GetRace()] = long.MaxValue;
        }

        internal void LeaveWorld(Player player)
        {
            lastCharOnlineTimeMillis[player.GetRace()] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        internal bool WasPlayingOnSameIpOrMac(Race race, long minLastOnlineMillis, AionConnection con)
        {
            if (!lastCharOnlineTimeMillis.TryGetValue(race, out long lastOnlineMillis))
                return false;
            return lastOnlineMillis > minLastOnlineMillis && HasAny(con.GetIP(), con.GetMacAddress());
        }
    }

    private record Identifiers(string ip, string mac);
}
