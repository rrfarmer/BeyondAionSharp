using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

/// <summary>Detached source/condition calculations; no actors, gameplay or SIM.</summary>
public sealed class RetailNpcStatSpellRaceTests
{
    [SkippableFact]
    public void SelectedDrainRaceBonusUsesNativeNpcOrderAndDoesNotGuessUnknownTargets()
    {
        var (context,_)=Context();
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned selected retail package");
        byte[] manifestBytes=File.ReadAllBytes(path!);
        var audit=RetailNpcStatCatalog.ReadForAudit(manifestBytes,hash!,Path.GetDirectoryName(path!)!);
        using var manifest=JsonDocument.Parse(manifestBytes);
        var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray()
            .Single(x=>x.GetProperty("id").GetInt32()==20061);
        byte[] bytes=File.ReadAllBytes(Path.GetFullPath(asset.GetProperty("path").GetString()!,Path.GetDirectoryName(path!)!));
        Assert.Equal("336cd0201e9ceb34d38ab9a701114858c2abd00598788e5d59d10085451533cf",
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var source=XElement.Parse(Encoding.Unicode.GetString(bytes));
        Assert.Equal("SpellATKDrain_Instant",source.Element("effect1_type")!.Value);
        Assert.Equal("0",source.Element("effect1_reserved3")!.Value);
        Assert.Equal("100000",source.Element("effect1_reserved4")!.Value);
        Assert.Equal("0",source.Element("effect1_reserved5")!.Value);
        Assert.Equal("200",source.Element("effect1_reserved15")!.Value);
        Assert.True(RetailNpcStatSpellBonus.TryNpcRaceCondition(source.Element("effect1_reserved7")!.Value,context,out ulong mask));
        Assert.Equal(1UL<<43,mask);
        var matching=audit.Members.Select(x=>x.Profile).First(x=>x.NpcId==282743);
        var other=audit.Members.Select(x=>x.Profile).First(x=>context.TryNpcRace(x,out var race)&&race.Order!=43);
        var unknown=audit.Members.Select(x=>x.Profile).First(x=>!context.TryNpcRace(x,out _));
        Assert.True(RetailNpcStatSpellBonus.TryNpcRaceMatch(mask,context,matching,out bool hit));Assert.True(hit);
        Assert.True(RetailNpcStatSpellBonus.TryNpcRaceMatch(mask,context,other,out bool miss));Assert.False(miss);
        Assert.False(RetailNpcStatSpellBonus.TryNpcRaceMatch(mask,context,unknown,out _));
        Assert.Equal(101000,RetailNpcStatSpellBonus.Apply(1000,100000,false,hit,0,3,(_,_)=>1).Damage);
        Assert.Equal(1000,RetailNpcStatSpellBonus.Apply(1000,100000,false,miss,0,3,
            (_,_)=>throw new Exception("A nonmatching predicate must not draw")).Damage);
        foreach(string condition in new[]{"", "_Race_unknown", "_Race_TricoDark,Poison", "_Class_mage"})
            Assert.False(RetailNpcStatSpellBonus.TryNpcRaceCondition(condition,context,out _));
        Assert.False(RetailNpcStatSpellBonus.TryNpcRaceCondition("_Race_TricoDark",null,out _));
    }

    [SkippableFact]
    public void ReviewedNpcRecordsResolveTheirRetailRaceWithoutDefaultingUnknowns()
    {
        var (context,_)=Context();
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned selected retail package");
        var audit=RetailNpcStatCatalog.ReadForAudit(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        var profiles=audit.Members.Select(x=>x.Profile)
            .GroupBy(x=>(x.NpcId,x.SourceRecordSha256)).Select(x=>x.First()).ToArray();
        int resolved=0,unknown=0,trico=0;
        foreach(var profile in profiles)
        {
            if(!context.TryNpcRace(profile,out var race)) { unknown++; continue; }
            resolved++;
            if(race==new RetailNpcStatSpellConditions.NativeRace(6152,43))
            {
                trico++;
                Assert.Equal(282743,profile.NpcId);
            }
        }
        Assert.Equal(10930,profiles.Length);
        Assert.Equal(10756,resolved);
        Assert.Equal(174,unknown);
        Assert.Equal(1,trico);
    }

    private static (RetailNpcStatSpellConditions Context,byte[] Bytes) Context()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SPELL_CONDITION_CONTEXT");
        Skip.If(string.IsNullOrEmpty(path),"Requires pinned selected RaceDB projection");
        byte[] bytes=File.ReadAllBytes(path!);
        return (RetailNpcStatSpellConditions.Parse(bytes,RetailNpcStatSpellConditions.ContextSha256),bytes);
    }
    private static RetailNpcStatSkillCombat.Source Parse(XElement xml,RetailNpcStatSpellConditions? context)
    {
        byte[] raw=Encoding.Unicode.GetBytes(xml.ToString(SaveOptions.DisableFormatting));
        return RetailNpcStatSkillCombat.ParseSource(raw,Convert.ToHexStringLower(SHA256.HashData(raw)),context);
    }
    [SkippableFact]
    public void SelectedRaceOrdersProjectOntoExactlyTwoPlayerRacesWithoutEnumAliasing()
    {
        var (context,bytes)=Context();using var doc=JsonDocument.Parse(bytes);
        int count=0;
        foreach(var row in doc.RootElement.GetProperty("races").EnumerateArray())
        {
            string name=row.GetProperty("name").GetString()!;int id=row.GetProperty("raw_id").GetInt32();
            Assert.True(RetailNpcStatSpellBonus.TryCondition("_Race_"+name,context,out uint classes,out int? races));
            Assert.Equal(0u,classes);Assert.NotNull(races);
            var program=new RetailNpcStatSpellBonus.Program(0,75,false,classes,races,RetailNpcStatSpellConditions.ContextSha256);
            foreach(var pc in Enum.GetValues<PlayerClass>())
            {
                Assert.Equal(id==0,program.AppliesTo(pc,Race.ELYOS));
                Assert.Equal(id==1,program.AppliesTo(pc,Race.ASMODIANS));
            }
            count++;
        }
        Assert.Equal(64,count);
        Assert.False(context.TryPlayerMask("human",out _)); // Native abstract class has no race order.
        Assert.False(context.TryPlayerMask("unknown",out _));
    }
    [SkippableFact]
    public void NonplayerRaceConditionIsFalseAndConsumesNeitherBonusDraw()
    {
        var (context,_)=Context();
        foreach(string name in new[]{"type_c","type_d","construct","drakan","outsider","pc_dragon"})
        {
            Assert.True(RetailNpcStatSpellBonus.TryCondition("_dmg_Race_"+name,context,out uint classes,out int? races));
            Assert.Equal(0,races);
            var program=new RetailNpcStatSpellBonus.Program(86,-86,false,classes,races,RetailNpcStatSpellConditions.ContextSha256);
            foreach(var race in new[]{Race.ELYOS,Race.ASMODIANS})
            {
                Assert.False(program.AppliesTo(PlayerClass.CLERIC,race));
                var result=RetailNpcStatSpellBonus.Apply(123,program.ValueAtLevel(20),false,
                    program.AppliesTo(PlayerClass.CLERIC,race),1,3,(_,_)=>throw new Exception("No bonus draw expected"));
                Assert.Equal(new RetailNpcStatSpellBonus.Result(123,null,null,null),result);
            }
        }
    }
    [SkippableFact]
    public void RaceAndClassConditionsAreOrCombinedAndCaseFolded()
    {
        var (context,_)=Context();
        Assert.True(RetailNpcStatSpellBonus.TryCondition("_Race_PC_LIGHT,\t_Class_priest _dmg_Race_Type_C",context,out uint classes,out int? races));
        var program=new RetailNpcStatSpellBonus.Program(0,70,false,classes,races,RetailNpcStatSpellConditions.ContextSha256);
        Assert.True(program.AppliesTo(PlayerClass.WARRIOR,Race.ELYOS));
        Assert.True(program.AppliesTo(PlayerClass.CLERIC,Race.ASMODIANS));
        Assert.False(program.AppliesTo(PlayerClass.PRIEST,Race.ASMODIANS));
    }
    [SkippableFact]
    public void MissingContextUnknownConditionsAndUnsupportedPlayerRacesRemainVisible()
    {
        var (context,_)=Context();
        Assert.False(RetailNpcStatSpellBonus.TryCondition("_Race_PC_Light",null,out _,out _));
        foreach(string text in new[]{"_Race_unknown","_Race_human","_Race_pc_light,Snare","_Back","_Race_Type_C\n"})
            Assert.False(RetailNpcStatSpellBonus.TryCondition(text,context,out _,out _));
        var program=new RetailNpcStatSpellBonus.Program(0,1,false,0,0,RetailNpcStatSpellConditions.ContextSha256);
        Assert.Throws<ArgumentOutOfRangeException>(()=>program.AppliesTo(PlayerClass.WARRIOR,Race.LYCAN));
        Assert.Throws<ArgumentOutOfRangeException>(()=>program.AppliesTo(PlayerClass.WARRIOR,Race.NONE));
    }
    [SkippableFact]
    public void ChangedContextIsRejectedEvenWhenTheCallerRecomputesItsDigest()
    {
        var (_,bytes)=Context();bytes[^2]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcStatSpellConditions.Parse(bytes,RetailNpcStatSpellConditions.ContextSha256));
        Assert.Throws<InvalidDataException>(()=>RetailNpcStatSpellConditions.Parse(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))));
    }
    [SkippableFact]
    public void EveryActualRaceSourceEqualsItsUnconditionalControlExceptForTheProvedPredicate()
    {
        var (context,_)=Context();
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? pin=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(pin),"Requires complete pinned source records");
        byte[] raw=File.ReadAllBytes(path!);Assert.Equal(pin,Convert.ToHexStringLower(SHA256.HashData(raw)));
        using var manifest=JsonDocument.Parse(raw);var rows=new List<object>();int physical=0,spell=0;
        foreach(var asset in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            byte[] sourceBytes=File.ReadAllBytes(Path.GetFullPath(asset.GetProperty("path").GetString()!,Path.GetDirectoryName(path!)!));
            var xml=XElement.Parse(Encoding.Unicode.GetString(sourceBytes));
            string? condition=xml.Element("effect1_reserved16")?.Value;
            var source=RetailNpcStatSkillCombat.ParseSource(sourceBytes,asset.GetProperty("sha256").GetString()!,context);
            var old=RetailNpcStatSkillCombat.ParseSource(sourceBytes,asset.GetProperty("sha256").GetString()!);
            if(source.Supported&&source.Family==RetailNpcStatSkillCombat.Kind.PhysicalInstant)physical++;
            if(source.Supported&&source.Family==RetailNpcStatSkillCombat.Kind.SpellInstant)spell++;
            if(xml.Element("effect1_type")?.Value=="SkillATK_Instant"&&condition?.Contains("_race_",StringComparison.OrdinalIgnoreCase)==true)
            {
                // Physical second-half predicates now use this same independently
                // pinned race projection; their float arithmetic has separate tests.
                if(source.Supported)
                {
                    Assert.False(old.Supported);Assert.NotNull(source.PhysicalBonus);
                    Assert.Equal(RetailNpcStatSpellConditions.ContextSha256,source.PhysicalBonus.ConditionContextSha256);
                    Assert.Equal(0,source.PhysicalBonus.PlayerRaceMask);
                    foreach(var race in new[]{Race.ELYOS,Race.ASMODIANS})
                        foreach(var pc in Enum.GetValues<PlayerClass>())Assert.False(source.PhysicalBonus.AppliesTo(pc,race));
                }
                else Assert.False(old.Supported);
                continue;
            }
            if(xml.Element("effect1_type")?.Value!="SpellATK_Instant"||condition is null||!condition.Contains("_race_",StringComparison.OrdinalIgnoreCase))
            {Assert.Equal(old,source);continue;}
            xml.Element("effect1_reserved16")!.Remove();var control=Parse(xml,context);
            Assert.True(context.TryPlayerMask(condition[(condition.IndexOf("_race_",StringComparison.OrdinalIgnoreCase)+6)..].ToLowerInvariant(),out int expectedMask));
            if(control.Supported)
            {
                Assert.NotNull(control.SpellBonus);
                Assert.Equal(control with {Sha256=source.Sha256,SpellBonus=control.SpellBonus with
                    {PlayerRaceMask=expectedMask,ConditionContextSha256=RetailNpcStatSpellConditions.ContextSha256}},source);
                Assert.False(old.Supported);
                foreach(var race in new[]{Race.ELYOS,Race.ASMODIANS})
                    foreach(var pc in Enum.GetValues<PlayerClass>())
                        Assert.Equal((expectedMask&(race==Race.ELYOS?1:2))!=0,source.SpellBonus!.AppliesTo(pc,race));
            }
            else Assert.False(source.Supported);
            rows.Add(new {id=source.Id,condition,expectedMask,supported=source.Supported,reason=source.UnsupportedReason,source=source.Sha256});
        }
        Assert.Equal(120,rows.Count);
        Assert.Equal(RetailNpcStatReviewedSourceCensus.PhysicalWithRaceContext,physical);
        Assert.Equal(RetailNpcStatReviewedSourceCensus.SpellWithRaceContext,spell);
        if(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SPELL_RACE_OUTPUT") is {Length:>0} output)
        {
            using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);JsonSerializer.Serialize(stream,new {physical,spell,rows});
        }
    }
}
