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

/// <summary>An explicitly source-reviewed primary modifier for offline fixtures.</summary>
internal interface IRetailNpcPlayerPrimaryReceipt
{
    RetailNpcCurseStatMath.PrimaryModifier Modifier { get; }
    bool IsCurrentForOfflineFixture(Player player);
    bool MatchesHostPrimaries(Player player,int health,int will);
    bool AllowsConditionalFunction(IStatFunction function)=>false;
}

/// <summary>Bounded fixture for one source-reviewed active food effect. It
/// does not establish a complete player primary snapshot or admit gameplay.</summary>
internal static class RetailNpcPlayerPrimaryFoodCapture
{
    private const string SourceHash="29b56ae81b965408f2b4f2f7ae71f55781cbea61c517b5b8f22c12e2a62adaa3";
    private static readonly StatEnum[] PrimaryStats=[StatEnum.HEALTH,StatEnum.WILL,StatEnum.ALLPARA,StatEnum.STRVIT,StatEnum.KNOWIL];

    internal static void VerifySourceForOfflineFixture(ReadOnlySpan<byte> raw)
    {
        if(Convert.ToHexStringLower(SHA256.HashData(raw))!=SourceHash)throw new InvalidDataException("Primary food source hash differs");
        using var reader=XmlReader.Create(new StringReader(new UnicodeEncoding(false,false,true).GetString(raw)),
            new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null});
        var element=XElement.Load(reader);
        string? Field(string name)
        {
            var nodes=element.Elements(name).ToArray();
            return nodes is {Length:1} && !nodes[0].HasElements && !nodes[0].HasAttributes ? nodes[0].Value:null;
        }
        if(element.Name!="skill_base"||Field("id")!="9839"||Field("name")!="item_e3_dish_kno_wil_01"
            ||Field("effect1_type")!="StatUp"||Field("effect1_target_type")!="Target_Only"
            ||Field("effect1_remain1")!="0"||Field("effect1_remain2")!="300000"||Field("effect1_effectid")!="10005"
            ||Field("effect1_reserved1")!="5"||Field("effect1_reserved2")!="0"||Field("effect1_reserved6")!="0"
            ||Field("effect1_reserved13")!="knowil")throw new InvalidDataException("Primary food source differs");
    }

    internal sealed class Receipt(Player target,Effect effect,StatFunctionProxy function,long life):IRetailNpcPlayerPrimaryReceipt
    {
        internal RetailNpcCurseStatMath.PrimaryModifier Modifier => new(RetailNpcCurseStatMath.PrimaryAttribute.KnowledgeWill,0,5);
        // Allows the curse's own HP/MP mutation, but requires the exact food
        // owner and stat function to remain installed. It is not a lock.
        internal bool IsCurrentForOfflineFixture(Player player)
            => ReferenceEquals(target,player) && player.GetController().RetailLifeEpoch==life
                && TryRead(player,effect,out var current) && ReferenceEquals(current,function)
                && player.GetController().RetailLifeEpoch==life;

        RetailNpcCurseStatMath.PrimaryModifier IRetailNpcPlayerPrimaryReceipt.Modifier=>Modifier;
        bool IRetailNpcPlayerPrimaryReceipt.IsCurrentForOfflineFixture(Player player)=>IsCurrentForOfflineFixture(player);
        bool IRetailNpcPlayerPrimaryReceipt.MatchesHostPrimaries(Player player,int health,int will)
            =>health==player.GetPlayerClass().GetHealth() &&will==player.GetPlayerClass().GetWill();
    }

    internal static bool TryCaptureForOfflineFixture(Player player,Effect effect,ReadOnlySpan<byte> source,out Receipt? receipt)
    {
        receipt=null;VerifySourceForOfflineFixture(source);
        long life=player.GetController().RetailLifeEpoch;
        if(!TryRead(player,effect,out var function)||player.GetController().RetailLifeEpoch!=life)return false;
        receipt=new(player,effect,function!,life);return true;
    }

    private static bool TryRead(Player player,Effect effect,out StatFunctionProxy? result)
    {
        result=null;var stats=player.GetGameStats();
        if(!player.IsSpawned()||player.IsDead()||!stats.TryCaptureRetailStatMutation(out long stamp))return false;
        var active=new HashSet<Effect>(player.GetEffectController().GetAllEffects(),ReferenceEqualityComparer.Instance);
        if(!active.Contains(effect)||effect.RetailHasEnded||!ReferenceEquals(effect.GetEffector(),player)
            ||!ReferenceEquals(effect.GetEffected(),player)||effect.GetSkillId()!=9839||effect.GetSkillLevel()!=1
            ||effect.GetStack()!="ITEM_E3_DISH_KNO_WIL_01"
            ||!ReferenceEquals(effect.GetSkillTemplate(),DataManager.SKILL_DATA.GetSkillTemplate(9839))
            ||effect.GetSkillTemplate().GetEffectTemplate(1) is not StatupEffect template
            ||!effect.IsInSuccessEffects(1)||!ReferenceEquals(effect.EffectInPos(1),template)
            ||template.Effectid!=10005||template.Duration1!=0||template.Duration2!=300000
            ||template.PreEffects is not null||template.EffectConditions is not null||template.EffectSubConditions is not null)return false;
        var changes=template.change;
        if(changes is not {Count:1}||changes[0].Stat!=StatEnum.KNOWIL||changes[0].Func!=ChangeFunc.ADD
            ||changes[0].Value!=0||changes[0].Delta!=5||changes[0].Conditions is not null)return false;
        foreach(var stat in PrimaryStats)
        {
            var functions=stats.GetStatsSorted(stat);
            if(stat!=StatEnum.KNOWIL) { if(functions.Count!=0)return false;continue; }
            if(functions is not [StatFunctionProxy proxy]||!ReferenceEquals(proxy.GetOwner(),effect)
                ||proxy.GetProxiedFunction().GetType()!=typeof(StatAddFunction)||proxy.HasConditions()
                ||!proxy.IsBonus()||proxy.GetValue()!=5)return false;
            result=proxy;
        }
        return result is not null && stats.RetailStatMutationUnchanged(stamp)
            && active.SetEquals(player.GetEffectController().GetAllEffects());
    }
}
