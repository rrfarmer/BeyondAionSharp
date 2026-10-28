using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;
using Aion.GameServer.Retail;
using Aion.GameServer.Tests.Ai;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcTribeActorSnapshotTests
{
    [SkippableFact]
    public void UserOwnedTrapUsesTheOwnersCurrentTribeOnlyInConflictWorlds()
    {
        string? manifest = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? tribe = Environment.GetEnvironmentVariable("AION_RETAIL_TRIBE_SOURCE");
        Skip.If(string.IsNullOrEmpty(manifest) || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(tribe),
            "Requires pinned retail sources");
        using var active = RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!), hash!, Path.GetDirectoryName(manifest!)!);
        var matrix = RetailNpcTribeMatrix.ReadReviewedSource(File.ReadAllBytes(tribe!));
        var profile = RetailNpcStatCatalog.Members.Single(x => x.MapId == 0 && x.Profile.NpcId == 749233).Profile;
        var unchanged = RetailNpcTribeActorSnapshot.ResolveUserOwnedTrapCreationForOfflineFixture(
            profile, "PC_Dark", matrix, false, null);
        Assert.NotNull(unchanged);
        Assert.Equal("IDYun_Polymorph", unchanged.NpcTribe);
        Assert.Equal(3, unchanged.Relation);
        var owned = RetailNpcTribeActorSnapshot.ResolveUserOwnedTrapCreationForOfflineFixture(
            profile, "PC_Dark", matrix, true, "PC");
        Assert.NotNull(owned);
        Assert.Equal("PC", owned.NpcTribe);
        Assert.Equal(1, owned.Relation);
        Assert.Equal(unchanged.SourceSha, owned.SourceSha);
        Assert.Null(RetailNpcTribeActorSnapshot.ResolveUserOwnedTrapCreationForOfflineFixture(profile, "PC_Dark", matrix, null, "PC"));
        Assert.Null(RetailNpcTribeActorSnapshot.ResolveUserOwnedTrapCreationForOfflineFixture(profile, "PC_Dark", matrix, true, null));
        Assert.Null(RetailNpcTribeActorSnapshot.ResolveUserOwnedTrapCreationForOfflineFixture(profile, "PC_Dark", matrix, true, "unreviewed"));
    }

    [SkippableFact]
    public void AllRemainingDrainBindingsResolveTheirPinnedStartupTribe()
    {
        string? manifest = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? tribe = Environment.GetEnvironmentVariable("AION_RETAIL_TRIBE_SOURCE");
        string? census = Environment.GetEnvironmentVariable("AION_RETAIL_DRAIN_TRIBE_CENSUS");
        Skip.If(string.IsNullOrEmpty(manifest) || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(tribe)
            || string.IsNullOrEmpty(census), "Requires pinned retail source and census");
        byte[] censusBytes = File.ReadAllBytes(census!);
        Assert.Equal("e82d82c06e071d223a14f91dd9082bf78db595662038b027ebca2d8f26a623cb",
            Convert.ToHexStringLower(SHA256.HashData(censusBytes)));
        using var active = RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!), hash!, Path.GetDirectoryName(manifest!)!);
        var matrix = RetailNpcTribeMatrix.ReadReviewedSource(File.ReadAllBytes(tribe!));
        using var doc = JsonDocument.Parse(censusBytes);
        var rows = doc.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(26, rows.Length);
        foreach (var row in rows)
        {
            int map = row.GetProperty("map").GetInt32(), npc = row.GetProperty("npc").GetInt32();
            var member = RetailNpcStatCatalog.Members.Single(x => x.MapId == map && x.Profile.NpcId == npc);
            var projection = RetailNpcTribeActorSnapshot.ResolveProfileSource(member.Profile, "PC_Dark", matrix);
            Assert.NotNull(projection);
            Assert.Equal(row.GetProperty("tribe").GetString(), projection.NpcTribe);
            Assert.Equal(row.GetProperty("asmodian_relation").GetInt32(), projection.Relation);
            Assert.Equal(row.GetProperty("source_record_sha256").GetString(), projection.SourceSha);
            Assert.Equal(row.GetProperty("elyos_relation").GetInt32(),
                RetailNpcTribeActorSnapshot.ResolveProfileSource(member.Profile, "PC", matrix)!.Relation);
            Assert.Null(RetailNpcTribeActorSnapshot.ResolveProfileSource(member.Profile, "other tribe", matrix));
        }
    }

    [SkippableFact]
    public void SelectedNpcAndOrdinaryPlayerUseSourceIdentityOnlyWithExplicitNativePreconditions()
    {
        string? manifest = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? tribe = Environment.GetEnvironmentVariable("AION_RETAIL_TRIBE_SOURCE");
        Skip.If(string.IsNullOrEmpty(manifest) || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(tribe),
            "Requires pinned retail sources");
        using var active = RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!), hash!, Path.GetDirectoryName(manifest!)!);
        var matrix = RetailNpcTribeMatrix.ReadReviewedSource(File.ReadAllBytes(tribe!));
        using var h = BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc = h.SpawnWithAi(214583, "retail_stat_base_fixture");
        var player = RetailNpcStatPeriodicTests.Player(h, 220050000);
        var captured = RetailNpcTribeActorSnapshot.CaptureForOfflineFixture(npc, player, matrix, true, true);
        Assert.NotNull(captured);
        Assert.Equal("AggressiveSingleMonster", captured.NpcTribe);
        Assert.Equal("PC_Dark", captured.PlayerTribe);
        Assert.Equal(0, captured.Relation);
        Assert.Equal("5b9cbb07bde2093644db166a3f3ed51a0729457f077b394de6ba8f5c0ea92600", captured.SourceSha);
        Assert.Null(RetailNpcTribeActorSnapshot.CaptureForOfflineFixture(npc, player, matrix, false, true));
        Assert.Null(RetailNpcTribeActorSnapshot.CaptureForOfflineFixture(npc, player, matrix, true, false));
        player.GetCommonData().SetRace(Race.ELYOS);
        Assert.Equal("PC", RetailNpcTribeActorSnapshot.CaptureForOfflineFixture(npc, player, matrix, true, true)!.PlayerTribe);
        npc.SetCreatorId(1234567);
        Assert.Null(RetailNpcTribeActorSnapshot.CaptureForOfflineFixture(npc, player, matrix, true, true));
    }
}
