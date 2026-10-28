using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using ChangeFunc = Aion.GameServer.SkillEngine.Change.Func;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Offline witness for the selected 5.8 9942 all-primary reduction.
/// It does not establish complete player primary state or admit gameplay.</summary>
internal static class RetailNpcPlayerPrimaryAllStatdownCapture
{
    private const string SourceHash="2c9f809e88e348c2831f59c9f4a11bf714a2a42d0d18bb1f74da04fc7886ca5d";
    private static readonly StatEnum[] PrimaryStats=[StatEnum.HEALTH,StatEnum.WILL,StatEnum.ALLPARA,StatEnum.STRVIT,StatEnum.KNOWIL];

    internal static void VerifySourceForOfflineFixture(ReadOnlySpan<byte> raw)
    {
        if(Convert.ToHexStringLower(SHA256.HashData(raw))!=SourceHash)throw new InvalidDataException("All-primary statdown source hash differs");
        using var reader=XmlReader.Create(new StringReader(new UnicodeEncoding(false,false,true).GetString(raw)),
            new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null});
        var root=XElement.Load(reader);
        string? Field(string name)
        {
            var nodes=root.Elements(name).ToArray();
            return nodes is {Length:1} && !nodes[0].HasElements && !nodes[0].HasAttributes?nodes[0].Value:null;
        }
        if(root.Name!="skill_base" ||Field("id")!="9942" ||Field("name")!="item_dish_mushroom_sleepy"
            ||Field("first_target")!="Me" ||Field("target_relation_restriction")!="Friend"
            ||Enumerable.Range(1,3).Any(i=>Field($"effect{i}_noresist")!="1" ||Field($"effect{i}_target_type")!="Target_Only"
                ||Field($"effect{i}_remain1")!="0" ||Field($"effect{i}_remain2")!="600000" ||Field($"effect{i}_effectid")!="10013"
                ||Field($"effect{i}_reserved2")!="0" ||Field($"effect{i}_reserved6")!="0")
            ||Field("effect1_type")!="StatUp" ||Field("effect1_reserved1")!="8" ||Field("effect1_reserved13")!="hpRegen"
            ||Field("effect2_type")!="StatUp" ||Field("effect2_reserved1")!="8" ||Field("effect2_reserved13")!="mpRegen"
            ||Field("effect2_cond_preeffect")!="e1" ||Field("effect2_cond_preeffect_prob1")!="0" ||Field("effect2_cond_preeffect_prob2")!="100"
            ||Field("effect3_type")!="StatDown" ||Field("effect3_reserved1")!="3" ||Field("effect3_reserved13")!="allpara"
            ||Field("effect3_cond_preeffect")!="e2" ||Field("effect3_cond_preeffect_prob1")!="0" ||Field("effect3_cond_preeffect_prob2")!="100"
            ||Enumerable.Range(4,5).Any(i=>Field($"effect{i}_type") is not null))
            throw new InvalidDataException("All-primary statdown source differs");
    }

    internal sealed class Receipt(Player target,Effect effect,StatFunctionProxy function,long life):IRetailNpcPlayerPrimaryReceipt
    {
        public RetailNpcCurseStatMath.PrimaryModifier Modifier=>new(RetailNpcCurseStatMath.PrimaryAttribute.All,0,-3);
        public bool MatchesHostPrimaries(Player player,int health,int will)
            =>health==player.GetPlayerClass().GetHealth() &&will==player.GetPlayerClass().GetWill();
        public bool IsCurrentForOfflineFixture(Player player)
            =>ReferenceEquals(player,target) &&player.GetController().RetailLifeEpoch==life
                &&TryRead(player,effect,out var current) &&ReferenceEquals(current,function)
                &&player.GetController().RetailLifeEpoch==life;
    }

    internal static bool TryCaptureForOfflineFixture(Player player,Effect effect,ReadOnlySpan<byte> source,out Receipt? receipt)
    {
        receipt=null;VerifySourceForOfflineFixture(source);long life=player.GetController().RetailLifeEpoch;
        if(!TryRead(player,effect,out var function)||player.GetController().RetailLifeEpoch!=life)return false;
        receipt=new(player,effect,function!,life);return true;
    }

    private static bool TryRead(Player player,Effect effect,out StatFunctionProxy? result)
    {
        result=null;var stats=player.GetGameStats();
        if(!player.IsSpawned() ||player.IsDead() ||!stats.TryCaptureRetailStatMutation(out long stamp))return false;
        var active=new HashSet<Effect>(player.GetEffectController().GetAllEffects(),ReferenceEqualityComparer.Instance);
        if(!active.Contains(effect) ||effect.RetailHasEnded ||!ReferenceEquals(effect.GetEffector(),player)
            ||!ReferenceEquals(effect.GetEffected(),player) ||effect.GetSkillId()!=9942 ||effect.GetSkillLevel()!=1
            ||effect.GetStack()!="ITEM_DISH_MUSHROOM_SLEEPY"
            ||!ReferenceEquals(effect.GetSkillTemplate(),DataManager.SKILL_DATA.GetSkillTemplate(9942))
            ||effect.GetEffectTemplates() is not {Count:3} templates
            ||templates[0] is not StatupEffect first ||templates[1] is not StatupEffect second ||templates[2] is not StatdownEffect third
            ||Enumerable.Range(1,3).Any(i=>!effect.IsInSuccessEffects(i) ||!ReferenceEquals(effect.EffectInPos(i),templates[i-1]))
            ||first.PreEffects is not null ||second.PreEffects is not [1] ||third.PreEffects is not [2]
            ||templates.Any(t=>t.Effectid!=10013 ||t.Duration1!=0 ||t.Duration2!=600000 ||!t.NoResist
                ||t.EffectConditions is not null ||t.EffectSubConditions is not null ||t.change is not {Count:1}))return false;
        var hp=first.change![0];var mp=second.change![0];var down=third.change![0];
        if(hp.Stat!=StatEnum.REGEN_HP ||hp.Func!=ChangeFunc.ADD ||hp.Value!=0 ||hp.Delta!=8 ||hp.Conditions is not null
            ||mp.Stat!=StatEnum.REGEN_MP ||mp.Func!=ChangeFunc.ADD ||mp.Value!=0 ||mp.Delta!=8 ||mp.Conditions is not null
            ||down.Stat!=StatEnum.ALLPARA ||down.Func!=ChangeFunc.ADD ||down.Value!=0 ||down.Delta!=-3 ||down.Conditions is not null)return false;
        foreach(var stat in PrimaryStats)
        {
            var functions=stats.GetStatsSorted(stat);
            if(stat!=StatEnum.ALLPARA) { if(functions.Count!=0)return false;continue; }
            if(functions is not [StatFunctionProxy proxy] ||!ReferenceEquals(proxy.GetOwner(),effect)
                ||proxy.GetProxiedFunction().GetType()!=typeof(StatAddFunction) ||proxy.HasConditions()
                ||!proxy.IsBonus() ||proxy.GetValue()!=-3)return false;
            result=proxy;
        }
        return result is not null &&stats.RetailStatMutationUnchanged(stamp)
            &&active.SetEquals(player.GetEffectController().GetAllEffects());
    }
}
