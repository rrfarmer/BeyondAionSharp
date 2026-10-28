using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Aion.GameServer.Retail;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcCohortTests
{
    [Fact]
    public async Task AManifestWithoutASeparatelySelectedDigestCannotPrepareProfiles()
    {
        await Assert.ThrowsAsync<InvalidDataException>(()=>RetailNpcCohortCatalog.InitializeAsync("unreviewed.json",null));
        await Assert.ThrowsAsync<InvalidDataException>(()=>RetailNpcCohortCatalog.InitializeAsync(null,new string('a',64)));
        Assert.False(RetailNpcCohortCatalog.Enabled);
    }

    [Fact]
    public void ChangedSourceBytesCannotBeRelabeledWithTheReviewedHash()
    {
        byte[] original="reviewed source"u8.ToArray();
        string pin=Convert.ToHexStringLower(SHA256.HashData(original));
        RetailNpcCohortCatalog.RequireHash(original,pin,"fixture");
        Assert.Throws<InvalidDataException>(()=>RetailNpcCohortCatalog.RequireHash("changed source"u8,pin,"fixture"));
    }

    [SkippableFact]
    public void CohortMembershipUsesMapAndNpcTogetherAndRestoresItsPublicationScope()
    {
        var f=Fixture();
        Assert.False(RetailNpcCohortCatalog.Enabled);
        using(RetailNpcCohortCatalog.InstallForTests(f.Bytes,f.Hash,f.Directory))
        {
            var profiles=RetailNpcCohortCatalog.GetActiveProfiles();Assert.NotEmpty(profiles);
            foreach(var p in profiles)
            {
                Assert.True(SkyTempleNpcProfileCatalog.TryGet(p.Cohort!.MapId,p.NpcId,out var selected));
                Assert.Same(p,selected);
                Assert.False(SkyTempleNpcProfileCatalog.TryGet(0,p.NpcId,out _));
                Assert.False(SkyTempleNpcProfileCatalog.TryGet(p.Cohort.MapId,1,out _));
            }
        }
        Assert.False(RetailNpcCohortCatalog.Enabled);
    }

    [SkippableFact]
    public void DuplicateMembersAndOverlappingCohortsAreRejectedBeforePublication()
    {
        var f=Fixture();var root=JsonNode.Parse(f.Bytes)!;
        var members=root["cohorts"]![0]!["members"]!.AsArray();members.Add(members[0]!.DeepClone());
        AssertInvalid(root);
        root=JsonNode.Parse(f.Bytes)!;
        var cohorts=root["cohorts"]!.AsArray();var copy=cohorts[0]!.DeepClone();copy["id"]="overlapping-fixture";cohorts.Add(copy);
        AssertInvalid(root);
        void AssertInvalid(JsonNode changed)
        {
            byte[] bytes=System.Text.Encoding.UTF8.GetBytes(changed.ToJsonString());
            Assert.Throws<InvalidDataException>(()=>RetailNpcCohortCatalog.InstallForTests(bytes,
                Convert.ToHexStringLower(SHA256.HashData(bytes)),f.Directory));
            Assert.False(RetailNpcCohortCatalog.Enabled);
        }
    }

    [SkippableFact]
    public void AnUnverifiedNonPlayerTargetCannotReachLegacyZeroAttackStats()
    {
        var f=Fixture();using var scope=RetailNpcCohortCatalog.InstallForTests(f.Bytes,f.Hash,f.Directory);
        using var h=BossAiHarness.For(220010000).Build();
        var source=h.SpawnWithAi(210649,"aggressive",100,100,200);
        var unmatched=h.SpawnWithAi(210364,"aggressive",101,100,200);
        Assert.True(Aion.GameServer.Controllers.Attack.RetailNpc.SkyTempleNpcCombat.RejectUnverifiedTarget(source,unmatched));
        Assert.False(Aion.GameServer.Controllers.Attack.RetailNpc.SkyTempleNpcCombat.RejectUnverifiedTarget(unmatched,source));
        Assert.False(Aion.GameServer.Controllers.Attack.RetailNpc.SkyTempleNpcCombat.RejectUnverifiedTarget(source,null));
    }

    [SkippableFact]
    public void EveryAdmittedProfileMaterializesItsOwnSourceZerosAndCalculatedCombatStats()
    {
        var f=Fixture();using var scope=RetailNpcCohortCatalog.InstallForTests(f.Bytes,f.Hash,f.Directory);
        foreach(var group in RetailNpcCohortCatalog.GetActiveProfiles().GroupBy(p=>p.Cohort!.MapId))
        {
            using var h=BossAiHarness.For(group.Key).Build();
            foreach(var p in group)
            {
                var npc=h.SpawnWithAi(p.NpcId,p.Cohort!.ExpectedHostAi,100,100,200);
                Assert.Equal(p.Level,(int)npc.GetLevel());
                Assert.Equal(p.MaxHp,npc.GetLifeStats().GetMaxHp());Assert.Equal(p.MaxHp,npc.GetLifeStats().GetCurrentHp());
                var stats=npc.GetGameStats();
                Assert.Equal(p.PhysicalDefense,stats.GetPDef().GetCurrent());
                Assert.Equal(p.MagicalDefense,stats.GetMDef().GetCurrent());
                Assert.Equal(p.MagicalResist,stats.GetMResist().GetCurrent());
                Assert.Equal(p.PhysicalAccuracy,stats.GetMainHandPAccuracy().GetCurrent());
                Assert.Equal(p.Dodge,stats.GetEvasion().GetCurrent());
                Assert.Equal(p.HpRegen,stats.GetHpRegenRate().GetCurrent());
                Assert.Equal(0,p.CreateStatsTemplate().GetPower());
                Assert.Equal(0,p.MagicalDefense);
            }
        }
        Assert.True(SkyTempleNpcProfileCatalog.TryGet(220010000,210649,out var sparkie));
        Assert.Equal((199,22,33,80,136,113),(sparkie.MaxHp,sparkie.MinDamage,sparkie.MaxDamage,
            sparkie.PhysicalDefense,sparkie.Dodge,sparkie.MagicalResist));
        Assert.True(SkyTempleNpcProfileCatalog.TryGet(220010000,210727,out var fighter));
        Assert.Equal(100,Assert.Single(fighter.Attacks).Weight); // Authored100, not the pilot's1000.
    }

    private static (byte[] Bytes,string Hash,string Directory) Fixture()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_COHORT_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_COHORT_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(hash),"Requires the reviewed external cohort manifest and separate digest");
        return(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(Path.GetFullPath(path!))!);
    }
}
