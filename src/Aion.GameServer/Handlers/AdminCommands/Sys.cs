using System;
using System.Globalization;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Commons.Utils.Info;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Time;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>
/// Java parity: data/handlers/admincommands/Sys (lord_rex). Shows and controls the system environment.
/// info/memory branches read SystemInfo (idiomatic .NET memory/runtime banners — gameplay-faithful-infra-idiomatic),
/// shutdown/restart drive the existing GameServer/ShutdownHook delayed-shutdown path. The threadpool branch
/// renders the .NET ThreadPool global counters as the closest observable equivalent: the faithful C#
/// ThreadPoolManager is a single async scheduler with no Java ThreadPoolExecutor per-pool internals
/// (getActiveCount/getCorePoolSize/...), so that exact per-pool breakdown is gated on an unported pillar.
/// </summary>
public class Sys : AdminCommand
{
    public Sys()
        : base("sys", "Shows and controls the system environment.")
    {
        SetSyntaxInfo(
            "<info> - Shows general system information.",
            "<memory> [gc] - Shows memory usage statistics and optionally runs the garbage collector.",
            "<threadpool> - Shows thread pool manager info.",
            "<restart|shutdown> [delay] - Restarts or shuts down the server after the specified delay in seconds (default: uses delay from config).");
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length < 1)
        {
            SendInfo(player);
            return;
        }

        if ("info".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            SendInfo(player, "System information at: " + ServerTime.Now().ToString("H:mm:ss", CultureInfo.InvariantCulture));
            SendInfo(player, VersionInfo.commons.ToString(GSConfig.TIME_ZONE_ID));
            SendInfo(player, GameServer.versionInfo.ToString(GSConfig.TIME_ZONE_ID));
            SendInfo(player, SystemInfo.GetSystemInfo());
        }
        else if ("memory".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            if (paramsArr.Length > 1 && "gc".Equals(paramsArr[1], StringComparison.OrdinalIgnoreCase))
            {
                long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                GC.Collect();
                SendInfo(player, "Garbage collection took " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - time) + " ms");
            }
            SendInfo(player, SystemInfo.GetMemoryInfo());
        }
        else if ("threadpool".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            foreach (string stat in GetThreadPoolStats())
                SendInfo(player, stat.Replace("\t", ""));
        }
        else if ("shutdown".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            InitShutdown(ExitCode.NORMAL, paramsArr.Length == 1 ? null : paramsArr[1]);
        }
        else if ("restart".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            InitShutdown(ExitCode.RESTART, paramsArr.Length == 1 ? null : paramsArr[1]);
        }
        else
        {
            SendInfo(player);
        }
    }

    private void InitShutdown(int exitCode, string delay)
    {
        int delaySeconds = delay == null ? ShutdownConfig.DELAY : ParseInt(delay);
        GameServer.InitShutdown(exitCode, delaySeconds);
    }

    // The faithful C# ThreadPoolManager is a single async scheduler (no ThreadPoolExecutor per-pool internals);
    // the .NET ThreadPool global counters are the closest observable equivalent (see class remark / §D3).
    private static string[] GetThreadPoolStats()
    {
        System.Threading.ThreadPool.GetAvailableThreads(out int availWorker, out int availIo);
        System.Threading.ThreadPool.GetMaxThreads(out int maxWorker, out int maxIo);
        System.Threading.ThreadPool.GetMinThreads(out int minWorker, out int minIo);
        return new[]
        {
            "",
            "Runtime thread pool:",
            "=================================================",
            "\tThreadCount: ......... " + System.Threading.ThreadPool.ThreadCount,
            "\tPendingWorkItemCount:  " + System.Threading.ThreadPool.PendingWorkItemCount,
            "\tCompletedWorkItemCount:" + System.Threading.ThreadPool.CompletedWorkItemCount,
            "\tWorker (min/avail/max):" + minWorker + " / " + availWorker + " / " + maxWorker,
            "\tIOCP   (min/avail/max):" + minIo + " / " + availIo + " / " + maxIo,
        };
    }
}
