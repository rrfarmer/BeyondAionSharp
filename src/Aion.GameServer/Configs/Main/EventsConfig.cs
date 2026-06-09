using System.Collections.Generic;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/EventsConfig. Set&lt;String&gt;→ISet&lt;string&gt;, Set&lt;Integer&gt;→ISet&lt;int&gt;.</summary>
public static class EventsConfig
{
    /// <summary>Property key: gameserver.event.service.disabled_events</summary>
    public static ISet<string> DISABLED_EVENTS;

    /// <summary>Property key: gameserver.event.arcade.enable</summary>
    public static bool ENABLE_EVENT_ARCADE = false;

    /// <summary>Property key: gameserver.event.arcade.resume_token</summary>
    public static int ARCADE_RESUME_TOKEN = 3;

    /// <summary>Property key: gameserver.worldraid.enable</summary>
    public static bool ENABLE_WORLDRAID = true;

    /// <summary>Property key: gameserver.worldraid.use_spawn_msg</summary>
    public static bool WORLDRAID_ENABLE_SPAWNMSG = true;

    /// <summary>Property key: gameserver.event.headhunting.enable</summary>
    public static bool ENABLE_HEADHUNTING = false;

    /// <summary>Property key: gameserver.event.headhunting.maps</summary>
    public static ISet<int> HEADHUNTING_MAPS = new HashSet<int>();

    /// <summary>Property key: gameserver.event.headhunting.consolation_prize_kills</summary>
    public static int HEADHUNTING_CONSOLATION_PRIZE_KILLS = 50;

    /// <summary>Property key: gameserver.event.advent_calendar.enable</summary>
    public static bool ENABLE_ADVENT_CALENDAR = false;
}
