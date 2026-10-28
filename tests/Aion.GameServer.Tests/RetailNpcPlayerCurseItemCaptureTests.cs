using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Stats.Listeners;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Text.Json;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcPlayerCurseItemCaptureTests
{
    private sealed class OtherOwner:IStatOwner;
    private sealed class UnreviewedPlayerGameStats(Player owner):PlayerGameStats(owner);
    private sealed class TimedSkill(SkillTemplate template,Creature caster,Creature target,int level):Skill(template,caster,level,target,null!)
    { internal void SetWord(int word){SetClientHitTime(word);UpdateHitTime(false);} }

    [SkippableFact]
    public void EquippedSourceMatchedRingsContributeCurseResistanceAndUnknownOwnersReject()
    {
        string? directory=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_CATALOG");
        string? buffs=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_PLAYER_RESISTANCE_CATALOG");
        string? directPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_DIRECT_ITEM_CATALOG");
        string? absolutePath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ABSOLUTE_CATALOG");
        string? hostAbsolute=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HOST_ABSOLUTE");
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? manifestHash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(directory)||string.IsNullOrWhiteSpace(buffs)||string.IsNullOrWhiteSpace(directPath)
            ||string.IsNullOrWhiteSpace(absolutePath)||string.IsNullOrWhiteSpace(hostAbsolute)
            ||string.IsNullOrWhiteSpace(manifest)||string.IsNullOrWhiteSpace(manifestHash),"Requires pinned retail source packages");
        byte[] bytes=File.ReadAllBytes(Path.Combine(directory!,"items.json"));
        const string hash="749064aed3dd1896dc51fb6622852015171e71370c0525e0f638622cc6a97701";
        var catalog=RetailNpcPlayerCurseItemCapture.Catalog.LoadForOfflineFixture(bytes,hash,directory!);
        var directBytes=File.ReadAllBytes(directPath!);
        const string directHash="31cf2bda19ca3b306c97838a331fcc014e5d52a01eafee3f1a5c61c9448f3fb4";
        var direct=RetailNpcPlayerCurseItemCapture.DirectCatalog.LoadForOfflineFixture(directBytes,directHash);
        var changedDirect=(byte[])directBytes.Clone();changedDirect[0]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcPlayerCurseItemCapture.DirectCatalog.LoadForOfflineFixture(changedDirect,directHash));
        Assert.True(direct.TryGet(122000983,out var reviewedRing));Assert.Equal(100,reviewedRing.CurseFlat);
        Assert.True(direct.TryGet(120000012,out var reviewedEarring));Assert.Null(reviewedEarring.CurseFlat);
        var buffCatalog=RetailNpcPlayerResistanceCapture.Catalog.LoadForOfflineFixture(
            File.ReadAllBytes(Path.Combine(buffs!,"catalog.json")),
            "bb44e35a26fb715b8f12a3d26b0b3cd6c53e0aecca5035e6162ba9cf90b4a990",buffs!);
        string? protectionPath=Environment.GetEnvironmentVariable("AION_RETAIL_PROTECTION_RESISTANCE_CATALOG");
        if(!string.IsNullOrWhiteSpace(protectionPath))
            buffCatalog=buffCatalog.WithRetailOnlyForOfflineFixture(File.ReadAllBytes(Path.Combine(protectionPath,"catalog.json")),
                "a7e2ab26a3df5b1bdbf375fc1f79d52bfdc81c72a2d0a162861b0b1540ee0e1d",protectionPath);
        string? avatarPath=Environment.GetEnvironmentVariable("AION_RETAIL_AVATAR_RESISTANCE_CATALOG");
        if(!string.IsNullOrWhiteSpace(avatarPath))
            buffCatalog=buffCatalog.WithLinkedForOfflineFixture(File.ReadAllBytes(Path.Combine(avatarPath,"catalog.json")),
                "739c439fea8ec195c5762164641d18a6137509976a1901e904dccf1affa0919e",avatarPath);
        var absoluteCatalog=RetailNpcPlayerCurseAbsoluteCapture.Catalog.LoadForOfflineFixture(
            File.ReadAllBytes(Path.Combine(absolutePath!,"catalog.json")),
            "a4837b798a026e14b573974d7a644c4ddf719b7a64f69ed52f7cf265ecef3c95",absolutePath!);
        var altered=(byte[])bytes.Clone();altered[0]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcPlayerCurseItemCapture.Catalog.LoadForOfflineFixture(altered,hash,directory!));
        using var package=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!),manifestHash!,Path.GetDirectoryName(manifest!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        string? hostItems=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HOST_ITEMS");
        Skip.If(string.IsNullOrWhiteSpace(hostItems),"Requires the existing host item data");
        var records=new List<XElement>();
        using(var reader=XmlReader.Create(hostItems!))
            while(reader.Read())
                if(reader.NodeType==XmlNodeType.Element && reader.Name=="item_template"
                    && reader.GetAttribute("id") is "122000983" or "122000984" or "120000012" or "110100009")
                { using var subtree=reader.ReadSubtree();records.Add(XElement.Load(subtree)); }
        Assert.Equal(4,records.Count);
        using var xml=new StringReader(new XElement("item_templates",records).ToString());
        var itemData=(ItemData)new XmlSerializer(typeof(ItemData)).Deserialize(xml)!;itemData.AfterUnmarshal(null!);
        typeof(StaticData).GetProperty(nameof(StaticData.ItemDataDh))!.SetValue(h.Data.StaticData,itemData);
        var itemSets=new ItemSetData {itemsetList=[]};itemSets.AfterUnmarshal(null!);
        typeof(StaticData).GetProperty(nameof(StaticData.ItemSetDataDh))!.SetValue(h.Data.StaticData,itemSets);
        using var absoluteXml=File.OpenRead(hostAbsolute!);
        var absoluteData=(AbsoluteStatsData)new XmlSerializer(typeof(AbsoluteStatsData)).Deserialize(absoluteXml)!;
        absoluteData.AfterUnmarshal(null!);
        typeof(StaticData).GetProperty(nameof(StaticData.AbsoluteStatsDataDh))!.SetValue(h.Data.StaticData,absoluteData);
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        var mastery=DataManager.SKILL_DATA.GetMasterySkills(itemData.GetItemTemplate(110100009).GetItemGroup());
        player.SetSkillList(new PlayerSkillList(mastery.Select(id=>new PlayerSkillEntry(id,1,0,IPersistable.PersistentState.NEW)).ToList()));
        Item Equip(int id,int objectId,ItemSlot slot)
        {
            var item=new Item(objectId,DataManager.ITEM_DATA.GetItemTemplate(id),1,true,(long)slot);
            player.GetEquipment().OnLoadHandler(item);
            ItemEquipmentListener.OnItemEquipment(item,player);
            return item;
        }
        var first=Equip(122000983,900001,ItemSlot.RING_LEFT);
        Assert.True(RetailNpcPlayerCurseItemCapture.TryCaptureForOfflineFixture(player,catalog,out var one));
        Assert.Equal(new RetailNpcStatSpellDrain.CurseModifiers(0,100),one);
        var earring=Equip(120000012,900003,ItemSlot.EARRINGS_LEFT);
        var armor=Equip(110100009,900004,ItemSlot.TORSO);
        Assert.Contains(earring,player.GetEquipment().GetEquippedItemsWithoutStigma());
        Assert.Contains(armor,player.GetEquipment().GetEquippedItemsWithoutStigma());
        Assert.False(RetailNpcPlayerCurseItemCapture.TryCaptureForOfflineFixture(player,catalog,out _));
        Assert.True(RetailNpcPlayerCurseItemCapture.TryCaptureForOfflineFixture(player,catalog,out var withOrdinary,direct));
        Assert.Equal(one,withOrdinary);
        earring.SetTempering(1);
        Assert.False(RetailNpcPlayerCurseItemCapture.TryCaptureForOfflineFixture(player,catalog,out _,direct));
        earring.SetTempering(0);
        first.SetTempering(1);
        Assert.False(RetailNpcPlayerCurseItemCapture.TryCaptureForOfflineFixture(player,catalog,out _,direct));
        first.SetTempering(0);
        var buffTemplate=DataManager.SKILL_DATA.GetSkillTemplate(2767);
        var buffStat=Assert.IsType<StatupEffect>(buffTemplate.GetEffectTemplate(3));
        var buff=new Effect(player,player,buffTemplate,1);buff.AddSuccessEffect(buffStat);buff.ApplyEffect();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");player.GetCommonData().SetLevel(38);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),
            new StatSetFunction(StatEnum.CURSE_RESISTANCE_PENETRATION,0)]);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_RESIST,0)]);
        Assert.True(RetailNpcStatCatalog.TryGet(npc,out var profile));
        Assert.True(RetailNpcStatCatalog.TryGetSkill(profile,16596,38,out var source));
        var vampireTemplate=DataManager.SKILL_DATA.GetSkillTemplate(16596);
        var firstHit=Assert.IsType<SpellAtkDrainInstantEffect>(vampireTemplate.GetEffectTemplate(1));
        var curse=Assert.IsType<CurseEffect>(vampireTemplate.GetEffectTemplate(2));
        var timed=new TimedSkill(vampireTemplate,npc,player,38);timed.SetWord(0);
        bool constructedBaseUnchanged=true;int baseReadsBeforeRetirement=-1;
        bool ConstructedBaseCurrent(Player target)
        {
            if(!ReferenceEquals(target,player)||!constructedBaseUnchanged)return false;
            return baseReadsBeforeRetirement<0||baseReadsBeforeRetirement-- > 0;
        }
        constructedBaseUnchanged=false;
        Assert.False(RetailNpcPlayerResistanceSnapshot.TryCaptureReceiptFromConstructedRetailBaseForOfflineFixture(player,
            ConstructedBaseCurrent,absoluteCatalog,catalog,direct,buffCatalog,out _));
        constructedBaseUnchanged=true;
        var originalStats=player.GetGameStats();
        player.SetGameStats(new UnreviewedPlayerGameStats(player));
        Assert.False(RetailNpcPlayerResistanceSnapshot.TryCaptureReceiptFromMappedHostSeedForOfflineFixture(player,
            absoluteCatalog,catalog,direct,buffCatalog,out _));
        player.SetGameStats(originalStats);
        Effect Prepared()
        {
            var effect=new Effect(timed,player);
            effect.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
            RetailNpcStatSpellDrain.Prepare(effect,firstHit,new(100,false,Curse:new(source.Sha256,FixtureResistanceReceipt:target=>
            {
                if(!RetailNpcPlayerResistanceSnapshot.TryCaptureReceiptFromConstructedRetailBaseForOfflineFixture(target,
                    ConstructedBaseCurrent,absoluteCatalog,catalog,direct,buffCatalog,out var input))
                    throw new InvalidOperationException("Player resistance input retired");
                Assert.NotNull(input);Assert.True(input.IsCurrent(target));
                constructedBaseUnchanged=false;
                Assert.False(input.IsCurrent(target));Assert.False(input.HasEquivalentInputForOfflineFixture(target));
                constructedBaseUnchanged=true;
                baseReadsBeforeRetirement=1;Assert.False(input.IsCurrent(target));
                baseReadsBeforeRetirement=1;Assert.False(input.HasEquivalentInputForOfflineFixture(target));
                baseReadsBeforeRetirement=-1;
                target.GetCommonData().SetLevel(39);
                Assert.False(input.IsCurrent(target));Assert.False(input.HasEquivalentInputForOfflineFixture(target));
                target.GetCommonData().SetLevel(38);Assert.True(input.IsCurrent(target));
                return input;
            })));
            effect.AddSuccessEffect(firstHit);return effect;
        }
        var resisted=Prepared();var rolls=new Queue<int>([100,1000,200]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(resisted,curse,(_,_)=>rolls.Dequeue()));
        Assert.False(resisted.IsInSuccessEffects(2));Assert.Empty(rolls);
        var admitted=Prepared();rolls=new Queue<int>([100,1000,201]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(admitted,curse,(_,_)=>rolls.Dequeue()));
        Assert.True(admitted.IsInSuccessEffects(2));Assert.Empty(rolls);
        if(!string.IsNullOrWhiteSpace(protectionPath))
        {
            var protectionTemplate=DataManager.SKILL_DATA.GetSkillTemplate(8874);
            var protectionStat=Assert.IsType<StatupEffect>(protectionTemplate.GetEffectTemplate(2));
            var protection=new Effect(player,player,protectionTemplate,8);
            protection.AddSuccessEffect(protectionStat);protection.ApplyEffect();
            var protectedRoll=Prepared();rolls=new Queue<int>([100,1000,400]);
            Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(protectedRoll,curse,(_,_)=>rolls.Dequeue()));
            Assert.False(protectedRoll.IsInSuccessEffects(2));Assert.Empty(rolls);
            var aboveProtection=Prepared();rolls=new Queue<int>([100,1000,401]);
            Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(aboveProtection,curse,(_,_)=>rolls.Dequeue()));
            Assert.True(aboveProtection.IsInSuccessEffects(2));Assert.Empty(rolls);
            protection.EndEffect();
        }
        if(!string.IsNullOrWhiteSpace(avatarPath))
        {
            var avatarTemplate=DataManager.SKILL_DATA.GetSkillTemplate(11885);
            var launcher=Assert.IsType<SkillLauncherEffect>(avatarTemplate.GetEffectTemplate(1));
            var shape=Assert.IsType<ShapeChangeEffect>(avatarTemplate.GetEffectTemplate(2));
            var avatar=new Effect(player,player,avatarTemplate,1);
            avatar.AddSuccessEffect(launcher);avatar.AddSuccessEffect(shape);avatar.ApplyEffect();
            var child=Assert.Single(player.GetEffectController().GetAllEffects(),e=>e.GetSkillId()==11912);
            var protectedRoll=Prepared();rolls=new Queue<int>([100,1000,1000]);
            Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(protectedRoll,curse,(_,_)=>rolls.Dequeue()));
            Assert.False(protectedRoll.IsInSuccessEffects(2));Assert.Empty(rolls);
            avatar.EndEffect();child.EndEffect();
        }
        var mutated=Prepared();rolls=new Queue<int>([100,1000,201]);
        var temporaryOwner=new OtherOwner();
        Assert.Throws<InvalidOperationException>(()=>RetailNpcStatSpellDrain.TryCalculateCurse(mutated,curse,(_,_)=>
        {
            int value=rolls.Dequeue();
            if(rolls.Count==0)
            {
                player.GetGameStats().AddEffectOnly(temporaryOwner,[new StatAddFunction(StatEnum.ABNORMAL_RESISTANCE_ALL,20,true)]);
                player.GetGameStats().EndEffect(temporaryOwner);
            }
            return value;
        }));
        Assert.False(mutated.IsInSuccessEffects(2));Assert.Empty(rolls);
        var changedItem=Prepared();rolls=new Queue<int>([100,1000,201]);
        try
        {
            Assert.Throws<InvalidOperationException>(()=>RetailNpcStatSpellDrain.TryCalculateCurse(changedItem,curse,(_,_)=>
            {
                int value=rolls.Dequeue();if(rolls.Count==0)earring.SetTempering(1);return value;
            }));
            Assert.False(changedItem.IsInSuccessEffects(2));Assert.Empty(rolls);
        }
        finally { earring.SetTempering(0); }
        // Exercise source-backed resistance through the ordinary prepared
        // primary/curse/heal path, with explicitly synthetic resource maxima.
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,100000),new StatSetFunction(StatEnum.MAXMP,1000)]);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(80000);player.GetLifeStats().SetCurrentMp(600);
        Assert.Equal(100000,player.GetLifeStats().GetMaxHp());
        int fixtureMaxMp=player.GetLifeStats().GetMaxMp();Assert.True(fixtureMaxMp>=1000);
        npc.GetLifeStats().SetCurrentHp(100);
        using(var manifestDoc=JsonDocument.Parse(File.ReadAllBytes(manifest!)))
        {
            var asset=manifestDoc.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==16596);
            var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(16596,File.ReadAllBytes(asset.GetProperty("path").GetString()!));
            var full=new Effect(timed,player);int captures=0;
            int newMaxMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,fixtureMaxMp,fixtureMaxMp,-35);
            RetailNpcPlayerResistanceSnapshot.Receipt? observed=null;
            Func<Player,Effect,RetailNpcCurseResourceOwner.Plan?> plan=(target,owner)=>
            {
                var checks=RetailNpcCurseStatWitness.Create(target,owner,p=>observed?.HasEquivalentInputForOfflineFixture(p)==true,
                    -35000,newMaxMp-fixtureMaxMp);
                return new(100000,100000,0,fixtureMaxMp,fixtureMaxMp,0,checks.Before,checks.After,checks.CommitStamp);
            };
            Func<Player,RetailNpcPlayerResistanceSnapshot.Receipt?> capture=target=>
            {
                captures++;
                // No fixture baseline callback is supplied for the mapped host path.
                constructedBaseUnchanged=false;
                bool captured=RetailNpcPlayerResistanceSnapshot.TryCaptureReceiptFromMappedHostSeedForOfflineFixture(target,
                    absoluteCatalog,catalog,direct,buffCatalog,out observed);
                constructedBaseUnchanged=true;
                if(!captured)return null;
                Assert.NotNull(observed);Assert.True(observed.IsCurrent(target));
                var capturedStats=target.GetGameStats();target.SetGameStats(new UnreviewedPlayerGameStats(target));
                Assert.False(observed.IsCurrent(target));Assert.False(observed.HasEquivalentInputForOfflineFixture(target));
                target.SetGameStats(capturedStats);Assert.True(observed.IsCurrent(target));
                target.GetCommonData().SetLevel(39);
                Assert.False(observed.IsCurrent(target));Assert.False(observed.HasEquivalentInputForOfflineFixture(target));
                target.GetCommonData().SetLevel(38);Assert.True(observed.IsCurrent(target));
                return observed;
            };
            RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(full,candidate,plan,(_,hi)=>hi,capture);
            full.Initialize();Assert.True(full.IsInSuccessEffects(1));Assert.True(full.IsInSuccessEffects(2));Assert.Equal(1,captures);
            int damage=full.GetReserveds(firstHit.Position).GetValue();Assert.InRange(damage,1,25000);
            full.ApplyEffect();
            int afterCurse=RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,100000,65000,80000-damage);
            Assert.Equal(65000,player.GetLifeStats().GetMaxHp());Assert.Equal(afterCurse,player.GetLifeStats().GetCurrentHp());
            Assert.Equal(newMaxMp,player.GetLifeStats().GetMaxMp());
            Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Mp,fixtureMaxMp,newMaxMp,600),
                player.GetLifeStats().GetCurrentMp());
            h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
            Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,100)),npc.GetLifeStats().GetCurrentHp());
            curse.EndEffect(full);Assert.Equal(100000,player.GetLifeStats().GetMaxHp());
            Assert.Equal(fixtureMaxMp,player.GetLifeStats().GetMaxMp());full.EndEffect();

            // Removing the observed buff after the resistance roll must not
            // commit the stale curse resource change. The landed hit and its
            // separately scheduled heal still belong to the prepared primary.
            player.GetLifeStats().SetCurrentHp(80000);player.GetLifeStats().SetCurrentMp(600);npc.GetLifeStats().SetCurrentHp(100);
            var stale=new Effect(timed,player);
            RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(stale,candidate,plan,(_,hi)=>hi,capture);
            stale.Initialize();Assert.True(stale.IsInSuccessEffects(2));int staleDamage=stale.GetReserveds(firstHit.Position).GetValue();
            buff.EndEffect();
            var failure=Assert.Throws<Exception>(()=>stale.ApplyEffect());Assert.IsType<InvalidOperationException>(failure.InnerException);
            Assert.True(stale.RetailHasEnded);Assert.Equal(100000,player.GetLifeStats().GetMaxHp());
            Assert.Equal(80000-staleDamage,player.GetLifeStats().GetCurrentHp());Assert.Equal(600,player.GetLifeStats().GetCurrentMp());
            h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
            Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(staleDamage,100)),npc.GetLifeStats().GetCurrentHp());
            buff=new Effect(player,player,buffTemplate,1);buff.AddSuccessEffect(buffStat);buff.ApplyEffect();
        }
        Effect StartAbsolute(int id)
        {
            var template=DataManager.SKILL_DATA.GetSkillTemplate(id);
            var selected=Assert.IsAssignableFrom<AbstractAbsoluteStatEffect>(template.GetEffectTemplate(2));
            var effect=new Effect(player,player,template,1);effect.AddSuccessEffect(selected);effect.ApplyEffect();return effect;
        }
        var absoluteBuff=StartAbsolute(21055);
        var overridden=Prepared();rolls=new Queue<int>([100,1000,1000]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(overridden,curse,(_,_)=>rolls.Dequeue()));
        Assert.False(overridden.IsInSuccessEffects(2));Assert.Empty(rolls);
        var absoluteDebuff=StartAbsolute(21218);
        Assert.True(RetailNpcPlayerCurseAbsoluteCapture.TryCaptureReceiptForOfflineFixture(player,absoluteCatalog,out var receipt));
        Assert.True(receipt!.IsCurrent(player));
        var absoluteFunction=Assert.Single(player.GetGameStats().GetStatsSorted(StatEnum.CURSE_RESISTANCE),
            f=>ReferenceEquals(f.GetOwner(),absoluteBuff));
        var absoluteLeaf=Assert.IsType<StatAbsFunction>(Assert.IsType<StatFunctionProxy>(absoluteFunction).GetProxiedFunction());
        int oldValue=absoluteLeaf.Value;
        try { absoluteLeaf.Value=oldValue-1;Assert.False(receipt.IsCurrent(player)); }
        finally { absoluteLeaf.Value=oldValue; }
        Assert.True(receipt.IsCurrent(player));
        var suppressed=Prepared();rolls=new Queue<int>([100,1000,200]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(suppressed,curse,(_,_)=>rolls.Dequeue()));
        Assert.False(suppressed.IsInSuccessEffects(2));Assert.Empty(rolls);
        var afterSuppression=Prepared();rolls=new Queue<int>([100,1000,201]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(afterSuppression,curse,(_,_)=>rolls.Dequeue()));
        Assert.True(afterSuppression.IsInSuccessEffects(2));Assert.Empty(rolls);
        absoluteDebuff.EndEffect();Assert.False(receipt.IsCurrent(player));absoluteBuff.EndEffect();buff.EndEffect();
        var second=Equip(122000984,900002,ItemSlot.RING_RIGHT);
        Assert.True(RetailNpcPlayerCurseItemCapture.TryCaptureForOfflineFixture(player,catalog,out var both,direct));
        Assert.Equal(new RetailNpcStatSpellDrain.CurseModifiers(0,300),both);
        var other=new OtherOwner();player.GetGameStats().AddEffectOnly(other,[new StatAddFunction(StatEnum.CURSE_RESISTANCE,20,true)]);
        Assert.False(RetailNpcPlayerCurseItemCapture.TryCaptureForOfflineFixture(player,catalog,out _,direct));
        player.GetGameStats().EndEffect(other);
        first.SetEquipped(false);
        Assert.False(RetailNpcPlayerCurseItemCapture.TryCaptureForOfflineFixture(player,catalog,out _,direct));
        first.SetEquipped(true);
        Assert.True(RetailNpcPlayerCurseItemCapture.TryCaptureForOfflineFixture(player,catalog,out _,direct));
        player.GetGameStats().EndEffect(second);
        Assert.False(RetailNpcPlayerCurseItemCapture.TryCaptureForOfflineFixture(player,catalog,out _,direct));
        Assert.Empty(h.Clock.Faults);
    }
}
