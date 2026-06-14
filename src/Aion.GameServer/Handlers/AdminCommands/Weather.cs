using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Weather (Kwazar).</summary>
public class Weather : AdminCommand
{
    public Weather()
        : base("weather", "Shows/changes the weather.")
    {
        SetSyntaxInfo(
            "<info> - Shows info for the weather in the current zone.",
            "<next> - Triggers a natural weather change on this map.",
            "<set> <code> - Changes the weather on this map, according to the weather code between 0 (default) and 12.");
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        switch (paramsArr[0].ToLower())
        {
            case "info":
                foreach (ZoneInstance regionZone in admin.FindZones())
                {
                    if (regionZone.GetZoneTemplate().GetZoneType() == ZoneClassName.WEATHER)
                    {
                        int weatherZoneId = DataManager.ZONE_DATA.GetWeatherZoneId(regionZone.GetZoneTemplate());
                        WeatherEntry weatherEntry = WeatherService.GetInstance().GetWeatherEntry(admin.GetWorldId(), weatherZoneId);
                        if (weatherEntry != null)
                        {
                            string info = "Weather for region " + regionZone.GetZoneTemplate().GetXmlName() + ":";
                            if (weatherEntry == WeatherEntry.NONE)
                            {
                                info += "\n\tcode: " + weatherEntry.GetCode() + " (no weather)";
                            }
                            else
                            {
                                if (weatherEntry.GetZoneId() > 0)
                                    info += "\n\tzone: " + weatherEntry.GetZoneId();
                                if (weatherEntry.GetWeatherName() != null)
                                    info += "\n\tname: " + weatherEntry.GetWeatherName();
                                info += "\n\tcode: " + weatherEntry.GetCode();
                            }
                            SendInfo(admin, info);
                            return;
                        }
                    }
                }
                SendInfo(admin, "No weather found for this region.");
                return;
            case "set":
            case "next":
                int weatherCode;
                if (paramsArr[0].Equals("next", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (paramsArr.Length != 1)
                    {
                        SendInfo(admin);
                        return;
                    }
                    weatherCode = -1;
                }
                else
                {
                    weatherCode = int.TryParse(paramsArr[1], out int code) ? code : -1;
                    if (weatherCode < 0 || weatherCode > 12)
                    {
                        SendInfo(admin, "Weather code must be between 0 and 12.");
                        return;
                    }
                }

                if (WeatherService.GetInstance().ChangeWeather(admin.GetWorldId(), weatherCode))
                {
                    string weatherName = WeatherService.GetInstance().FindWeatherEntry(admin).GetWeatherName();
                    SendInfo(admin, "Changed the weather" + (weatherName == null ? "." : " to " + weatherName + "."));
                }
                else
                {
                    SendInfo(admin, "This region has no weather defined.");
                }
                return;
        }
    }
}
