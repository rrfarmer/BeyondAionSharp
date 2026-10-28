using System.Text.Json;
using System.Security.Cryptography;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcTribeExplicitRelationsTests
{
    [SkippableFact]
    public void ReviewedRetailEntriesBindTheQueenToAsmodiansWithoutAssumingDefaults()
    {
        string? tribePath = Environment.GetEnvironmentVariable("AION_RETAIL_TRIBE_SOURCE");
        string? manifestPath = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(tribePath) || string.IsNullOrEmpty(manifestPath), "Requires pinned retail sources");
        byte[] raw = File.ReadAllBytes(tribePath!);
        var relations = RetailNpcTribeExplicitRelations.ReadReviewedSource(raw);
        byte[] changed = (byte[])raw.Clone(); changed[0] ^= 1;
        Assert.Throws<InvalidDataException>(() => RetailNpcTribeExplicitRelations.ReadReviewedSource(changed));

        byte[] manifestBytes = File.ReadAllBytes(manifestPath!);
        Assert.Equal("28013685a95b0394b312c1541a9453d8121ac3109dff67e629c0be1b0f645c58",
            Convert.ToHexStringLower(SHA256.HashData(manifestBytes)));
        using var manifest = JsonDocument.Parse(manifestBytes);
        var cohort = manifest.RootElement.GetProperty("cohorts").EnumerateArray()
            .Single(x => x.GetProperty("map_id").GetInt32() == 220050000
                && x.GetProperty("members").EnumerateArray().Any(m => m.GetProperty("npc_id").GetInt32() == 214583));
        var artifact = cohort.GetProperty("profile_artifact");
        byte[] profileBytes = File.ReadAllBytes(artifact.GetProperty("path").GetString()!);
        Assert.Equal(artifact.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(profileBytes)));
        using var profile = JsonDocument.Parse(profileBytes);
        var queen = profile.RootElement.GetProperty("profiles").EnumerateArray()
            .Single(x => x.GetProperty("npc_id").GetInt32() == 214583);
        string tribe = queen.GetProperty("scalar_fields").GetProperty("tribe").GetProperty("raw_values")[0].GetString()!;
        Assert.Equal("AggressiveSingleMonster", tribe);
        Assert.Equal(0, relations.FindExplicit(tribe, "PC_Dark"));
        Assert.Equal(0, relations.FindExplicit(tribe.ToLowerInvariant(), "pc_dark"));
        Assert.Equal(5, relations.FindExplicit(tribe, "F6_Invasion_Da"));
        Assert.Null(relations.FindExplicit(tribe, "Pet_Dark"));
        Assert.Null(relations.FindExplicit("PC_Dark", tribe));
        Assert.Throws<ArgumentException>(() => relations.FindExplicit("", "PC_Dark"));

        var skill = manifest.RootElement.GetProperty("skill_sources").EnumerateArray()
            .Single(x => x.GetProperty("id").GetInt32() == 18460);
        var geometry = RetailNpcVampireFireballGeometry.ReadReviewedSource(File.ReadAllBytes(skill.GetProperty("path").GetString()!));
        Assert.Equal(RetailNpcVampireFireballGeometry.EnemyRestrictionResult.EligibleForFurtherChecks,
            geometry.EvaluateOwnerlessNpcEnemyRestriction(100, 101, 2, 0, relations.FindExplicit(tribe, "PC_Dark")));
        Assert.Equal(RetailNpcVampireFireballGeometry.EnemyRestrictionResult.Unresolved,
            geometry.EvaluateOwnerlessNpcEnemyRestriction(100, 101, 2, 0, relations.FindExplicit(tribe, "Pet_Dark")));
    }

    [SkippableFact]
    public void UsaFallbackRequiresTheReviewedCountrySourceAndAbsenceOfAnOverride()
    {
        string? configPath = Environment.GetEnvironmentVariable("AION_RETAIL_MAIN_CONFIG");
        Skip.If(string.IsNullOrEmpty(configPath), "Requires supplied 5.8 main-server config");
        var config = XDocument.Load(configPath!).Root!;
        string map = config.Element("mapDir")!.Value;
        int country = int.Parse(config.Element("country")!.Value);
        Assert.Equal(1, country);
        var selected = RetailNpcTribeExplicitRelations.ReadReviewedUsaFallback(map, country);
        Assert.Equal(0, selected.FindExplicit("AggressiveSingleMonster", "PC_Dark"));
        Assert.Equal(0, RetailNpcTribeMatrix.ReadReviewedUsaFallback(map, country).Find("AggressiveSingleMonster", "PC_Dark"));
        Assert.Throws<InvalidDataException>(() => RetailNpcTribeExplicitRelations.ReadReviewedUsaFallback(map, 2));

        // This directory belongs exclusively to this test, never to a server.
        string temp = Path.Combine(Path.GetTempPath(), "aion-tribe-review-" + Guid.NewGuid().ToString("N"));
        try
        {
            string xml = Path.Combine(temp, "XML");
            Directory.CreateDirectory(Path.Combine(xml, "USA"));
            File.Copy(Path.Combine(map, "XML", "npc_tribe_relation.xml"), Path.Combine(xml, "npc_tribe_relation.xml"));
            Assert.Equal(0, RetailNpcTribeExplicitRelations.ReadReviewedUsaFallback(temp, 1)
                .FindExplicit("AggressiveSingleMonster", "PC_Dark"));
            File.WriteAllText(Path.Combine(xml, "USA", "npc_tribe_relation.xml"), "unreviewed");
            Assert.Throws<InvalidDataException>(() => RetailNpcTribeExplicitRelations.ReadReviewedUsaFallback(temp, 1));
            Assert.Throws<InvalidDataException>(() => RetailNpcTribeMatrix.ReadReviewedUsaFallback(temp, 1));
        }
        finally
        {
            string xml = Path.Combine(temp, "XML");
            string usa = Path.Combine(xml, "USA");
            File.Delete(Path.Combine(usa, "npc_tribe_relation.xml"));
            File.Delete(Path.Combine(xml, "npc_tribe_relation.xml"));
            if (Directory.Exists(usa)) Directory.Delete(usa);
            if (Directory.Exists(xml)) Directory.Delete(xml);
            if (Directory.Exists(temp)) Directory.Delete(temp);
        }
    }
}
