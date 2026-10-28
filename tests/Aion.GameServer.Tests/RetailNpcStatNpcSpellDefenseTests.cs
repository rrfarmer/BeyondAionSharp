using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.Tests.Ai;
using Xunit;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatNpcSpellDefenseTests
{
    private static RetailNpcNormalScope NpcScope() => RetailNpcNormalScope.Create(
        RetailNpcNormalScope.MainBinarySha256,RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
        RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,216520,281451);

    [Theory]
    [InlineData(57,57,75000)]
    [InlineData(57,59,75000)]
    [InlineData(57,60,67500)]
    [InlineData(57,64,37500)]
    [InlineData(57,69,1)]
    [InlineData(57,70,1)]
    public void HigherLevelNpcTargetReducesSpellDamageBeforeDefense(int source,int target,int expected)
    {
        var result=RetailNpcStatNpcSpellDefense.Calculate(NpcScope(),new(75000,source,target,1000,0,0,0,0));
        Assert.Equal(expected,result.AfterLevel);
        Assert.Equal(expected,result.AfterElement);
    }

    [Fact]
    public void NpcElementFloorAndMoveAdjustedMagicDefenseFollowNativeOrder()
    {
        var result=RetailNpcStatNpcSpellDefense.Calculate(NpcScope(),new(1000,57,57,1000,100,-25,-2000,0));
        Assert.Equal(1000,result.AfterLevel);
        Assert.Equal(993,result.AfterMagicalDefense);
        Assert.Equal(-2000,result.NormalizedElement);
        Assert.Equal(1986,result.AfterElement);
        var playerScope=SkyTempleCombatInputs.Scope(216520);
        Assert.Throws<ArgumentException>(()=>RetailNpcStatNpcSpellDefense.Calculate(playerScope,new(1000,57,57,1000,0,0,0,0)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcStatNpcSpellDefense.Calculate(NpcScope(),new(1000,57,57,1000,0,0,32768,0)));
    }

    [SkippableFact]
    public void SelectedBossAndEggSupplyTheirRetailInitialDrainInputs()
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(manifest)||string.IsNullOrEmpty(hash),"Requires pinned selected retail package");
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!),hash!,Path.GetDirectoryName(manifest!)!);
        using var h=BossAiHarness.For(120010000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var caster=h.SpawnWithAi(216520,"retail_stat_base_fixture");
        var target=h.SpawnWithAi(281451,"retail_stat_base_fixture",x:981);
        Assert.True(RetailNpcStatCatalog.TryGet(caster,out var casterProfile));
        Assert.True(RetailNpcStatCatalog.TryGet(target,out var targetProfile));
        Assert.True(RetailNpcStatCatalog.TryGetPeriodic(casterProfile,18726,48,out var source));
        var initial=Assert.IsType<RetailNpcStatPeriodicCombat.InstantDrainOperands>(source.InitialDrain);
        Assert.Equal((75000,0,0,1),(initial.Value,initial.LevelRate,initial.WeaponMode,initial.ApplyBoost));
        Assert.Equal("Earth",initial.Element);
        int boost=caster.GetGameStats().GetMBoost().GetCurrent()-target.GetGameStats().GetMBResist().GetCurrent();
        Assert.Equal(0,boost);
        int baseDamage=RetailNpcStatSkillCombat.AbsoluteSpellBase(initial.Value!.Value,boost,(uint)targetProfile.StatRatio,true,2900);
        int earth=target.GetGameStats().GetResistance(StatEnum.EARTH_RESISTANCE).GetCurrent();
        int mdef=target.GetGameStats().GetMDef().GetCurrent();
        Assert.Equal((57,57,0,78),(caster.GetLevel(),target.GetLevel(),mdef,earth));
        var result=RetailNpcStatNpcSpellDefense.Calculate(NpcScope(),new(baseDamage,caster.GetLevel(),target.GetLevel(),
            (uint)casterProfile.StatRatio,mdef,0,earth,0));
        Assert.Equal(70500,result.AfterElement);
        // This is a pre-critical, pre-protection numeric witness, not a cast.
        Assert.Empty(h.Clock.Faults);
    }
}
