using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcPlayerDefenseDrainEquipmentProjectionTests
{
    private sealed class TestStatOwner:IStatOwner;
    [SkippableFact]
    public void RealSkillAndSingleEquipmentSnapshotJoinRawAndBonusInputs()
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hpPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ARMOR_FIXTURE");
        string? hpBonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        string? permitPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_CLASS_PERMIT_FIXTURE");
        string? armorPath=Environment.GetEnvironmentVariable("AION_RETAIL_17860_ARMOR_SOURCE");
        string? shieldPath=Environment.GetEnvironmentVariable("AION_RETAIL_17860_SHIELD_SOURCE");
        string? otherPath=Environment.GetEnvironmentVariable("AION_RETAIL_17860_OTHER_DEFENSE_SOURCE");
        string? weaponPath=Environment.GetEnvironmentVariable("AION_RETAIL_17860_WEAPON_DEFENSE_SOURCE");
        string? weaponHpPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_WEAPON_HP_FIXTURE");
        string? offPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_OFFHAND_FIXTURE");
        string? bonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_17860_ITEM_DEFENSE_BONUS_SOURCE");
        Skip.If(new[]{manifest,hpPath,hpBonusPath,permitPath,armorPath,shieldPath,otherPath,weaponPath,weaponHpPath,offPath,bonusPath}
            .Any(string.IsNullOrWhiteSpace),"Requires pinned retail item and skill sources");
        using var doc=JsonDocument.Parse(File.ReadAllBytes(manifest!));
        var asset=doc.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==17860);
        byte[] skill=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
        var hp=RetailNpcCurseArmorSource.Parse(File.ReadAllBytes(hpPath!),"72fa5bf580b1ace67426d6a15081531a75b7760962d601de59eed6fdb62a28ff");
        var itemBonus=RetailNpcCurseItemBonusSource.Parse(File.ReadAllBytes(hpBonusPath!),RetailNpcCurseItemBonusSource.ReviewedSourceSha);
        var permits=RetailNpcCurseClassPermitSource.Parse(File.ReadAllBytes(permitPath!),RetailNpcCurseClassPermitSource.ReviewedSha);
        var armor=RetailNpcArmorDefenseSource.ParseForOfflineFixture(File.ReadAllBytes(armorPath!),"ebd610658fd1225d1ec262cbf2971ba5a9d4954c8004f7739fe0456422e6a7ba");
        var shields=RetailNpcShieldDefenseSource.ParseForOfflineFixture(File.ReadAllBytes(shieldPath!),"5d31e7d11491ec75d4c2804fe59632e4566ea0228b4bddf7e1b4320f7da69ae6");
        var others=RetailNpcOtherEquipmentDefenseSource.ParseForOfflineFixture(File.ReadAllBytes(otherPath!),"53ebb035f0d1aa834638373b56ec991fc527cdf1ac1da56ab52533fd63eb3784");
        var weapons=RetailNpcWeaponDefenseSource.ParseForOfflineFixture(File.ReadAllBytes(weaponPath!),"ae8a1ebe3fa68fac4faf3b2b477d46e4e45a08fcf80e327a67483639477da11b");
        var weaponHp=RetailNpcCurseWeaponHpSource.Parse(File.ReadAllBytes(weaponHpPath!),"09c3411e58241d3b51b82d676841e4f2b09d29fffeb2114eeae7e115c8f6a53a");
        var offhand=RetailNpcCurseOffhandSource.Parse(File.ReadAllBytes(offPath!),"029a5388abcad6c7f750f0771f7096eb0a846f41b7b453ea8a6becf6f80a959a");
        var bonuses=RetailNpcItemDefenseBonusSource.ParseForOfflineFixture(File.ReadAllBytes(bonusPath!),"0bc098ba47c2f8d063b0afb47e1d4451d28abc594c6a4a2f5f510ade769ebcdd");
        var equipment=new Equipment(null!);
        var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        slots.Add((long)ItemSlot.TORSO,new Item(900001,new ItemTemplate {itemId=110301465,retailSourceName="lt_torso_a_m2_65a",itemGroup=ItemGroup.LT_TORSO,maxEnchant=0},1,true,(long)ItemSlot.TORSO));
        var snapshot=RetailNpcCurseEquipmentObservation.Capture(equipment,4,65,hp,itemBonus:itemBonus);
        var normal=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,true,0);
        var charge=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,true,0x200);
        var inputs=new RetailNpcPlayerDefenseDrainEquipmentProjection.ItemContext(0,null,true,RetailNpcArmorDefenseSource.Restriction.Computed,normal,charge);
        int calls=0;
        var result=RetailNpcPlayerDefenseDrainEquipmentProjection.ProjectForOfflineFixture(snapshot,skill,200,0,0,
            armor,shields,others,weapons,bonuses,permits,_=>{calls++;return inputs;});
        Assert.Equal(1,calls);Assert.False(result.GameplayReady);Assert.Equal(-10,result.RetailPercent);
        Assert.Empty(result.Equipment.Issues);Assert.Empty(result.ItemBonuses.Issues);
        Assert.Equal(395,result.Equipment.RevisedArmorSubtotal);Assert.Equal(167,result.ItemBonuses.Points);Assert.Equal(723,result.PhysicalDefense);
        var element=new RetailNpc17860DefenseEffect.ElementInput(500,0,0,-10000,10000);
        var elements=Enumerable.Repeat(element,6).ToArray();
        Assert.True(RetailNpcPlayerDefenseDrainEquipmentProjection.TryBuildEffectInputsForOfflineFixture(result,200,0,0,elements,out var effectInputs));
        Assert.NotNull(effectInputs);Assert.Equal(595u,effectInputs.PhysicalCurrent);Assert.Equal(395u,effectInputs.ArmorBasis);
        Assert.Equal(167,effectInputs.PhysicalFlat);
        Assert.Equal(723,RetailNpcPlayerDefenseDrainProjection.PhysicalDefense(effectInputs.PhysicalCurrent,effectInputs.ArmorBasis,-10,effectInputs.PhysicalFlat));
        Assert.False(RetailNpcPlayerDefenseDrainEquipmentProjection.TryBuildEffectInputsForOfflineFixture(result with {PhysicalDefense=724},200,0,0,elements,out _));
        Assert.False(RetailNpcPlayerDefenseDrainEquipmentProjection.TryBuildEffectInputsForOfflineFixture(result,200,0,0,elements[..5],out _));
        string? manifestHash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(manifestHash),"Requires pinned retail package hash");
        using(var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!),manifestHash!,Path.GetDirectoryName(manifest!)!))
        using(var h=BossAiHarness.For(220020000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build())
        {
            var npc=h.SpawnWithAi(212874,"retail_stat_base_fixture");
            var player=RetailNpcStatPeriodicTests.Player(h,220020000);player.GetCommonData().SetPlayerClass(PlayerClass.ASSASSIN);
            player.GetCommonData().SetLevel(65);
            player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_RESIST,0),
                new StatSetFunction(StatEnum.MAGICAL_DEFEND,0),new StatSetFunction(StatEnum.EARTH_RESISTANCE,0)]);
            var equipmentSlots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(player.GetEquipment()));
            var torso=slots[(long)ItemSlot.TORSO];equipmentSlots.Add((long)ItemSlot.TORSO,torso);
            var template=DataManager.SKILL_DATA.GetSkillTemplate(17860);var second=Assert.IsType<StatdownEffect>(template.GetEffectTemplate(2));
            var effect=new Effect(new Skill(template,npc,1,player,null!),player);
            var candidate=RetailNpc17860PrimaryProjection.ReadForOfflineFixture(skill);
            Assert.True(RetailNpcStatSkillCombat.TryPrepare17860PrimaryForOfflineFixture(effect,candidate,(_,high)=>high,out bool hit));
            Assert.True(hit);Assert.True(second.Calculate(effect,null,null));
            var stats=player.GetGameStats();
            bool removeDuringProjection=false,mutateStatsDuringProjection=false,readOwnDefense=false;
            var duringOwner=new TestStatOwner();
            RetailNpcCurseEquipmentObservation.Snapshot Observe(Player p)
                =>RetailNpcCurseEquipmentObservation.Capture(p.GetEquipment(),4,65,hp,itemBonus:itemBonus);
            RetailNpcPlayerDefenseDrainEquipmentProjection.Result Project(RetailNpcCurseEquipmentObservation.Snapshot observed)
            {
                var projected=RetailNpcPlayerDefenseDrainEquipmentProjection.ProjectForOfflineFixture(observed,skill,200,0,0,
                    armor,shields,others,weapons,bonuses,permits,_=>inputs);
                if(removeDuringProjection) {removeDuringProjection=false;equipmentSlots.Remove((long)ItemSlot.TORSO);}
                if(mutateStatsDuringProjection) {mutateStatsDuringProjection=false;stats.AddEffectOnly(duringOwner,[new StatAddFunction(StatEnum.MAGICAL_RESIST,1,true)]);}
                if(readOwnDefense) {readOwnDefense=false;_ = stats.GetStat(StatEnum.PHYSICAL_DEFENSE,1000);}
                return projected;
            }
            RetailNpc17860DefenseEffect.InputReceipt? Capture(Player p)
                =>RetailNpcPlayerDefenseDrainEquipmentProjection.CaptureEffectInputsForOfflineFixture(p,Observe,Project,200,0,0,elements);
            removeDuringProjection=true;Assert.Null(Capture(player));equipmentSlots.Add((long)ItemSlot.TORSO,torso);
            mutateStatsDuringProjection=true;Assert.Null(Capture(player));stats.EndEffect(duringOwner);
            int before=stats.GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent();
            RetailNpc17860DefenseEffect.AttachReceiptForOfflineFixture(effect,skill,Capture,p=>ReferenceEquals(p,player));
            second.ApplyEffect(effect);
            Assert.Same(effect,player.GetEffectController().GetAbnormalEffect(effect.GetStack()));
            Assert.Equal(before-39,stats.GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
            readOwnDefense=true;
            Assert.Throws<InvalidOperationException>(()=>stats.GetStat(StatEnum.PHYSICAL_DEFENSE,1000));
            Assert.Equal(before-39,stats.GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
            var prior=Assert.IsType<RetailNpc17860DefenseEffect.InputReceipt>(Capture(player));
            Assert.True(prior.Current(player));
            equipmentSlots.Remove((long)ItemSlot.TORSO);
            Assert.False(prior.Current(player));
            Assert.Equal(before,stats.GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
            var unknown=new Item(900999,new ItemTemplate {itemId=199999999,retailSourceName="unknown_torso",itemGroup=ItemGroup.LT_TORSO,maxEnchant=0},1,true,(long)ItemSlot.TORSO);
            equipmentSlots.Add((long)ItemSlot.TORSO,unknown);
            Assert.Throws<InvalidOperationException>(()=>stats.GetStat(StatEnum.PHYSICAL_DEFENSE,1000));
            equipmentSlots.Remove((long)ItemSlot.TORSO);
            equipmentSlots.Add((long)ItemSlot.TORSO,torso);
            Assert.Equal(before-39,stats.GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
            prior=Assert.IsType<RetailNpc17860DefenseEffect.InputReceipt>(Capture(player));
            var extraOwner=new TestStatOwner();
            stats.AddEffectOnly(extraOwner,[new StatAddFunction(StatEnum.MAGICAL_RESIST,1,true)]);
            Assert.False(prior.Current(player));
            stats.EndEffect(extraOwner);
            Assert.False(prior.Current(player)); // Add/remove is still a changed version.
            Assert.Equal(before-39,stats.GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
            var retiredFunction=Assert.Single(stats.GetStatsSorted(StatEnum.PHYSICAL_DEFENSE),f=>ReferenceEquals(f.GetOwner(),effect));
            var replacement=new Effect(new Skill(template,npc,1,player,null!),player);
            Assert.True(RetailNpcStatSkillCombat.TryPrepare17860PrimaryForOfflineFixture(replacement,candidate,(_,high)=>high,out hit));
            Assert.True(hit);Assert.True(second.Calculate(replacement,null,null));
            RetailNpc17860DefenseEffect.AttachReceiptForOfflineFixture(replacement,skill,Capture,p=>ReferenceEquals(p,player));
            second.ApplyEffect(replacement);
            Assert.True(effect.RetailHasEnded);
            Assert.Same(replacement,player.GetEffectController().GetAbnormalEffect(replacement.GetStack()));
            Assert.Throws<InvalidOperationException>(()=>retiredFunction.GetValue());
            Assert.DoesNotContain(stats.GetStatsSorted(StatEnum.PHYSICAL_DEFENSE),f=>ReferenceEquals(f.GetOwner(),effect));
            Assert.Equal(before-39,stats.GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
            var liveFunction=Assert.Single(stats.GetStatsSorted(StatEnum.PHYSICAL_DEFENSE),f=>ReferenceEquals(f.GetOwner(),replacement));
            int health=player.GetLifeStats().GetCurrentHp();Assert.True(health>0);
            var hpField=typeof(CreatureLifeStats).GetField("currentHp",BindingFlags.Instance|BindingFlags.NonPublic)!;
            lock(player.GetLifeStats())hpField.SetValue(player.GetLifeStats(),0); // Isolated boundary; bypasses death services.
            try {Assert.Throws<InvalidOperationException>(()=>liveFunction.GetValue());}
            finally {lock(player.GetLifeStats())hpField.SetValue(player.GetLifeStats(),health);}
            Assert.Equal(before-39,stats.GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
            replacement.EndEffect(false);Assert.Null(player.GetEffectController().GetAbnormalEffect(replacement.GetStack()));
            Assert.Throws<InvalidOperationException>(()=>liveFunction.GetValue());
            Assert.Equal(before,stats.GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
            var rejected=new Effect(new Skill(template,npc,1,player,null!),player);
            Assert.True(RetailNpcStatSkillCombat.TryPrepare17860PrimaryForOfflineFixture(rejected,candidate,(_,high)=>high,out hit));
            Assert.True(hit);Assert.True(second.Calculate(rejected,null,null));
            bool approved=true;
            RetailNpc17860DefenseEffect.AttachReceiptForOfflineFixture(rejected,skill,Capture,p=>approved&&ReferenceEquals(p,player));
            approved=false;
            Assert.Throws<InvalidOperationException>(()=>second.ApplyEffect(rejected));
            Assert.True(rejected.RetailHasEnded);
            Assert.Null(player.GetEffectController().GetAbnormalEffect(rejected.GetStack()));
            Assert.DoesNotContain(stats.GetStatsSorted(StatEnum.PHYSICAL_DEFENSE),f=>ReferenceEquals(f.GetOwner(),rejected));
            Assert.Equal(before,stats.GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
            equipmentSlots.Clear();Assert.Empty(h.Clock.Faults);
        }
        ushort hostTemplateBase=checked((ushort)PlayerClass.ASSASSIN.CreateStatsTemplate(65).GetPdef());
        Assert.Equal((ushort)0,hostTemplateBase);
        var hostBaseProjection=RetailNpcPlayerDefenseDrainEquipmentProjection.ProjectForOfflineFixture(snapshot,skill,hostTemplateBase,0,0,
            armor,shields,others,weapons,bonuses,permits,_=>inputs);
        Assert.Equal(523,hostBaseProjection.PhysicalDefense); // Subsequent retail actor state remains unproved.
        var lowLevel=snapshot with {PlayerLevel=55};
        var revised=RetailNpcPlayerDefenseDrainEquipmentProjection.ProjectForOfflineFixture(lowLevel,skill,200,0,0,
            armor,shields,others,weapons,bonuses,permits,_=>inputs);
        Assert.Equal(316,revised.Equipment.RevisedArmorSubtotal);Assert.Equal(133,revised.ItemBonuses.Points);Assert.Equal(618,revised.PhysicalDefense);
        var conditioned=lowLevel with {Items=ImmutableArray.Create(Assert.Single(lowLevel.Items) with {ChargePoints=1})};
        var charged=RetailNpcPlayerDefenseDrainEquipmentProjection.ProjectForOfflineFixture(conditioned,skill,200,0,0,
            armor,shields,others,weapons,bonuses,permits,_=>inputs);
        Assert.Empty(charged.Equipment.Issues);Assert.Empty(charged.ItemBonuses.Issues);
        Assert.Equal(316,charged.Equipment.RevisedArmorSubtotal);Assert.Equal(148,charged.ItemBonuses.Points);Assert.Equal(633,charged.PhysicalDefense);
        var withUnknown=snapshot with {Items=snapshot.Items.Add(Assert.Single(snapshot.Items) with {ObjectId=900002,ItemId=199999999})};
        var incomplete=RetailNpcPlayerDefenseDrainEquipmentProjection.ProjectForOfflineFixture(withUnknown,skill,200,0,0,
            armor,shields,others,weapons,bonuses,permits,_=>inputs);
        Assert.Null(incomplete.PhysicalDefense);Assert.NotEmpty(incomplete.Equipment.Issues);Assert.NotEmpty(incomplete.ItemBonuses.Issues);
        Assert.False(RetailNpcPlayerDefenseDrainEquipmentProjection.TryBuildEffectInputsForOfflineFixture(incomplete,200,0,0,elements,out _));
        var missing=RetailNpcPlayerDefenseDrainEquipmentProjection.ProjectForOfflineFixture(snapshot,skill,200,0,0,
            armor,shields,others,weapons,bonuses,permits,_=>null);
        Assert.Null(missing.PhysicalDefense);Assert.NotEmpty(missing.Equipment.Issues);Assert.NotEmpty(missing.ItemBonuses.Issues);
        var badSkill=(byte[])skill.Clone();badSkill[^1]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcPlayerDefenseDrainEquipmentProjection.ProjectForOfflineFixture(snapshot,badSkill,200,0,0,
            armor,shields,others,weapons,bonuses,permits,_=>inputs));
        slots.Clear();
        slots.Add((long)ItemSlot.MAIN_HAND,new Item(900003,new ItemTemplate {itemId=100000001,retailSourceName="sword_circulous",itemGroup=ItemGroup.SWORD,maxEnchant=15},1,true,(long)ItemSlot.MAIN_HAND));
        slots.Add((long)ItemSlot.SUB_HAND,new Item(900004,new ItemTemplate {itemId=100200001,retailSourceName="dagger_polymorph_01a",itemGroup=ItemGroup.DAGGER,maxEnchant=10},1,true,(long)ItemSlot.SUB_HAND));
        var dual=RetailNpcCurseEquipmentObservation.Capture(equipment,1,65,hp,offhand,weaponHp,itemBonus:itemBonus);
        var dualResult=RetailNpcPlayerDefenseDrainEquipmentProjection.ProjectForOfflineFixture(dual,skill,200,0,0,
            armor,shields,others,weapons,bonuses,permits,x=>inputs with {Normal=normal with {BonusFlags=x.OccupiedSlot==(long)ItemSlot.SUB_HAND?8:0}});
        Assert.Equal(200,dualResult.PhysicalDefense);Assert.Equal(2,dualResult.Equipment.Entries.Length);Assert.Equal(2,dualResult.ItemBonuses.Entries.Length);
        slots.Clear();long both=(long)ItemSlot.MAIN_OR_SUB;
        var greatsword=new Item(900005,new ItemTemplate {itemId=100900001,retailSourceName="2hsword_test_01",itemGroup=ItemGroup.GREATSWORD,maxEnchant=10},1,true,both);
        slots.Add((long)ItemSlot.MAIN_HAND,greatsword);slots.Add((long)ItemSlot.SUB_HAND,greatsword);
        var two=RetailNpcCurseEquipmentObservation.Capture(equipment,1,65,hp,offhand,weaponHp,itemBonus:itemBonus);
        var twoResult=RetailNpcPlayerDefenseDrainEquipmentProjection.ProjectForOfflineFixture(two,skill,200,0,0,
            armor,shields,others,weapons,bonuses,permits,_=>inputs with {Normal=normal with {BonusFlags=0x100}});
        Assert.Equal(200,twoResult.PhysicalDefense);Assert.Single(twoResult.Equipment.Entries);Assert.Single(twoResult.ItemBonuses.Entries);
        slots.Clear();
        var mainTemplate=new ItemTemplate {itemId=100900898,retailSourceName="2hsword_n_l1_r_60a",itemGroup=ItemGroup.GREATSWORD,maxEnchant=10};
        var fusedTemplate=new ItemTemplate {itemId=100900855,retailSourceName="2hsword_n_e1_55c",itemGroup=ItemGroup.GREATSWORD,maxEnchant=15};
        var fusedItem=new Item(900006,mainTemplate,1,true,both);
        typeof(Item).GetField("fusionedItemTemplate",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(fusedItem,fusedTemplate);
        slots.Add((long)ItemSlot.MAIN_HAND,fusedItem);slots.Add((long)ItemSlot.SUB_HAND,fusedItem);
        var fused=RetailNpcCurseEquipmentObservation.Capture(equipment,1,50,hp,offhand,weaponHp,itemBonus:itemBonus);
        var fusedNormal=normal with {BonusFlags=0x80};
        var fusion=new RetailNpcCurseEquipmentObservation.FusionProjectionContext(true,true,true,1,0,null,fusedNormal,fusedNormal with {BonusFlags=0x200});
        var fusedInputs=inputs with {Normal=normal with {BonusFlags=0x100},Fusion=fusion};
        var fusedResult=RetailNpcPlayerDefenseDrainEquipmentProjection.ProjectForOfflineFixture(fused,skill,200,0,0,
            armor,shields,others,weapons,bonuses,permits,_=>fusedInputs);
        Assert.Equal(200,fusedResult.PhysicalDefense);Assert.Single(fusedResult.Equipment.Entries);Assert.Single(fusedResult.ItemBonuses.Entries);
        var missingFusion=RetailNpcPlayerDefenseDrainEquipmentProjection.ProjectForOfflineFixture(fused,skill,200,0,0,
            armor,shields,others,weapons,bonuses,permits,_=>fusedInputs with {Fusion=null});
        Assert.Null(missingFusion.PhysicalDefense);Assert.NotEmpty(missingFusion.Equipment.Issues);Assert.NotEmpty(missingFusion.ItemBonuses.Issues);
        var forged=fused with {Items=fused.Items.Select(x=>x with {Fusion=x.Fusion! with {ItemId=123}}).ToImmutableArray()};
        Assert.Null(RetailNpcPlayerDefenseDrainEquipmentProjection.ProjectForOfflineFixture(forged,skill,200,0,0,
            armor,shields,others,weapons,bonuses,permits,_=>fusedInputs).PhysicalDefense);
    }
}
