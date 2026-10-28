using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcStatProfilePackageTests
{
    [Theory]
    [InlineData("digest")]
    [InlineData("mode")]
    [InlineData("region")]
    [InlineData("dataset")]
    [InlineData("maps")]
    [InlineData("empty")]
    [InlineData("duplicate-json")]
    public void IncompleteOrWrongSourceCannotReturnAPartialProfilePackage(string fault)
    {
        var root = new JsonObject
        {
            ["version"] = 1, ["mode"] = "statistics-only-v1", ["region"] = "USA",
            ["dataset_id"] = RetailNpcStatCatalog.SourceDatasetId,
            ["output_manifest_sha256"] = RetailNpcStatCatalog.SourceManifestSha256,
            ["supported_maps"] = new JsonArray(220010000), ["cohorts"] = new JsonArray()
        };
        switch (fault)
        {
            case "mode": root["mode"] = "other-package"; break;
            case "region": root["region"] = "Europe"; break;
            case "dataset": root["dataset_id"] = new string('a', 64); break;
            case "maps": root["supported_maps"] = new JsonArray(0); break;
        }
        string text = root.ToJsonString();
        if (fault == "duplicate-json") text = text.Replace("\"version\":1", "\"version\":1,\"version\":1");
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        string pin = fault == "digest" ? new string('a', 64) : Hash(bytes);
        Assert.Throws<InvalidDataException>(() => RetailNpcStatCatalog.ReadProfileSourceForAudit(bytes, pin,
            Path.GetTempPath()));
    }

    [SkippableFact]
    public void CompleteSelectedProfilePackageMatchesThePinnedEarlierFullParserExport()
    {
        string? manifest = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? pin = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? expectedPath = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_PROFILE_EXPECTED");
        string? expectedPin = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_PROFILE_EXPECTED_SHA256");
        Skip.If(new[] { manifest, pin, expectedPath, expectedPin }.Any(string.IsNullOrWhiteSpace),
            "Requires the selected package and pinned pre-refactor export; see external implementation status");
        byte[] expectedBytes = File.ReadAllBytes(expectedPath!);
        Assert.Equal(expectedPin, Hash(expectedBytes));
        using var expected = JsonDocument.Parse(expectedBytes);
        Assert.Equal(pin, expected.RootElement.GetProperty("manifest_sha256").GetString());
        var snapshot = RetailNpcStatCatalog.ReadProfileSourceForAudit(File.ReadAllBytes(manifest!), pin!,
            Path.GetDirectoryName(Path.GetFullPath(manifest!))!);
        var rows = expected.RootElement.GetProperty("rows").EnumerateArray().ToDictionary(
            r => (r.GetProperty("map_id").GetInt32(), r.GetProperty("npc_id").GetInt32()));
        Assert.Equal(rows.Count, snapshot.Members.Length);
        Assert.Equal(expected.RootElement.GetProperty("supported_maps").EnumerateArray().Select(m => m.GetInt32()), snapshot.Maps);
        var compatibility = new StatsTemplate { MaxMp = 777, Attack = 444, Matk = 555,
            Speeds = new() { WalkSpeed = 6, RunSpeed = 9 } };
        foreach (var member in snapshot.Members)
        {
            var p = member.Profile;
            var row = rows[(member.MapId, p.NpcId)];
            var fields = row.GetProperty("fields").EnumerateArray().ToDictionary(
                f => f.GetProperty("field").GetString()!, f => f.GetProperty("reference58").GetInt32());
            Assert.Equal(row.GetProperty("host_template_sha256").GetString(), member.HostTemplateSha256);
            Assert.Equal(row.GetProperty("source").GetProperty("raw_record_sha256").GetString(), p.SourceRecordSha256);
            Assert.Equal(row.GetProperty("normal_min_damage").GetInt32(), p.MinDamage);
            Assert.Equal(row.GetProperty("normal_max_damage").GetInt32(), p.MaxDamage);
            Assert.Equal(row.GetProperty("normal_attack_correction_supported").GetBoolean(), p.HasNormalAttackCorrection);
            Assert.Equal(p.Values.Count - 1, fields.Count);
            Assert.DoesNotContain("hp_regen", fields.Keys); // Regeneration is outside this audit's scope.
            foreach (var (field, value) in fields) Assert.Equal(value, p.Values[field]);
            var attacks = row.GetProperty("ordered_attacks").EnumerateArray().ToArray();
            Assert.Equal(attacks.Length, p.Attacks.Length);
            for (int i = 0; i < attacks.Length; i++)
            {
                Assert.Equal(attacks[i].GetProperty("ordinal").GetInt32(), p.Attacks[i].Ordinal);
                Assert.Equal(attacks[i].GetProperty("type").GetString(), p.Attacks[i].Type);
                Assert.Equal(attacks[i].GetProperty("range").GetSingle(), p.Attacks[i].Range);
                Assert.Equal(attacks[i].GetProperty("weight").GetInt32(), p.Attacks[i].Weight);
                Assert.Equal(attacks[i].GetProperty("modifier").GetInt32(), p.Attacks[i].Modifier);
            }
            var stats = p.CreateStatsTemplate(compatibility);
            Assert.Equal(fields["max_hp"], stats.GetMaxHp());
            Assert.Equal(fields["physical_defend"], stats.GetPdef());
            Assert.Equal(fields["magical_defend"], stats.GetMdef());
            Assert.Equal(fields["magical_resist"], stats.GetMresist());
            Assert.Equal(fields["hit_accuracy"], stats.GetAccuracy());
            Assert.Equal(fields["magical_hit_accuracy"], stats.GetMacc());
            Assert.Equal(fields["critical"], stats.GetPcrit());
            Assert.Equal(fields["magical_critical"], stats.GetMcrit());
            Assert.Equal(777, stats.GetMaxMp()); Assert.Equal(444, stats.GetAttack());
            Assert.Equal(555, stats.GetMagicalAttack()); Assert.Same(compatibility.Speeds, stats.Speeds);
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
