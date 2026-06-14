using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Aion.GameServer.Commons.Utils.Info;

/// <summary>
/// Java parity: commons/utils/info/VersionInfo (lord_rex, Neon). Holds the build/version information of the
/// assembly that defines a given type. Java reads the JAR manifest (Revision/Branch/Date) or, when run from an
/// IDE, the newest .class file's timestamp; the idiomatic C# equivalent reads the defining assembly's location,
/// last-write time, and informational/metadata attributes (gameplay-faithful / infra-idiomatic principle).
/// </summary>
public sealed class VersionInfo
{
    private string? source;
    private string? revision;
    private string? branch;
    private DateTimeOffset? buildDate;

    /// <summary>Constructs version information for the assembly that defines <paramref name="type"/>.</summary>
    public VersionInfo(Type type)
    {
        try
        {
            Assembly assembly = type.Assembly;
            string location = assembly.Location;
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
            {
                source = Path.GetFileName(location);
                buildDate = new DateTimeOffset(File.GetLastWriteTimeUtc(location), TimeSpan.Zero);
            }
            else
            {
                source = assembly.GetName().Name;
            }

            foreach (AssemblyMetadataAttribute meta in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            {
                if (string.Equals(meta.Key, "Revision", StringComparison.OrdinalIgnoreCase))
                    revision = meta.Value;
                else if (string.Equals(meta.Key, "Branch", StringComparison.OrdinalIgnoreCase))
                    branch = meta.Value;
            }

            revision ??= assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString();
        }
        catch (Exception)
        {
            // Java parity: log.error("Could not get version information", e) — best-effort, fields stay null.
        }
    }

    public string? GetSource() => source;

    public string? GetRevision() => revision;

    public string? GetBranch() => branch;

    public DateTimeOffset? GetBuildDate() => buildDate;

    /// <summary>Java parity: getBuildInfo(ZoneId).</summary>
    public string GetBuildInfo(TimeZoneInfo timeZoneId)
    {
        string buildInfo = revision == null ? "" : "revision " + revision + (branch == null ? " " : " (" + branch + ") ");
        if (buildDate != null)
        {
            DateTimeOffset local = TimeZoneInfo.ConvertTime(buildDate.Value, timeZoneId ?? TimeZoneInfo.Local);
            buildInfo += "built on " + local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
        return buildInfo;
    }

    public override string ToString() => ToString(TimeZoneInfo.Local);

    public string ToString(TimeZoneInfo timeZoneId) => (source ?? "") + " " + GetBuildInfo(timeZoneId);
}
