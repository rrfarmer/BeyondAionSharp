using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatAuditTests
{
    [SkippableFact]
    public void StatAuditUsesTheCompleteProfileSetWithoutPublishingOrReadingOptionalPlayerState()
    {
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat: true, includeBuffs: true);
        var baseline = RetailNpcStatCatalog.ReadStatPackageForAudit(bytes, pin, directory);
        var root = JsonNode.Parse(bytes)!.AsObject();
        root["spell_condition_context"] = new JsonObject { ["path"] = "does-not-exist", ["sha256"] = "invalid" };
        byte[] altered = System.Text.Encoding.UTF8.GetBytes(root.ToJsonString());
        var snapshot = RetailNpcStatCatalog.ReadStatPackageForAudit(altered, Hash(altered), directory);
        Assert.False(RetailNpcStatCatalog.Enabled);
        Assert.Empty(RetailNpcStatCatalog.Members);
        Assert.Equal(11517, snapshot.Members.Length);
        Assert.Equal(118, snapshot.Maps.Length);
        Assert.Equal(baseline.Members.Select(m => (m.MapId, m.Profile.NpcId, m.Profile.MaxHp)),
            snapshot.Members.Select(m => (m.MapId, m.Profile.NpcId, m.Profile.MaxHp)));
        Assert.Equal(baseline.SkillBindings.ToArray(), snapshot.SkillBindings.ToArray());
        Assert.NotEmpty(snapshot.BuffSources);
        Assert.Contains(snapshot.SkillBindings, b => b.SourceBound);
        var identities = snapshot.Members.Select(m => (m.MapId, m.Profile.NpcId)).ToHashSet();
        Assert.All(snapshot.SkillBindings, b => Assert.Contains((b.MapId, b.NpcId), identities));
    }

    [SkippableTheory]
    [InlineData("manifest")]
    [InlineData("combat_stats")]
    [InlineData("skill_sources")]
    public void StatAuditRejectsTamperedRequiredEvidence(string fault)
    {
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat: true, includeBuffs: true);
        var root = JsonNode.Parse(bytes)!.AsObject();
        if (fault == "manifest") pin = new string('a', 64);
        else
        {
            var asset = fault == "combat_stats" ? root[fault]! : root[fault]!.AsArray()[0]!;
            asset["sha256"] = new string('a', 64);
            bytes = System.Text.Encoding.UTF8.GetBytes(root.ToJsonString()); pin = Hash(bytes);
        }
        Assert.Throws<InvalidDataException>(() => RetailNpcStatCatalog.ReadStatPackageForAudit(bytes, pin, directory));
        Assert.False(RetailNpcStatCatalog.Enabled);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
