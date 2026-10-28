using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Aion.GameServer.Tests;

/// <summary>Generated test values only; no copied retail table or NPC record.</summary>
internal static class SyntheticNpcRatioTable
{
    internal static byte[] Bytes() => Encoding.UTF8.GetBytes(new XElement("pc_stat_ratios",
        Enumerable.Range(1, 100).Select(i => new XElement("level", new XAttribute("level", i),
            new XElement("stat_ratio", i == 66 ? 1015 : i == 100 ? 1525 : 1000))))
        .ToString(SaveOptions.DisableFormatting));

    internal static string Sha256 => Convert.ToHexStringLower(SHA256.HashData(Bytes()));
}

/// <summary>Optional private integration data. A configured but corrupt/missing
/// fixture fails; an unconfigured external source integration test is skipped.</summary>
internal static class RetailNpcPrivateStatFixtures
{
    private const string ExpectedSha256 = "0b6b6bb102ba337b50fd55a6ac977792fa5db4287b653f794007ce34170b978e";

    internal static string ReadText(string name)
    {
        string? root = Environment.GetEnvironmentVariable("AION_RETAIL_NPC_STAT_FIXTURES");
        Skip.If(string.IsNullOrWhiteSpace(root), "Requires external pinned ordinary-stat fixtures; see docs/retail-stat-foundation.md");
        string path = Path.Combine(root!, "ordinary-stat-fixtures.json");
        Assert.InRange(new FileInfo(path).Length, 1, 2_000_000);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(ExpectedSha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var document = JsonDocument.Parse(bytes);
        return document.RootElement.GetProperty(name).GetString()!;
    }
}
