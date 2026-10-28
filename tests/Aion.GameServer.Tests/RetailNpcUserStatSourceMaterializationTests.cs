using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using C = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserStatSourceCatalog;
using M = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserStatSourceMaterialization;
using R = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserStatSourceRecipes;
using GI = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserGearStatLifeInputs;
using G = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserOwnedStatGraph;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcUserStatSourceMaterializationTests
{
    public static IEnumerable<object[]> Cases=>StatSourceFixture.Cases.Select(x=>new object[]{StatSourceFixture.Text(x,"label")});
    [Theory][MemberData(nameof(Cases))]
    public void IndependentSourceAndRawCurrentInputsProduceTheCompleteReadHistories(string label)
    {
        using var f=new StatSourceFixture();var c=C.Read(f.Request());var row=StatSourceFixture.Case(label);
        M.RegistryInput input=StatSourceFixture.Input(c,row);var projected=G.ProjectGeneral(input);
        Assert.Equal(M.Status.Computed,projected.Status);Assert.Null(projected.Failure);Assert.False(projected.ActivationReady);Assert.False(projected.RuntimeVerified);
        var projection=Assert.IsType<M.Projection>(projected.Candidate);
        Assert.Equal(input.Registrations.Sum(x=>x.ActualFunctions.Length),projection.Functions.Length);
        Assert.Equal(input.Registrations.Length,projection.RegistrationOccurrences.Length);
        Assert.Equal(input.StatLists.Sum(x=>x.Entries.Length),projection.Functions.Length);
        M.RefreshResult result=G.EvaluateGeneral(projection,StatSourceFixture.Frame(row));
        Assert.Equal(M.Status.Computed,result.Status);Assert.Null(result.Failure);Assert.False(result.ActivationReady);Assert.False(result.RuntimeVerified);
        M.Derived actual=Assert.IsType<M.Derived>(result.Candidate);var expected=row.GetProperty("expected");
        Assert.Equal(expected.GetProperty("base_hp").GetInt32(),actual.BaseHp);Assert.Equal(expected.GetProperty("base_mp").GetInt32(),actual.BaseMp);
        Assert.Equal(expected.GetProperty("health_additional").GetInt32(),actual.HealthAdditional);Assert.Equal(expected.GetProperty("will_additional").GetInt32(),actual.WillAdditional);
        Assert.Equal(6,actual.Stats.Count);Assert.Equal(6,result.Reads.Length);
        foreach(var pair in expected.GetProperty("values").EnumerateObject())
        {
            G.StatValue value=actual.Stats[pair.Name];Assert.Equal(pair.Value.GetProperty("value").GetInt32(),value.Current);
            Assert.Equal(Word(pair.Value.GetProperty("exact")),value.Exact);Assert.Equal(Fields(pair.Value.GetProperty("fields")),value.Fields);
            JsonElement[] history=expected.GetProperty("histories").GetProperty(pair.Name).EnumerateArray().ToArray();Assert.Equal(history.Length,value.History.Length);
            for(int i=0;i<history.Length;i++)
            {
                G.Step step=value.History[i];Assert.Equal(i,step.Ordinal);Assert.Equal(history[i].GetProperty("valid").GetBoolean(),step.Condition);
                if(step.Condition){Assert.Equal(Fields(history[i].GetProperty("fields")),step.State);Assert.Equal(Word(history[i].GetProperty("exact")),step.Exact);}
                else{Assert.Null(step.State);Assert.Null(step.Exact);}
            }
            M.ReadReceipt receipt=Assert.Single(result.Reads,x=>x.Stat==pair.Name);
            Assert.Equal(input.StatLists.SingleOrDefault(x=>x.Stat==pair.Name)?.Entries.ToArray()??[],receipt.CurrentEntries.ToArray());
            Assert.Equal(value.History.Count(x=>x.Condition),receipt.ExecutedEntries.Length);
            Assert.Equal(value.History.Count(x=>!x.Condition),receipt.PredicateFalseEntries.Length);
        }
        Assert.Equal(actual.Stats["HEALTH"].Current,actual.Health);Assert.Equal(actual.Stats["WILL"].Current,actual.Will);
        Assert.Equal(actual.Stats["MAXHP"].Current,actual.MaxHp);Assert.Equal(actual.Stats["MAXMP"].Current,actual.MaxMp);
        Assert.Equal(actual.Stats[StatSourceFixture.Bool(row,"flying")?"FLY_SPEED":"SPEED"].Current,actual.Speed);
        Assert.Equal(actual.Stats["ATTACK_SPEED"].Current,actual.Delay);
        Assert.Equal(projection.Functions.Where(x=>!R.ReadStats.Contains(x.Stat)).Select(x=>x.Entry).ToArray(),actual.RetainedNonReadPrograms.Select(x=>x.Entry).ToArray());
    }

    [Theory]
    [InlineData("policy")][InlineData("owners")][InlineData("lists")][InlineData("order")][InlineData("items")]
    [InlineData("nullowner")][InlineData("defaultregs")][InlineData("defaultitems")][InlineData("defaultlists")]
    [InlineData("duplicatereg")][InlineData("duplicatesequence")][InlineData("nullreg")][InlineData("nullfunction")]
    [InlineData("functionvalue")][InlineData("functionclass")][InlineData("functionowner")][InlineData("functiongeneration")]
    [InlineData("functionepoch")][InlineData("objectidentity")][InlineData("incompletefields")][InlineData("sourcechanged")]
    [InlineData("factory")][InlineData("listmissing")][InlineData("listduplicate")][InlineData("wrongstat")]
    [InlineData("priority")][InlineData("defaultentries")][InlineData("floating")][InlineData("ownerreceipt")]
    [InlineData("wrongcatalog")][InlineData("itemgeneration")][InlineData("itemslot")][InlineData("itemsource")]
    public void SourceCurrentAndAllSiblingAdmissionIsWhole(string defect)
    {
        using var f=new StatSourceFixture();var catalog=C.Read(f.Request());var row=StatSourceFixture.Case("source-sword-charge2");M.RegistryInput input=StatSourceFixture.Input(catalog,row);
        R.Registration r=input.Registrations[1];R.CurrentFunction fn=r.ActualFunctions[0];
        switch(defect)
        {
            case "policy":input=input with{Policy="unreviewed"};break;
            case "owners":input=input with{CompleteAllOwnersAndSiblings=null};break;
            case "lists":input=input with{CompleteAllStatLists=null};break;
            case "order":input=input with{ActualPerStatOrderKnown=false};break;
            case "items":input=input with{CompleteAllItemsAndAliases=null};break;
            case "nullowner":input=input with{PredefinedNullOwnerKnown=null};break;
            case "defaultregs":input=input with{Registrations=default};break;
            case "defaultitems":input=input with{Items=default};break;
            case "defaultlists":input=input with{StatLists=default};break;
            case "duplicatereg":input=input with{Registrations=input.Registrations.Add(r)};break;
            case "duplicatesequence":r=r with{Sequence=0};break;
            case "nullreg":input=input with{Registrations=input.Registrations.SetItem(1,null!)};break;
            case "nullfunction":r=r with{ActualFunctions=r.ActualFunctions.SetItem(0,null!)};break;
            case "functionvalue":fn=fn with{Value=unchecked(fn.Value+1)};break;
            case "functionclass":fn=fn with{Class="UnknownGameplayFunction"};break;
            case "functionowner":fn=fn with{Owner=fn.Owner with{Owner="another"}};break;
            case "functiongeneration":fn=fn with{Owner=fn.Owner with{Generation="stale"}};break;
            case "functionepoch":fn=fn with{Owner=fn.Owner with{Epoch="stale"}};break;
            case "objectidentity":fn=fn with{OriginalObjectIdentity=null};break;
            case "incompletefields":fn=fn with{CompleteOriginalFields=null};break;
            case "sourcechanged":r=r with{SourceRecipeUnchanged=null};break;
            case "factory":r=r with{FactoryCSharpSha256=new('f',64)};break;
            case "listmissing":input=input with{StatLists=input.StatLists.RemoveAt(0)};break;
            case "listduplicate":input=input with{StatLists=input.StatLists.Add(input.StatLists[0])};break;
            case "wrongstat":input=input with{StatLists=input.StatLists.SetItem(0,input.StatLists[0] with{Stat="WILL"})};break;
            case "priority":
                input=StatSourceFixture.Input(catalog,StatSourceFixture.Case("mixed-abs-debuff-first-registration"));
                int index=Enumerable.Range(0,input.StatLists.Length).Single(i=>input.StatLists[i].Stat=="MAXHP");
                input=input with{StatLists=input.StatLists.SetItem(index,input.StatLists[index] with{Entries=input.StatLists[index].Entries.Reverse().ToImmutableArray()})};break;
            case "defaultentries":input=input with{StatLists=input.StatLists.SetItem(0,input.StatLists[0] with{Entries=default})};break;
            case "floating":input=input with{Floating=input.Floating with{FlushToZero=true}};break;
            case "ownerreceipt":r=r with{Owner=r.Owner with{Evidence=null!}};break;
            case "wrongcatalog":
                using(var other=new StatSourceFixture()){var foreign=C.Read(other.Request());r=r with{Source=foreign.Retain(r.Source!.Metadata.Key)};}break;
            case "itemgeneration":input=input with{Items=input.Items.SetItem(0,input.Items[0] with{Current=input.Items[0].Current with{Generation="stale"}})};break;
            case "itemslot":input=input with{Items=input.Items.SetItem(0,input.Items[0] with{EquipmentMask=2})};break;
            case "itemsource":input=input with{Items=input.Items.SetItem(0,input.Items[0] with{Source=catalog.Retain(StatSourceFixture.Key("Item","187100015"))})};break;
        }
        if(defect is "functionvalue" or "functionclass" or "functionowner" or "functiongeneration" or "functionepoch" or "objectidentity" or "incompletefields")r=r with{ActualFunctions=r.ActualFunctions.SetItem(0,fn)};
        if(defect is "duplicatesequence" or "nullfunction" or "sourcechanged" or "factory" or "ownerreceipt" or "wrongcatalog" or "functionvalue" or "functionclass" or "functionowner" or "functiongeneration" or "functionepoch" or "objectidentity" or "incompletefields")input=input with{Registrations=input.Registrations.SetItem(1,r)};
        var result=M.Project(input);Assert.Equal(M.Status.UnsupportedPrecondition,result.Status);Assert.Null(result.Candidate);Assert.False(result.ActivationReady);
    }

    [Theory]
    [InlineData("source-inventory-variant2","variant")][InlineData("source-polish-variant2","idian")]
    [InlineData("source-enchant-sword5","level")][InlineData("source-accessory-temper-sword2","temper")]
    [InlineData("source-plume-level5","plume")][InlineData("source-set-part-full-same-owner","parts")]
    [InlineData("source-instance-registration-base","base")][InlineData("source-fusion-sword","fusion")]
    [InlineData("source-masteries-retained","armornull")][InlineData("source-masteries-retained","armoractualnull")]
    [InlineData("source-masteries-retained","armormismatch")][InlineData("source-masteries-retained","masterygroup")]
    public void CurrentProducerOperandsMustJoinTheExactSourceRecipe(string label,string defect)
    {
        using var f=new StatSourceFixture();var input=StatSourceFixture.Input(C.Read(f.Request()),StatSourceFixture.Case(label));
        if(defect is "variant" or "idian" or "level" or "temper" or "plume" or "fusion")
        {
            M.Item item=input.Items[0];item=defect switch{"variant"=>item with{InventoryVariant=1},"idian"=>item with{IdianVariant=1},"level"=>item with{EnchantLevel=null},"temper"=>item with{TemperingLevel=null},"plume"=>item with{PlumeRandom=null},_=>item with{Fusion=GI.Known.Unknown}};
            input=input with{Items=input.Items.SetItem(0,item)};
        }
        else
        {
            int i=defect.StartsWith("armor",StringComparison.Ordinal)||defect=="masterygroup"?Enumerable.Range(0,input.Registrations.Length).Single(n=>input.Registrations[n].ActualFunctions.Any(x=>x.Class=="StatArmorMasteryFunction")):1;
            var r=input.Registrations[i];
            r=defect switch{"parts"=>r with{Selection=r.Selection with{EquippedParts=0}},"base"=>r with{Selection=r.Selection with{OriginalOperands=r.Selection.OriginalOperands.SetItem(0,r.Selection.OriginalOperands[0] with{Current=r.Owner with{Generation="stale"}})}},
                "armornull"=>r with{Selection=r.Selection with{ArmorFixedBonus=null}},"armoractualnull"=>r with{ActualFunctions=r.ActualFunctions.SetItem(0,r.ActualFunctions[0] with{ArmorFactor=null})},"armormismatch"=>r with{ActualFunctions=r.ActualFunctions.SetItem(0,r.ActualFunctions[0] with{ArmorFixedBonus=999})},_=>r with{ActualFunctions=r.ActualFunctions.SetItem(0,r.ActualFunctions[0] with{MasteryGroup="WRONG"})}};
            input=input with{Registrations=input.Registrations.SetItem(i,r)};
        }
        Assert.Null(M.Project(input).Candidate);
    }

    [Theory]
    [InlineData("class")][InlineData("classhash")][InlineData("epoch")][InlineData("generation")]
    [InlineData("riding")][InlineData("sprinting")][InlineData("flying")][InlineData("fp")]
    [InlineData("charge")][InlineData("chargegeneration")][InlineData("chargepoints")][InlineData("chargeduplicate")]
    public void AnyReachedReadFailureWithholdsTheEntireCandidateAndAllReadReceipts(string defect)
    {
        using var f=new StatSourceFixture();var row=StatSourceFixture.Case("item-charge-at-late-delay");var input=StatSourceFixture.Input(C.Read(f.Request()),row);var projection=M.Project(input).Candidate!;var frame=StatSourceFixture.Frame(row);
        frame=defect switch{"class"=>frame with{ClassId=17},"classhash"=>frame with{ClassSourceSha256=new('f',64)},"epoch"=>frame with{Epoch="stale"},"generation"=>frame with{Current=frame.Current with{Generation="stale"}},
            "riding"=>frame with{Riding=null},"sprinting"=>frame with{Sprinting=true},"flying"=>frame with{Flying=null},"fp"=>frame with{Floating=frame.Floating with{RoundNearestEven=null}},
            "charge"=>frame with{Charge=default},"chargegeneration"=>frame with{Charge=frame.Charge.SetItem(0,frame.Charge[0] with{Current=frame.Charge[0].Current with{Generation="stale"}})},"chargepoints"=>frame with{Charge=frame.Charge.SetItem(0,frame.Charge[0] with{Points=-1})},_=>frame with{Charge=frame.Charge.Add(frame.Charge[0])}};
        var result=M.Refresh(projection,frame);Assert.Equal(M.Status.IncompleteReachedProgram,result.Status);Assert.Null(result.Candidate);Assert.Empty(result.Reads);
    }

    [Fact]
    public void FalseEarlierPredicatesDoNotDemandAnUnreachedChargeValue()
    {
        using var f=new StatSourceFixture();var catalog=C.Read(f.Request());
        foreach(string label in new[]{"predicate-false-weapon-skips-unknown-charge","item-predicate-false-before-unknown-charge"})
        {var row=StatSourceFixture.Case(label);var frame=StatSourceFixture.Frame(row);Assert.True(frame.Charge.IsDefault);var result=M.Refresh(M.Project(StatSourceFixture.Input(catalog,row)).Candidate!,frame);Assert.Equal(M.Status.Computed,result.Status);Assert.Contains(result.Reads.SelectMany(x=>x.PredicateFalseEntries),x=>x.StartsWith("r1/",StringComparison.Ordinal));}
    }

    [Fact]
    public void SameOwnerRemovalIncludesEverySiblingAndNonReadProgramWithoutRemovingThePhysicalItem()
    {
        using var f=new StatSourceFixture();var c=C.Read(f.Request());var row=StatSourceFixture.Case("source-whole-effect347-siblings");var input=StatSourceFixture.Input(c,row);var original=M.Project(input).Candidate!;
        Assert.Equal(3,original.RegistrationOccurrences.Count(x=>x.Owner.Owner=="effect"));Assert.Contains(original.Functions,x=>x.Stat=="FLY_SPEED"&&x.Owner.Owner=="effect");
        var removed=M.RemoveOwners(original,[StatSourceFixture.Binding("effect")]);Assert.Equal(M.Status.Computed,removed.Status);var after=removed.Candidate!;
        Assert.DoesNotContain(after.Functions,x=>x.Owner.Owner=="effect");Assert.DoesNotContain(after.RegistrationOccurrences,x=>x.Owner.Owner=="effect");Assert.Equal(original.Items.Length,after.Items.Length);
        var result=M.Refresh(after,StatSourceFixture.Frame(row));Assert.Equal(M.Status.Computed,result.Status);Assert.Equal(6000,result.Candidate!.Speed);
        Assert.Null(M.RemoveOwners(original,[StatSourceFixture.Binding("effect") with{Generation="stale"}]).Candidate);
        Assert.Equal(3,original.RegistrationOccurrences.Count(x=>x.Owner.Owner=="effect"));Assert.Equal(15,after.Functions.Length);
    }

    [Fact]
    public void EveryMasteryRawFieldRemainsExplicitEvenWhenItsProgramIsNotReadByThisRefresh()
    {
        using var f=new StatSourceFixture();var input=StatSourceFixture.Input(C.Read(f.Request()),StatSourceFixture.Case("source-masteries-retained"));
        var p=M.Project(input);Assert.Equal(M.Status.Computed,p.Status);Assert.Contains(p.Candidate!.Functions,x=>x.Program==R.Program.ArmorMastery&&x.ArmorFactor==30&&x.ArmorFixedBonus==10);
        Assert.Contains(M.Refresh(p.Candidate,StatSourceFixture.Frame(StatSourceFixture.Case("source-masteries-retained"))).Candidate!.RetainedNonReadPrograms,x=>x.Program==R.Program.ArmorMastery);
    }

    [Theory]
    [InlineData("ArmorMastery",false,1033)]
    [InlineData("ArmorMastery",true,1033)]
    [InlineData("WeaponMastery",false,1100)]
    [InlineData("WeaponMastery",true,1100)]
    [InlineData("ShieldMastery",false,1100)]
    [InlineData("ShieldMastery",true,1100)]
    public void ConditionalWrapperArithmeticUsesItsOwnChannelsAndExplicitRawConstructorFields(string programName,bool bonus,int expected)
    {
        // Independent mathematical branch fixtures, not actual source registration reachability.
        // Armor's 10*30/100 truncates to3; its fixed10*30/100f adds3. Weapon bonus
        // writes FixedBonusRate=.1; regular rate bonus writes Bonus=100.
        R.Program program=Enum.Parse<R.Program>(programName);
        var fn=new R.Function("synthetic-wrapper","MAXHP","conditional-wrapper",10,bonus,program,GI.FunctionKind.Rate,[],StatSourceFixture.Binding("conditional"),R.OwnerType.Effect,"synthetic","synthetic","synthetic",0,bonus?50:20,Group:"SWORD",ArmorFixedBonus:10,ArmorFactor:30);
        G.StatValue value=G.EvaluateGeneralOrdered("MAXHP",1000,[new(fn,0,true,0)]);
        Assert.Equal(expected,value.Current);Assert.Equal(BitConverter.SingleToUInt32Bits((float)expected),value.Exact);
        Assert.Equal(program==R.Program.WeaponMastery&&bonus?BitConverter.SingleToUInt32Bits(.1f):0u,value.Fields.FixedBonusRate);
    }

    [Fact]
    public void NullReachedFramesAndRemovalOwnersAreTypedWholeFailures()
    {
        using var f=new StatSourceFixture();var row=StatSourceFixture.Case("source-predefined-unarmed");var p=M.Project(StatSourceFixture.Input(C.Read(f.Request()),row)).Candidate!;
        Assert.Null(M.Refresh(p,null!).Candidate);Assert.Null(M.Refresh(p,StatSourceFixture.Frame(row) with{Current=null!}).Candidate);
        Assert.Null(M.RemoveOwners(p,[null!]).Candidate);Assert.Null(M.RemoveOwners(null!,[]).Candidate);
    }
    [Theory]
    [InlineData("NOT_AN_ITEM_GROUP")][InlineData("sword")][InlineData("1")][InlineData("SWORD,BOW")]
    public void ConsumedWeaponTokensRejectUnknownCaseChangedAndNumericEnumAliases(string token)
    {
        Assert.Throws<GI.Domain>(()=>R.GroupToken(token));
        var node=System.Xml.Linq.XElement.Parse("<add name='MAXHP' value='1'><conditions><weapon weapon='"+token+"'/></conditions></add>");
        Assert.Throws<GI.Domain>(()=>R.Conditions(node));
    }
    [Theory]
    [InlineData("NOT_AN_ARMOR_TYPE")][InlineData("plate")][InlineData("1")]
    public void ConsumedArmorSubtypeTokensUseExactJavaSpellings(string token)=>Assert.Throws<GI.Domain>(()=>R.ArmorToken(token));

    [Fact]
    public void ItemGroupAbsenceRetainsTheProvedNoneDefaultWithoutChangingRawPresence()
    {
        var absent=System.Xml.Linq.XElement.Parse("<item_template id='1'/>");
        Assert.Equal("NONE",R.ItemGroup(absent));Assert.Null(absent.Attribute("item_group"));
        Assert.Equal("NONE",R.ItemGroup(System.Xml.Linq.XElement.Parse("<item_template id='1' item_group='NONE'/>")));
        Assert.Throws<GI.Domain>(()=>R.ItemGroup(System.Xml.Linq.XElement.Parse("<item_template id='1' item_group=''/>")));
        Assert.Empty(R.Conditions(System.Xml.Linq.XElement.Parse("<add name='MAXHP' value='1'><conditions><weapon weapon=' '/></conditions></add>"))[0].Weapons);
        Assert.Throws<GI.Domain>(()=>R.Conditions(System.Xml.Linq.XElement.Parse("<add name='MAXHP' value='1'><conditions><weapon/></conditions></add>")));
    }

    [Theory][InlineData("weapon")][InlineData("armor")]
    public void UnknownMasterySubtypeCannotBeAdmittedByMatchingAnEquallyUntypedCurrentString(string field)
    {
        using var f=new StatSourceFixture();string old=field=="weapon"?"SWORD":"PLATE";
        f.Xml("skills/skill_templates.xml",s=>s.Replace(field+"=\""+old+"\"",field+"=\"NOT_A_BOUND_ENUM\"",StringComparison.Ordinal));
        var input=StatSourceFixture.Input(C.Read(f.Request()),StatSourceFixture.Case("source-masteries-retained"));int index=field=="weapon"?1:2;
        var r=input.Registrations[index];r=r with{ActualFunctions=r.ActualFunctions.Select(x=>x with{MasteryGroup="NOT_A_BOUND_ENUM"}).ToImmutableArray()};
        input=input with{Registrations=input.Registrations.SetItem(index,r)};
        var result=M.Project(input);Assert.Equal(M.Status.UnsupportedPrecondition,result.Status);Assert.Null(result.Candidate);
    }

    [Fact]
    public void OnePhysicalIdentityCannotBeAnItemAndAnEffectEvenWithTheSameGeneration()
    {
        using var f=new StatSourceFixture();var c=C.Read(f.Request());var input=StatSourceFixture.Input(c,StatSourceFixture.Case("source-skill347-child1"));
        var item=new M.Item(StatSourceFixture.Binding("effect"),c.Retain(StatSourceFixture.Key("Item","100001286")),1,true,R.OwnerType.Item,GI.Known.Null);
        var result=M.Project(input with{Items=[item]});Assert.Equal(M.Status.UnsupportedPrecondition,result.Status);Assert.Null(result.Candidate);
    }

    [Fact]
    public void FusionOwnerGenerationJoinsPhysicalItemDirectlyWithoutDependingOnABaseItemSibling()
    {
        using var f=new StatSourceFixture();var input=StatSourceFixture.Input(C.Read(f.Request()),StatSourceFixture.Case("source-fusion-sword"));
        var removed=input.Registrations[1].ActualFunctions.Select(x=>x.Entry).ToHashSet(StringComparer.Ordinal);
        var fusion=input.Registrations[2];var stale=fusion.Owner with{Generation="stale-fusion-generation"};
        fusion=fusion with{Owner=stale,ActualFunctions=fusion.ActualFunctions.Select(x=>x with{Owner=stale}).ToImmutableArray()};
        input=input with{Registrations=[input.Registrations[0],fusion],StatLists=input.StatLists.Select(x=>x with{Entries=x.Entries.Where(e=>!removed.Contains(e)).ToImmutableArray()}).ToImmutableArray()};
        var result=M.Project(input);Assert.Equal(M.Status.UnsupportedPrecondition,result.Status);Assert.Null(result.Candidate);
    }

    [Theory][InlineData(null)][InlineData(true)]
    public void StaffUnknownOrTrueWithholdsTheWholeOrdinaryCapsResult(bool? isStaff)
    {
        using var f=new StatSourceFixture();var row=StatSourceFixture.Case("source-predefined-unarmed");var p=M.Project(StatSourceFixture.Input(C.Read(f.Request()),row)).Candidate!;
        var result=M.Refresh(p,StatSourceFixture.Frame(row) with{IsStaff=isStaff});Assert.Equal(M.Status.IncompleteReachedProgram,result.Status);Assert.Null(result.Candidate);Assert.Empty(result.Reads);
    }
    private static uint Word(JsonElement x)=>uint.Parse(x.GetString()!,NumberStyles.HexNumber,CultureInfo.InvariantCulture);
    private static G.Fields Fields(JsonElement x){uint[] a=x.EnumerateArray().Select(Word).ToArray();return new(a[0],a[1],a[2],a[3],a[4],a[5]);}
}
