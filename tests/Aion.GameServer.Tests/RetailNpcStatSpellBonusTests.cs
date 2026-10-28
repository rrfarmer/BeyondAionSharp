using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcStatSpellBonusTests
{
    [Theory]
    [InlineData(86,-86,86,-86)]
    [InlineData(-1,100,0,0)]
    [InlineData(1,-2,0,0)]
    [InlineData(int.MaxValue,1,0,0)]
    [InlineData(0,800,0,800)]
    public void ConstructorValidatesTheWrappedLevelOnePair(int rate,int constant,int expectedRate,int expectedConstant)
        => Assert.Equal((expectedRate,expectedConstant),RetailNpcStatSpellBonus.Construct(rate,constant));

    [Fact]
    public void TheSignedLevelPairWrapsBeforeItsNonnegativeClamp()
    {
        var p=new RetailNpcStatSpellBonus.Program(86,-86,false,0);
        Assert.Equal(0,p.ValueAtLevel(0));Assert.Equal(0,p.ValueAtLevel(1));Assert.Equal(86,p.ValueAtLevel(2));
        Assert.Equal(0,(p with {Rate=100000,Constant=500}).ValueAtLevel(32767));
        Assert.Throws<ArgumentOutOfRangeException>(()=>p.ValueAtLevel(65535));
    }

    [Theory]
    [InlineData("warrior",PlayerClass.WARRIOR)]
    [InlineData("fighter",PlayerClass.GLADIATOR)]
    [InlineData("knight",PlayerClass.TEMPLAR)]
    [InlineData("scout",PlayerClass.SCOUT)]
    [InlineData("assassin",PlayerClass.ASSASSIN)]
    [InlineData("ranger",PlayerClass.RANGER)]
    [InlineData("mage",PlayerClass.MAGE)]
    [InlineData("wizard",PlayerClass.SORCERER)]
    [InlineData("elementallist",PlayerClass.SPIRIT_MASTER)]
    [InlineData("elementalist",PlayerClass.SPIRIT_MASTER)]
    [InlineData("cleric",PlayerClass.PRIEST)]
    [InlineData("priest",PlayerClass.CLERIC)]
    [InlineData("chanter",PlayerClass.CHANTER)]
    [InlineData("engineer",PlayerClass.ENGINEER)]
    [InlineData("rider",PlayerClass.RIDER)]
    [InlineData("gunner",PlayerClass.GUNNER)]
    [InlineData("artist",PlayerClass.ARTIST)]
    [InlineData("bard",PlayerClass.BARD)]
    public void NativeClassNamesSelectExactlyTheMatchingPlayerClass(string native,PlayerClass expected)
    {
        Assert.True(RetailNpcStatSpellBonus.TryClassCondition("_Class_"+native,out uint mask));
        var p=new RetailNpcStatSpellBonus.Program(0,1,false,mask);
        foreach(var pc in Enum.GetValues<PlayerClass>())Assert.Equal(pc==expected,p.AppliesTo(pc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , \t")]
    public void NoConditionLeavesTheBonusUnconditional(string? text)
    {
        Assert.True(RetailNpcStatSpellBonus.TryClassCondition(text,out uint mask));Assert.Equal(0u,mask);
    }
    [Fact]
    public void ClassTokensAreOrCombinedWithNativeSeparatorsAndCaseFolding()
    {
        Assert.True(RetailNpcStatSpellBonus.TryClassCondition("_Class_PRIEST,\t_dmg_Class_ranger _Class_priest",out uint mask));
        Assert.Equal((1u<<10)|(1u<<5),mask);
    }
    [Theory]
    [InlineData("snare")]
    [InlineData("_dmg_Race_Type_C")]
    [InlineData("_Class_Cleric,_Back")]
    [InlineData("_Class_Unknown")]
    [InlineData("_Class_ranger\n_Class_priest")]
    public void UnprovedConditionsCannotBecomeUnconditionalBonuses(string text)
        => Assert.False(RetailNpcStatSpellBonus.TryClassCondition(text,out _));

    [Theory]
    [InlineData(100,800,false,0,1,0,900)]
    [InlineData(3,50,true,1,3,65,5)] //1.5*1.5 truncates2, not trunc(1.5)*1.5.
    [InlineData(3,50,true,1,1,65,4)] //Basic-only mask skips bonus randomization.
    [InlineData(0,1,false,1,2,0,0)] //Active nonzero bonus still draws when it rounds to0.
    [InlineData(100,70,true,4,2,0,170)] //Constant100% table row still draws.
    public void BonusDrawsAndFloatQuantizationPrecedeTheIntegerAdd(int basis,int value,bool percent,int type,int mask,int word,int expected)
    {
        var calls=new List<(int,int)>();
        var result=RetailNpcStatSpellBonus.Apply(basis,value,percent,true,type,mask,(lo,hi)=>
        {calls.Add((lo,hi));return lo==1?100:word;});
        Assert.Equal(expected,result.Damage);Assert.Equal(100,result.ChanceDraw);
        bool random=type!=0&&(mask&2)!=0;
        Assert.Equal(random?word:(int?)null,result.RandomDraw);
        Assert.Equal(random?new[]{(1,100),(0,99)}:new[]{(1,100)},calls);
    }
    [Fact]
    public void AllMaskUsesIndependentBaseAndBonusWordsInNativeOrder()
    {
        var draws=new Queue<int>([0,100,99]);var calls=new List<(int,int)>();
        int Draw(int lo,int hi){calls.Add((lo,hi));return draws.Dequeue();}
        int basis=RetailNpcStatSkillCombat.SpellRandomBasic(10,1,3,Draw);
        var result=RetailNpcStatSpellBonus.Apply(basis,100,true,true,1,3,Draw);
        Assert.Equal(5,basis);Assert.Equal(12,result.Damage);Assert.Equal(7,result.AddedDamage);
        Assert.Equal(new[]{(0,99),(1,100),(0,99)},calls);Assert.Empty(draws);
    }
    [Fact]
    public void ZeroOrFalseConditionsConsumeNeitherBonusDraw()
    {
        int Forbidden(int lo,int hi)=>throw new Exception("No bonus draw expected");
        Assert.Equal(new RetailNpcStatSpellBonus.Result(100,null,null,null),RetailNpcStatSpellBonus.Apply(100,0,false,true,1,3,Forbidden));
        Assert.Equal(new RetailNpcStatSpellBonus.Result(100,null,null,null),RetailNpcStatSpellBonus.Apply(100,10,false,false,1,3,Forbidden));
    }
    [Fact]
    public void IntegerAdditionWrapsInsteadOfUsingAnUnboundedIntermediate()
    {
        var result=RetailNpcStatSpellBonus.Apply(int.MaxValue,1,false,true,0,1,(_,_)=>100);
        Assert.Equal(int.MinValue,result.Damage);
    }

    [Fact]
    public void UnadmittedInputsAndInvalidDrawsCannotBecomeDamage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcStatSpellBonus.Apply(-1,1,false,true,0,1,(_,_)=>1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcStatSpellBonus.Apply(1,-1,false,true,0,1,(_,_)=>1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcStatSpellBonus.Apply(1,1,false,true,11,1,(_,_)=>1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcStatSpellBonus.Apply(1,1,false,true,0,0,(_,_)=>1));
        Assert.Throws<InvalidOperationException>(()=>RetailNpcStatSpellBonus.Apply(1,1,false,true,0,1,(_,_)=>0));
        Assert.Throws<InvalidOperationException>(()=>RetailNpcStatSpellBonus.Apply(1,1,false,true,1,3,(lo,_)=>lo==1?100:100));
    }

    [SkippableFact]
    public void EverySelectedNativeTableBucketAlsoAppliesToTheIndependentBonusDraw()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_RANDOM_TABLE_SOURCE");
        Skip.If(string.IsNullOrEmpty(path),"Requires selected native random table");
        byte[] raw=File.ReadAllBytes(path!);
        Assert.Equal("1901847166c70e03258072a31737d3e5d43ec7bf0eb39634acfabb2e5d53eacb",Convert.ToHexStringLower(SHA256.HashData(raw)));
        var rows=XDocument.Parse(Encoding.Unicode.GetString(raw).TrimStart('\ufeff')).Root!.Elements().ToArray();
        Assert.Equal(10,rows.Length);
        foreach(var row in rows)
            for(int word=0;word<100;word++)
                foreach(int mask in new[]{2,3})
                {
                    var calls=new List<(int,int)>();
                    var result=RetailNpcStatSpellBonus.Apply(1000,100,true,true,(int)row.Attribute("random_type")!,mask,(lo,hi)=>
                    {calls.Add((lo,hi));return lo==1?100:word;});
                    Assert.Equal(1000+10*(int)row.Element("range"+(word/5))!,result.Damage);
                    Assert.Equal(new[]{(1,100),(0,99)},calls);
                }
    }

    internal static RetailNpcStatSkillCombat.Source Parse(XElement xml)
    {
        byte[] b=Encoding.Unicode.GetBytes(xml.ToString(SaveOptions.DisableFormatting));
        return RetailNpcStatSkillCombat.ParseSource(b,Convert.ToHexStringLower(SHA256.HashData(b)));
    }

    [SkippableFact]
    public void EveryBonusSourceHasAnIndependentZeroBonusControl()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? pin=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(pin),"Requires pinned complete native records");
        byte[] raw=File.ReadAllBytes(path!);Assert.Equal(pin,Convert.ToHexStringLower(SHA256.HashData(raw)));
        using var manifest=JsonDocument.Parse(raw);var rows=new List<object>();
        foreach(var asset in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            byte[] bytes=File.ReadAllBytes(Path.GetFullPath(asset.GetProperty("path").GetString()!,Path.GetDirectoryName(path!)!));
            var source=RetailNpcStatSkillCombat.ParseSource(bytes,asset.GetProperty("sha256").GetString()!);
            var x=XElement.Parse(Encoding.Unicode.GetString(bytes));
            if(x.Element("effect1_type")?.Value!="SpellATK_Instant")continue;
            int rate=(int?)x.Element("effect1_reserved8")??0,constant=(int?)x.Element("effect1_reserved9")??0;
            int mode=(int?)x.Element("effect1_reserved7")??0;
            if(rate==0&&constant==0&&mode==0)continue;
            string? condition=x.Element("effect1_reserved16")?.Value;
            x.SetElementValue("effect1_reserved8",0);x.SetElementValue("effect1_reserved9",0);x.SetElementValue("effect1_reserved7",0);
            var control=Parse(x);
            if(RetailNpcStatSpellBonus.TrySpellCondition(condition,null,out uint mask,out int? races,out int statusMask)&&mode is 0 or 1)
            {
                var pair=RetailNpcStatSpellBonus.Construct(rate,constant);
                var expected=pair==(0,0)?null:new RetailNpcStatSpellBonus.Program(pair.Rate,pair.Constant,mode==1,mask,races,StatusMask:statusMask);
                bool NoProc(string tag)=>x.Element(tag) is not { } node||int.TryParse(node.Value,out int value)&&value==0;
                bool sibling=x.Elements().Any(e=>e.Name.LocalName.StartsWith("effect",StringComparison.Ordinal)
                    &&e.Name.LocalName.EndsWith("_type",StringComparison.Ordinal)&&e.Name.LocalName!="effect1_type"
                    &&int.TryParse(e.Name.LocalName[6..^5],out _)&&e.Value!="0");
                bool statusContext=statusMask==0||pair==(0,0)||NoProc("effect1_reserved14")&&NoProc("effect1_reserved15")&&!sibling;
                if(control.Supported&&statusContext)Assert.Equal(control with {SpellBonus=expected,Sha256=source.Sha256},source);
                else Assert.False(source.Supported);
            }
            else Assert.False(source.Supported);
            rows.Add(new {id=source.Id,rate,constant,mode,condition,supported=source.Supported,reason=source.UnsupportedReason,source=source.Sha256});
        }
        Assert.Equal(178,rows.Count);
        if(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SPELL_BONUS_OUTPUT") is {Length:>0} output)
        {
            using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);JsonSerializer.Serialize(stream,new {rows});
        }
    }
}
