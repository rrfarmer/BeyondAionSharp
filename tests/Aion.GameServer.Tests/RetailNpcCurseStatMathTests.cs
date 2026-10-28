using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils.Stats;
using System.Security.Cryptography;
using System.Text.Json;
using Resource = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcCurseStatMath.Resource;

namespace Aion.GameServer.Tests;

public class RetailNpcCurseStatMathTests
{
    [Fact]
    public void ReviewedCombinedPrimaryModifiersReachRetailResourceBases()
    {
        // The reviewed retail skills 9839 and 9942 supply KNOWIL +5 and
        // ALLPARA -3 at level one; explicit fixture inputs do not assert they
        // are active on a particular player.
        var mods=new RetailNpcCurseStatMath.PrimaryModifier[]
        {
            new(RetailNpcCurseStatMath.PrimaryAttribute.KnowledgeWill,0,5),
            new(RetailNpcCurseStatMath.PrimaryAttribute.All,0,-3)
        };
        var actual=RetailNpcCurseStatMath.OrdinaryPrimaries(115,90,mods);
        Assert.Equal(((ushort)112,(ushort)92),actual);
        Assert.NotEqual(RetailNpcCurseStatMath.InitialBase(Resource.Hp,65,115,1),
            RetailNpcCurseStatMath.InitialBase(Resource.Hp,65,actual.Health,1));
        Assert.NotEqual(RetailNpcCurseStatMath.InitialBase(Resource.Mp,65,90,1),
            RetailNpcCurseStatMath.InitialBase(Resource.Mp,65,actual.Will,1));
    }

    [Fact]
    public void PrimaryFanoutAggregatesChannelsAndRejectsUnknownAttributes()
    {
        var modifiers=new RetailNpcCurseStatMath.PrimaryModifier[]
        {
            new(RetailNpcCurseStatMath.PrimaryAttribute.StrengthHealth,10,2),
            new(RetailNpcCurseStatMath.PrimaryAttribute.Health,-3,-5),
            new(RetailNpcCurseStatMath.PrimaryAttribute.KnowledgeWill,-7,5),
            new(RetailNpcCurseStatMath.PrimaryAttribute.Will,3,-1),
            new(RetailNpcCurseStatMath.PrimaryAttribute.All,1,-3)
        };
        Assert.Equal(((ushort)118,(ushort)89),RetailNpcCurseStatMath.OrdinaryPrimaries(115,90,modifiers));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcCurseStatMath.OrdinaryPrimaries(100,100,
            [new((RetailNpcCurseStatMath.PrimaryAttribute)99,0,0)]));
    }

    [SkippableFact]
    public void OrdinaryPrimaryMatchesRetailSignedPercentageAndClamp()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_PLAYER_PRIMARY_MODIFIERS_FIXTURE");
        Skip.If(string.IsNullOrEmpty(path),"Requires the pinned retail ordinary-primary proof");
        byte[] bytes=File.ReadAllBytes(path!);
        Assert.Equal("fa2af7c540e5ce6b9aa419347611e4b0882d2479624db1930a9fb5210b64b5f2",Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var proof=JsonDocument.Parse(bytes);var rows=proof.RootElement.GetProperty("cases");Assert.Equal(100,rows.GetArrayLength());
        foreach(var row in rows.EnumerateArray())
            Assert.Equal(row.GetProperty("result").GetUInt16(),RetailNpcCurseStatMath.OrdinaryPrimary(
                row.GetProperty("initial").GetUInt16(),row.GetProperty("percent").GetInt32(),row.GetProperty("flat").GetInt32()));
        Assert.Equal((ushort)89,RetailNpcCurseStatMath.OrdinaryPrimary(95,-7,0));
        Assert.Equal((ushort)999,RetailNpcCurseStatMath.OrdinaryPrimary(115,int.MaxValue,int.MaxValue));
        Assert.Equal((ushort)80,RetailNpcCurseStatMath.OrdinaryPrimary(115,int.MinValue,int.MinValue));
    }
    [SkippableFact]
    public void InitialPrimaryMatchesBothRetailRaceAndAllClassTables()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_PLAYER_PRIMARY_FIXTURE");
        Skip.If(string.IsNullOrEmpty(path),"Requires the pinned 5.8 player primary proof");
        byte[] bytes=File.ReadAllBytes(path!);
        Assert.Equal("ccd80e74109404f8eacaccb1c2d97a284206b4e75b4a59f806bf1092bc4698db",Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var proof=JsonDocument.Parse(bytes);
        var rows=proof.RootElement.GetProperty("rows");Assert.Equal(17,rows.GetArrayLength());
        foreach(var row in rows.EnumerateArray())
        {
            var source=row.GetProperty("retail_initial");var actual=RetailNpcCurseStatMath.InitialClassPrimary(row.GetProperty("index").GetInt32());
            Assert.Equal(source[1].GetUInt16(),actual.Health);Assert.Equal(source[5].GetUInt16(),actual.Will);
        }
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcCurseStatMath.InitialClassPrimary(-1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcCurseStatMath.InitialClassPrimary(17));
    }
    [Fact]
    public void RetailRevisesFlatAndPercentItemBonusesSeparately()
    {
        var bonus=new RetailNpcCurseStatMath.ItemResourceBonus(572,-35);
        Assert.Equal(bonus,RetailNpcCurseStatMath.ReviseItemBonus(Resource.Hp,bonus,33,23,false));
        Assert.Equal(bonus,RetailNpcCurseStatMath.ReviseItemBonus(Resource.Mp,bonus,23,33,true));
        Assert.Equal(new(457,-28),RetailNpcCurseStatMath.ReviseItemBonus(Resource.Hp,bonus,33,23,true));
        Assert.Equal(new(11,0),RetailNpcCurseStatMath.ReviseItemBonus(Resource.Mp,bonus,100,0,true));
        Assert.Equal(new(-457,28),RetailNpcCurseStatMath.ReviseItemBonus(Resource.Mp,new(-572,35),33,23,true));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcCurseStatMath.ReviseItemBonus((Resource)2,bonus,33,23,true));
    }
    [Theory]
    [InlineData(0,1,100,244,210)]
    [InlineData(0,65,100,7853,4711)]
    [InlineData(1,65,100,8639,4711)]
    [InlineData(7,65,100,5104,7066)]
    public void InitialBaseMatchesReviewedNativeCases(int classIndex,ushort level,ushort primary,int hp,int mp)
    {
        Assert.Equal(hp,RetailNpcCurseStatMath.InitialBase(Resource.Hp,level,primary,classIndex));
        Assert.Equal(mp,RetailNpcCurseStatMath.InitialBase(Resource.Mp,level,primary,classIndex));
    }

    [SkippableFact]
    public void InitialBaseMatchesPinnedBinaryDerivedCorpus()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_BASE_FIXTURE");
        Skip.If(string.IsNullOrEmpty(path),"Requires offline retail base fixture");
        byte[] bytes=File.ReadAllBytes(path!);
        Assert.Equal("2b23b89defd3449017e2f40b70bf5e06decb29e9e6e2b8a5256c4f2d4f430a9c",Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var fixture=JsonDocument.Parse(bytes);
        var cases=fixture.RootElement.GetProperty("cases");Assert.Equal(272,cases.GetArrayLength());
        foreach(var row in cases.EnumerateArray())
        {
            int cls=row.GetProperty("class_index").GetInt32();
            ushort level=row.GetProperty("level").GetUInt16(),primary=row.GetProperty("primary").GetUInt16();
            Assert.Equal(row.GetProperty("hp").GetInt32(),RetailNpcCurseStatMath.InitialBase(Resource.Hp,level,primary,cls));
            Assert.Equal(row.GetProperty("mp").GetInt32(),RetailNpcCurseStatMath.InitialBase(Resource.Mp,level,primary,cls));
        }
    }

    [Fact]
    public void InitialBaseRejectsUnknownNativeInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcCurseStatMath.InitialBase(Resource.Hp,1,100,-1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcCurseStatMath.InitialBase(Resource.Mp,1,100,17));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcCurseStatMath.InitialBase((Resource)2,1,100,0));
    }

    [Fact]
    public void EquipmentChangesHpPercentageBaseButNotMpPercentageBase()
    {
        int[] armor=[300,125,-5];
        Assert.Equal(1420,RetailNpcCurseStatMath.PercentageBase(Resource.Hp,1000,armor));
        Assert.Equal(1000,RetailNpcCurseStatMath.PercentageBase(Resource.Mp,1000,armor));
        Assert.Equal(int.MinValue,RetailNpcCurseStatMath.PercentageBase(Resource.Hp,int.MaxValue,[1]));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcCurseStatMath.PercentageBase((Resource)2,1000,armor));
    }

    [Fact]
    public void RetailTruncatesSignedPercentageBeforeAdditionUnlikeHostCurse()
    {
        var host = new AdditionStat(StatEnum.MAXHP, 1001, null!);
        new StatRateFunction(StatEnum.MAXHP, -35, true).Apply(host, new HashSet<CalculationType>());
        Assert.Equal(650, host.GetCurrent());
        Assert.Equal(651, RetailNpcCurseStatMath.Maximum(Resource.Hp, 1001, 1001, -35));
    }

    [Fact]
    public void PercentageUsesTheAggregateAndOnlyTheExplicitBase()
    {
        Assert.Equal(1079, RetailNpcCurseStatMath.Maximum(Resource.Hp, 1001, 1329, -25));
        Assert.Equal(1459, RetailNpcCurseStatMath.Maximum(Resource.Hp, 1001, 1329, 13));
    }

    [Theory]
    [InlineData(true, 1, 0, -100, 100)]
    [InlineData(false, 1, 0, -100, 1)]
    [InlineData(true, 2000000000, 2000000000, 100, 2000000000)]
    public void MaximumUsesResourceBounds(bool hp, int basis, int other, int percent, int expected)
        => Assert.Equal(expected, RetailNpcCurseStatMath.Maximum(hp ? Resource.Hp : Resource.Mp, basis, other, percent));

    [Theory]
    [InlineData(true, 1001, 651, 100, 66)]
    [InlineData(true, 1001, 651, 0, 0)]
    [InlineData(false, 1001, 651, 0, 0)]
    [InlineData(false, 1001, 651, 100, 66)]
    [InlineData(true, 651, 1001, 66, 101)]
    [InlineData(true, 1000, 100, 1, 1)]
    [InlineData(true, 0, 100, 12, 12)]
    public void Mode2AdjustsCurrentWithTruncatedFloatDelta(bool hp, int oldMax, int newMax, int current, int expected)
        => Assert.Equal(expected, RetailNpcCurseStatMath.CurrentMode2(hp ? Resource.Hp : Resource.Mp, oldMax, newMax, current));
}
