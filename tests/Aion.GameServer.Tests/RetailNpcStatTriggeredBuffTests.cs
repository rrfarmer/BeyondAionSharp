using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Tests;

// Pure source/recipe checks: no world, NPC spawning, networking, clocks or SIM.
[Collection("GoldenDataManager")]
public sealed class RetailNpcStatTriggeredBuffTests
{
    private static RetailNpcStatTriggeredBuffs.Definition Parse(int id,string name,string fields="")
    {
        byte[] raw=Encoding.Unicode.GetBytes($"<skill_base><id>{id}</id><name>{name}</name>{fields}</skill_base>");
        return RetailNpcStatTriggeredBuffs.Parse(raw,Convert.ToHexStringLower(SHA256.HashData(raw)));
    }
    private static RetailNpcStatTriggeredBuffs.Definition Parent(int id,string child)=>Parse(id,"Parent"+id,
        $"<effect2_type>Provoker</effect2_type><effect2_reserved14>Me</effect2_reserved14><effect2_reserved17>{child}</effect2_reserved17>");

    [Fact]
    public void ChildRequiresAnActualNativePathFromADeclaredRoot()
    {
        var graph=new RetailNpcStatTriggeredBuffs([Parent(10,"Child"),Parse(20,"Child"),Parse(30,"Other")]);
        Assert.True(graph.TryResolve(20,[10],out var path));
        Assert.Equal(new RetailNpcStatTriggeredBuffs.Link(10,2,20,"Me"),Assert.Single(path));
        Assert.False(graph.TryResolve(20,[30],out _));
        Assert.False(graph.TryResolve(20,[20],out _)); // Global existence does not bypass declared levels.
    }

    [Fact]
    public void HostDriftCannotAuthorizeAnotherChildOrReceiver()
    {
        var graph=new RetailNpcStatTriggeredBuffs([Parent(10,"Child"),Parse(20,"Child")]);
        var actual=new ProvokerEffect {Position=2,skillId=20,provokeTarget=ProvokeTarget.ME};
        Assert.True(graph.TryResolve(20,[10],out _,l=>RetailNpcStatTriggeredBuffs.MatchesHost(l,actual)));
        actual.skillId=21;
        Assert.False(graph.TryResolve(20,[10],out _,l=>RetailNpcStatTriggeredBuffs.MatchesHost(l,actual)));
        actual.skillId=20;actual.provokeTarget=ProvokeTarget.OPPONENT;
        Assert.False(graph.TryResolve(20,[10],out _,l=>RetailNpcStatTriggeredBuffs.MatchesHost(l,actual)));
    }

    [Fact]
    public void CyclesTerminateAndAmbiguousNativeNamesRemainUnbound()
    {
        var graph=new RetailNpcStatTriggeredBuffs([Parent(10,"Parent20"),Parent(20,"Parent10"),Parse(30,"Unknown")]);
        Assert.False(graph.TryResolve(30,[10],out _));
        graph=new([Parent(10,"child"),Parse(20,"Child"),Parse(21,"CHILD")]);
        Assert.False(graph.TryResolve(20,[10],out _));
    }

    [Fact]
    public void StatValuesMustBeIndependentOfTheUnchangedTriggerLevelOwner()
    {
        RetailNpcStatBuffs.Source Source(int delta)
        {
            byte[] raw=Encoding.Unicode.GetBytes($"<skill_base><id>20</id><name>Child</name><effect1_type>Statup</effect1_type>"
                +$"<effect1_reserved1>{delta}</effect1_reserved1><effect1_reserved2>70</effect1_reserved2>"
                +"<effect1_reserved6>1</effect1_reserved6><effect1_reserved13>physicaldefend</effect1_reserved13></skill_base>");
            return RetailNpcStatBuffs.Parse(raw,Convert.ToHexStringLower(SHA256.HashData(raw)));
        }
        Assert.True(RetailNpcStatTriggeredBuffs.IsConstant(Source(0)));
        Assert.False(RetailNpcStatTriggeredBuffs.IsConstant(Source(1)));
        Assert.False(RetailNpcStatTriggeredBuffs.IsConstant(Source(-1)));
        Assert.All(new[]{0,1,20,32767},level=>Assert.Equal(70,RetailNpcStatBuffs.Value(Source(0).Programs[0].Parts[0],level)));
    }

    [Fact]
    public void DuplicateReferenceCannotBeHiddenByXmlOrder()
    {
        Assert.Throws<InvalidDataException>(()=>Parse(10,"Parent",
            "<effect1_type>Provoker</effect1_type><effect1_reserved17>A</effect1_reserved17><effect1_reserved17>B</effect1_reserved17>"));
    }

    [SkippableFact]
    public void BothIshalgenBuffGapsBindThroughTheirPinnedParentsWithoutDeclaringAnotherNpcSkill()
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? hostFile=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HOST_SKILLS");
        Skip.If(string.IsNullOrEmpty(manifest)||string.IsNullOrEmpty(hash)||string.IsNullOrEmpty(hostFile),"Requires pinned offline source package and host skill XML");
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!),hash!,Path.GetDirectoryName(manifest!)!);
        var hosts=XDocument.Load(hostFile!).Root!.Elements("skill_template").ToDictionary(x=>(int)x.Attribute("skill_id")!);
        var rows=new List<object>();
        foreach(var (npc,parent) in new[]{(210407,16394),(210409,16856)})
        {
            Assert.True(RetailNpcStatCatalog.TryGet(220010000,npc,out var profile));
            Assert.False(RetailNpcStatCatalog.TryGetSkill(profile,16393,1,out _));
            Assert.True(RetailNpcStatCatalog.TryGetBuff(profile,16393,1,out var source,out var lineage,link=>
            {
                var part=hosts[link.ParentId].Element("effects")!.Elements().Single(x=>(int?)x.Attribute("e")==link.Position);
                if(part.Name!="provoker")return false;
                return RetailNpcStatTriggeredBuffs.MatchesHost(link,new ProvokerEffect {Position=link.Position,
                    skillId=(int)part.Attribute("skill_id")!,provokeTarget=Enum.Parse<ProvokeTarget>(((string)part.Attribute("provoke_target")!).ToUpperInvariant())});
            }));
            var edge=Assert.Single(lineage);Assert.Equal(parent,edge.ParentId);Assert.Equal(16393,edge.ChildId);
            Assert.True(RetailNpcStatCatalog.TriggeredBuffMatchesStack(16393,(string)hosts[16393].Attribute("stack")!));
            var program=Assert.Single(source.Programs);var value=Assert.Single(program.Parts);
            Assert.Equal(StatEnum.PHYSICAL_DEFENSE,value.Stat);Assert.True(value.Percent);Assert.Equal(0,value.Delta);Assert.Equal(70,value.Value);
            rows.Add(new {npc,parent,child=source.Id,source=source.Sha256,physical_defense_percent=value.Value});
        }
        if(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TRIGGERED_AUDIT_OUTPUT") is {Length:>0} output)
        {
            Assert.False(File.Exists(output));File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(rows));
        }
    }
}
