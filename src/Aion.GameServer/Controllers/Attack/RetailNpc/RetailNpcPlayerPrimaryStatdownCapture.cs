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

/// <summary>Offline witness for the selected 5.8 9840 Will reduction.
/// It does not prove complete player primary state or admit gameplay.</summary>
internal static class RetailNpcPlayerPrimaryStatdownCapture
{
    private const string SourceHash="6bac7b1084cd8600978ece7ebad56773bed63b307eb448e6593a6000ef4922fd";
    private static readonly StatEnum[] PrimaryStats=[StatEnum.HEALTH,StatEnum.WILL,StatEnum.ALLPARA,StatEnum.STRVIT,StatEnum.KNOWIL];

    internal static void VerifySourceForOfflineFixture(ReadOnlySpan<byte> raw)
    {
        if(Convert.ToHexStringLower(SHA256.HashData(raw))!=SourceHash)throw new InvalidDataException("Primary statdown source hash differs");
        using var reader=XmlReader.Create(new StringReader(new UnicodeEncoding(false,false,true).GetString(raw)),
            new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null});
        var root=XElement.Load(reader);
        string? Field(string name)
        {
            var nodes=root.Elements(name).ToArray();
            return nodes is {Length:1} && !nodes[0].HasElements && !nodes[0].HasAttributes?nodes[0].Value:null;
        }
        if(root.Name!="skill_base" ||Field("id")!="9840" ||Field("name")!="item_e3_dish_kno_wil_02"
            ||Field("first_target")!="Me" ||Field("target_relation_restriction")!="Friend"
            ||Field("effect1_type")!="StatUp" ||Field("effect1_target_type")!="Target_Only"
            ||Field("effect1_remain1")!="0" ||Field("effect1_remain2")!="300000" ||Field("effect1_effectid")!="10005"
            ||Field("effect1_reserved1")!="10" ||Field("effect1_reserved2")!="0" ||Field("effect1_reserved6")!="0" ||Field("effect1_reserved13")!="kno"
            ||Field("effect2_type")!="StatDown" ||Field("effect2_noresist")!="1" ||Field("effect2_target_type")!="Target_Only"
            ||Field("effect2_remain1")!="0" ||Field("effect2_remain2")!="300000" ||Field("effect2_effectid")!="10005"
            ||Field("effect2_cond_preeffect")!="e1" ||Field("effect2_cond_preeffect_prob1")!="0" ||Field("effect2_cond_preeffect_prob2")!="100"
            ||Field("effect2_reserved1")!="5" ||Field("effect2_reserved2")!="0" ||Field("effect2_reserved6")!="0" ||Field("effect2_reserved13")!="wil"
            ||Enumerable.Range(3,6).Any(i=>Field($"effect{i}_type") is not null))
            throw new InvalidDataException("Primary statdown source differs");
    }

    internal sealed class Receipt(Player target,Effect effect,StatFunctionProxy function,long life):IRetailNpcPlayerPrimaryReceipt
    {
        public RetailNpcCurseStatMath.PrimaryModifier Modifier=>new(RetailNpcCurseStatMath.PrimaryAttribute.Will,0,-5);
        public bool MatchesHostPrimaries(Player player,int health,int will)
            =>health==player.GetPlayerClass().GetHealth() &&will==player.GetPlayerClass().GetWill()-5;
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
            ||!ReferenceEquals(effect.GetEffected(),player) ||effect.GetSkillId()!=9840 ||effect.GetSkillLevel()!=1
            ||effect.GetStack()!="ITEM_E3_DISH_KNO_WIL_02"
            ||!ReferenceEquals(effect.GetSkillTemplate(),DataManager.SKILL_DATA.GetSkillTemplate(9840))
            ||effect.GetEffectTemplates() is not {Count:2} templates
            ||templates[0] is not StatupEffect first ||templates[1] is not StatdownEffect second
            ||!effect.IsInSuccessEffects(1) ||!effect.IsInSuccessEffects(2)
            ||!ReferenceEquals(effect.EffectInPos(1),first) ||!ReferenceEquals(effect.EffectInPos(2),second)
            ||first.Effectid!=10005 ||first.Duration1!=0 ||first.Duration2!=300000
            ||first.PreEffects is not null ||first.EffectConditions is not null ||first.EffectSubConditions is not null
            ||second.Effectid!=10005 ||second.Duration1!=0 ||second.Duration2!=300000 ||!second.NoResist
            ||second.PreEffects is not [1] ||second.EffectConditions is not null ||second.EffectSubConditions is not null
            ||first.change is not {Count:1} ||second.change is not {Count:1})return false;
        var up=first.change[0];var down=second.change[0];
        if(up.Stat!=StatEnum.KNOWLEDGE ||up.Func!=ChangeFunc.ADD ||up.Value!=0 ||up.Delta!=10 ||up.Conditions is not null
            ||down.Stat!=StatEnum.WILL ||down.Func!=ChangeFunc.ADD ||down.Value!=0 ||down.Delta!=-5 ||down.Conditions is not null)return false;
        foreach(var stat in PrimaryStats)
        {
            var functions=stats.GetStatsSorted(stat);
            if(stat!=StatEnum.WILL) { if(functions.Count!=0)return false;continue; }
            if(functions is not [StatFunctionProxy proxy] ||!ReferenceEquals(proxy.GetOwner(),effect)
                ||proxy.GetProxiedFunction().GetType()!=typeof(StatAddFunction) ||proxy.HasConditions()
                ||!proxy.IsBonus() ||proxy.GetValue()!=-5)return false;
            result=proxy;
        }
        return result is not null &&stats.RetailStatMutationUnchanged(stamp)
            &&active.SetEquals(player.GetEffectController().GetAllEffects());
    }
}
