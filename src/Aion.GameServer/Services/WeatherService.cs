using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.Commons.Utils;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time.Gametime;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/WeatherService (ATracer, Kwazar, Rolandas). Per-zone weather state/rotation. Map→Dictionary (get→GetValueOrDefault, put→indexer, entrySet→KeyValuePair); anonymous Runnable→Schedule(ct=>{...;ValueTask}); synchronized(arr)→lock(arr); stream().filter().findFirst().orElseGet→Where().FirstOrDefault() ?? ...; switch on string; GameTime.clone→Clone. WeatherEntry/WeatherTable/GameTime/SM_WEATHER red-tolerated.</summary>
public class WeatherService
{
    private readonly Dictionary<int, WeatherEntry[]> worldZoneWeathers;

    public static WeatherService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private WeatherService()
    {
        worldZoneWeathers = new Dictionary<int, WeatherEntry[]>();
        GameTime gameTime = GameTimeService.GetInstance().GetGameTime().Clone();
        foreach (WorldMapTemplate worldMapTemplate in DataManager.WORLD_MAPS_DATA)
        {
            int mapId = worldMapTemplate.GetMapId();
            WeatherTable table = DataManager.MAP_WEATHER_DATA.GetWeather(mapId);
            if (table != null)
            {
                WeatherEntry[] weatherEntries = new WeatherEntry[table.GetZoneCount()];
                worldZoneWeathers[mapId] = weatherEntries;
                SetNextWeather(mapId, weatherEntries, gameTime);
            }
        }
    }

    /// <summary>Triggered on every day time change (4x every two hours, since an in-game day equals 120 minutes).</summary>
    public void CheckWeathersTime()
    {
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            GameTime weatherTime = GameTimeService.GetInstance().GetGameTime().Clone();
            foreach (KeyValuePair<int, WeatherEntry[]> e in worldZoneWeathers)
            {
                int mapId = e.Key;
                WeatherEntry[] weatherEntries = e.Value;
                SetNextWeather(mapId, weatherEntries, weatherTime);
                PacketSendUtility.BroadcastToWorld(new SM_WEATHER(weatherEntries), p => p.IsSpawned() && p.GetWorldId() == mapId);
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(Rnd.Get(20000, 240000))); // change weather 20s to 4m after daytime change
    }

    private void SetNextWeather(int mapId, WeatherEntry[] weatherEntries, GameTime createdTime)
    {
        WeatherTable table = DataManager.MAP_WEATHER_DATA.GetWeather(mapId);
        lock (weatherEntries)
        {
            for (int zoneId = 1; zoneId <= weatherEntries.Length; zoneId++)
            {
                int index = zoneId - 1;
                weatherEntries[index] = NextWeather(weatherEntries[index], table, zoneId, createdTime);
            }
        }
    }

    private WeatherEntry NextWeather(WeatherEntry oldEntry, WeatherTable table, int zoneId, GameTime time)
    {
        WeatherEntry nextWeather = table.GetWeatherAfter(oldEntry);
        return nextWeather == null ? GetRandomWeather(time, table, zoneId) : nextWeather;
    }

    private WeatherEntry GetRandomWeather(GameTime time, WeatherTable table, int zoneId)
    {
        List<WeatherEntry> weathers = table.GetWeathersForZone(zoneId);

        int rankChance = Rnd.Get(0, 6);
        // rank 2 occurs twice often than rank 1
        // rank 1 occurs twice often than rank 0
        int rank;
        if (rankChance == 0)
            rank = 0;
        else if (rankChance <= 2)
            rank = 1;
        else
            rank = 2;

        bool canSnow = time.GetMonth() <= 3 || time.GetMonth() >= 11;
        List<WeatherEntry> possibleWeathers = new List<WeatherEntry>();
        while (rank >= 0)
        {
            foreach (WeatherEntry entry in weathers)
            {
                if (entry.GetRank() == -1)
                    return entry; // constant weather, maybe completely random ?

                if (entry.GetRank() == rank && CheckSnowCondition(canSnow, entry))
                    possibleWeathers.Add(entry);
            }
            if (possibleWeathers.Count > 0)
            {
                rank = -1;
                break;
            }
            rank--;
        }

        WeatherEntry newWeather;
        if (possibleWeathers.Count == 0)
        {
            newWeather = WeatherEntry.NONE;
        }
        else
        {
            // almost all weather types have after and before weathers, so chances to pick up are almost equal
            newWeather = Rnd.Get(possibleWeathers);
            // now find "before" weather if such exists
            if (!newWeather.IsBefore())
            {
                foreach (WeatherEntry entry in weathers)
                {
                    if (newWeather.GetWeatherName().Equals(entry.GetWeatherName()) && entry.IsBefore())
                    {
                        newWeather = entry;
                        break;
                    }
                }
            }

            // now to be or not to be -- we don't want weather present every time :P
            // rank 2 is strongest to appear, rank 0 is the weakest
            int dayTimeCorrection = 1;
            if (time.GetDayTime() == DayTime.AFTERNOON && !canSnow)
                dayTimeCorrection *= 2; // sunny days more often :)
            float chance = Rnd.Chance();
            if ((newWeather.GetRank() == 0 && chance >= 33 / dayTimeCorrection) || (newWeather.GetRank() == 1 && chance >= 50 / dayTimeCorrection)
                || (newWeather.GetRank() == 2 && chance >= 66 / dayTimeCorrection))
                newWeather = WeatherEntry.NONE;
        }
        return newWeather;
    }

    private bool CheckSnowCondition(bool canSnow, WeatherEntry entry)
    {
        if (!canSnow && entry.GetWeatherName() != null)
        {
            switch (entry.GetWeatherName())
            {
                case "SNOW":
                case "SNOW_BEACH":
                    return false; // ALTGARD_SNOW (Altgard) and SNOW_x_WZ0x (Beluslan) are always valid
            }
        }
        return true;
    }

    public void LoadWeather(Player player)
    {
        WeatherEntry[] weatherEntries = worldZoneWeathers.GetValueOrDefault(player.GetWorldId());
        if (weatherEntries != null)
            PacketSendUtility.SendPacket(player, new SM_WEATHER(weatherEntries));
    }

    /// <summary>Changes the weather to the given weather code on the specified map. -1 has a special meaning and will trigger a natural weather change.</summary>
    public bool ChangeWeather(int mapId, int weatherCode)
    {
        WeatherEntry[] weatherEntries = worldZoneWeathers.GetValueOrDefault(mapId);
        if (weatherEntries == null)
            return false;
        WeatherTable table = DataManager.MAP_WEATHER_DATA.GetWeather(mapId);
        GameTime time = GameTimeService.GetInstance().GetGameTime().Clone();
        lock (weatherEntries)
        {
            for (int zoneId = 1; zoneId <= weatherEntries.Length; zoneId++)
            {
                int index = zoneId - 1;
                if (weatherCode == -1)
                    weatherEntries[index] = NextWeather(weatherEntries[index], table, zoneId, time);
                else
                    weatherEntries[index] = GetOrCreateWeatherEntry(zoneId, weatherCode, table);
            }
        }
        PacketSendUtility.BroadcastToWorld(new SM_WEATHER(weatherEntries), p => p.IsSpawned() && p.GetWorldId() == mapId);
        return true;
    }

    private WeatherEntry GetOrCreateWeatherEntry(int zoneId, int weatherCode, WeatherTable table)
    {
        if (weatherCode == 0) // 0 means sunny aka. no weather
            return WeatherEntry.NONE;
        return table.GetZoneData().Where(w => w.GetZoneId() == zoneId && w.GetCode() == weatherCode).FirstOrDefault()
            ?? new WeatherEntry(zoneId, weatherCode);
    }

    public WeatherEntry FindWeatherEntry(Creature creature)
    {
        foreach (ZoneInstance regionZone in creature.FindZones())
        {
            if (regionZone.GetZoneTemplate().GetZoneType() == ZoneClassName.WEATHER)
            {
                int weatherZoneId = DataManager.ZONE_DATA.GetWeatherZoneId(regionZone.GetZoneTemplate());
                WeatherEntry weatherEntry = GetWeatherEntry(creature.GetWorldId(), weatherZoneId);
                if (weatherEntry != null)
                    return weatherEntry;
            }
        }
        return WeatherEntry.NONE;
    }

    public WeatherEntry GetWeatherEntry(int mapId, int weatherZoneId)
    {
        WeatherEntry[] weatherEntries = worldZoneWeathers.GetValueOrDefault(mapId);
        if (weatherEntries == null)
            return null;
        return weatherZoneId <= 0 || weatherZoneId > weatherEntries.Length ? null : weatherEntries[weatherZoneId - 1];
    }

    private static class SingletonHolder
    {
        internal static readonly WeatherService instance = new WeatherService();
    }
}
