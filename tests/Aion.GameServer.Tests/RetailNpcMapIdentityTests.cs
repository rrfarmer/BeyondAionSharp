using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Retail;
using Aion.GameServer.Tests.Ai;
using Spawner=Aion.GameServer.SpawnEngine.SpawnEngine;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcMapIdentityTests
{
    [SkippableFact]
    public void SyntheticPandaemoniumMembershipDoesNotClaimInggisonCoverage()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(hash),"Requires pinned selected retail package");
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(120010000).Build();
        Assert.True(RetailNpcStatCatalog.TryGet(120010000,216520,out var synthetic));
        Assert.Equal(86954218,synthetic.MaxHp);
        Assert.False(RetailNpcStatCatalog.TryGet(210050000,216520,out _));
        Assert.False(RetailNpcStatCatalog.TryGet(210050000,281451,out _));
        Npc Create(int map)
        {
            var spawn=Spawner.NewSingleTimeSpawn(map,216520,100,100,200,0,null,"sematariux");
            return new Npc(new NpcController(),spawn,DataManager.NPC_DATA.GetNpcTemplate(216520));
        }
        var pandaemonium=Create(120010000);var inggison=Create(210050000);
        Assert.IsType<SematariuxAI>(pandaemonium.GetAi());Assert.IsType<SematariuxAI>(inggison.GetAi());
        Assert.NotNull(pandaemonium.GetGameStats().StatisticsProfile);
        Assert.Null(inggison.GetGameStats().StatisticsProfile);
        Assert.Equal(20880000,pandaemonium.GetLifeStats().GetMaxHp());
        Assert.Equal(20880000,inggison.GetLifeStats().GetMaxHp());
        Assert.Empty(h.Clock.Faults);
    }
}
