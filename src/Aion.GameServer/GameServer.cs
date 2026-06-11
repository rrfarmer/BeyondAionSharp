using System;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer;

/// <summary>
/// Java parity: com/aionemu/gameserver/GameServer — the static faction-ratio + shutdown-state authority every
/// consumer (SM_VERSION_CHECK, CM_CRAFT, PlayerController, AccountService) references. Only the faithful static
/// surface is ported here; the Java main()/NioServer bootstrap is an idiomatic C# concern handled by
/// GameServerHostedService (gameplay-faithful / infra-idiomatic principle). Shutdown queries delegate to the
/// DI-managed ShutdownHook via its singleton bridge; the ReentrantLock becomes a lock; integer ratio math
/// (int division widened to float) is preserved verbatim for wire/behaviour parity.
/// </summary>
public static class GameServer
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(GameServer));

    public static readonly int START_TIME_SECONDS =
        (int)new DateTimeOffset(Process.GetCurrentProcess().StartTime.ToUniversalTime(), TimeSpan.Zero).ToUnixTimeSeconds();

    private static int ELYOS_COUNT = 0;
    private static int ASMOS_COUNT = 0;
    private static float ELYOS_RATIO = 0f;
    private static float ASMOS_RATIO = 0f;
    private static readonly object lockObj = new object();

    public static bool IsShutdownScheduled()
    {
        return ShutdownHook.Instance?.IsRunning ?? false;
    }

    public static bool IsShuttingDownSoon()
    {
        ShutdownHook hook = ShutdownHook.Instance;
        return hook != null && hook.IsRunning && hook.RemainingSeconds <= 30;
    }

    public static void InitShutdown(int exitCode, int delaySeconds)
    {
        ShutdownHook.Instance?.InitShutdown(exitCode, delaySeconds);
    }

    public static void UpdateRatio(Race race, int i)
    {
        lock (lockObj)
        {
            switch (race)
            {
                case Race.ASMODIANS:
                    ASMOS_COUNT += i;
                    break;
                case Race.ELYOS:
                    ELYOS_COUNT += i;
                    break;
            }

            if ((ASMOS_COUNT <= GSConfig.RATIO_MIN_CHARACTERS_COUNT) && (ELYOS_COUNT <= GSConfig.RATIO_MIN_CHARACTERS_COUNT))
            {
                ASMOS_RATIO = ELYOS_RATIO = 50f;
            }
            else
            {
                ASMOS_RATIO = ASMOS_COUNT * 100 / (ASMOS_COUNT + ELYOS_COUNT);
                ELYOS_RATIO = ELYOS_COUNT * 100 / (ASMOS_COUNT + ELYOS_COUNT);
            }
        }

        log.LogInformation("FACTIONS RATIO UPDATED: E " + ELYOS_RATIO.ToString("F1", CultureInfo.InvariantCulture)
            + " % / A " + ASMOS_RATIO.ToString("F1", CultureInfo.InvariantCulture) + " %");
    }

    public static float GetRatiosFor(Race race)
    {
        switch (race)
        {
            case Race.ASMODIANS:
                return ASMOS_RATIO;
            case Race.ELYOS:
                return ELYOS_RATIO;
            default:
                return 0f;
        }
    }

    public static int GetCountFor(Race race)
    {
        switch (race)
        {
            case Race.ASMODIANS:
                return ASMOS_COUNT;
            case Race.ELYOS:
                return ELYOS_COUNT;
            default:
                return 0;
        }
    }
}
