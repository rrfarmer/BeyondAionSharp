using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Aion.GameServer.Commons.Utils.Info;

/// <summary>
/// Java parity: commons/utils/info/SystemInfo (lord_rex, Neon). Renders memory + runtime/OS banners for the
/// //sys admin command. The original reads JVM Runtime memory and System properties; the C# substrate has no
/// JVM, so the equivalent observable metrics come from the idiomatic .NET surface (GC/Process memory,
/// RuntimeInformation/Environment) preserving the line shapes/labels as closely as the data allows
/// (gameplay-faithful-infra-idiomatic). "Max. memory allowed" maps to the configured GC heap hard limit (or the
/// process working-set / physical-equivalent when unbounded); "Allocated" to the managed heap committed bytes;
/// "Used" to the live managed heap. The JVM-specific line becomes the CLR/runtime banner.
/// </summary>
public static class SystemInfo
{
    public static string[] GetMemoryInfo()
    {
        const long mbDivisor = 1024L * 1024L;
        GCMemoryInfo gcInfo = GC.GetGCMemoryInfo();
        // Max allowed: the GC heap hard limit if configured (>0), else the total available physical-equivalent.
        double max = (gcInfo.TotalAvailableMemoryBytes > 0 ? gcInfo.TotalAvailableMemoryBytes : gcInfo.HighMemoryLoadThresholdBytes) / (double)mbDivisor;
        double allocated = gcInfo.HeapSizeBytes / (double)mbDivisor; // committed managed heap
        double used = GC.GetTotalMemory(false) / (double)mbDivisor;  // live managed heap
        if (max <= 0)
            max = allocated;

        string MbFmt(double mib) => mib.ToString("#,##0", CultureInfo.InvariantCulture) + " MiB";
        string PercentFmt(double ratio) => "(" + (ratio * 100).ToString("0", CultureInfo.InvariantCulture) + " %)";

        return new[]
        {
            "Max. memory allowed: " + string.Format("{0,9}", MbFmt(max)),
            "├ Allocated memory:  " + string.Format("{0,9}", MbFmt(allocated)) + string.Format("{0,8}", PercentFmt(max == 0 ? 0 : allocated / max)),
            "└ Used memory:       " + string.Format("{0,9}", MbFmt(used)) + string.Format("{0,8}", PercentFmt(max == 0 ? 0 : used / max)),
        };
    }

    public static string[] GetSystemInfo()
    {
        int availableCPUs = Environment.ProcessorCount;
        string totalCPUsEnv = Environment.GetEnvironmentVariable("NUMBER_OF_PROCESSORS") ?? "";
        string totalCPUs = totalCPUsEnv == "" || totalCPUsEnv == availableCPUs.ToString(CultureInfo.InvariantCulture)
            ? ""
            : string.Format("{0,12}", " (of " + totalCPUsEnv + ')');

        return new[]
        {
            "OS:  " + RuntimeInformation.OSDescription + " (" + RuntimeInformation.OSArchitecture + ')',
            "Runtime: " + RuntimeInformation.FrameworkDescription,
            "Runtime available CPUs: " + string.Format("{0,6}", availableCPUs) + totalCPUs,
        };
    }
}
