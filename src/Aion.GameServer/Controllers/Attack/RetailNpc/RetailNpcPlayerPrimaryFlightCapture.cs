using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using ChangeFunc = Aion.GameServer.SkillEngine.Change.Func;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Offline witness for eight selected 5.8 flight passives. Native
/// state is supplied explicitly; no live state mapping or NPC skill is admitted.</summary>
internal static class RetailNpcPlayerPrimaryFlightCapture
{
    internal sealed record Definition(int Id,string Hash,string Name,string Stack,StatEnum First,StatEnum Second,
        string FirstNative,string SecondNative,int FirstEffect,int SecondEffect,int ThirdEffect,bool FirstDefaults,int ThirdBasicLevel);
    private static readonly Definition[] Definitions=[
        new(340,"97ed0d305bbb6b553aad31dff7cc3d244a29de120af42e7395abae10a62d3bce","CH_ChantersGift_G1","CH_CHANTERSGIFT",StatEnum.POWER,StatEnum.WILL,"STR","WIL",112971,112972,112973,false,0),
        new(344,"677704806ac9dcae9a39e1534c3970c23bd16be8c0728608373e272c0e76c621","RI_RidersGift_G1","RI_RIDERSGIFT",StatEnum.WILL,StatEnum.KNOWLEDGE,"WIL","KNO",100971,100972,100973,true,0),
        new(347,"5ab2748fc6a6df5266f37b1d19b793f83c04d9dce9d233558d4082d499299ff5","FI_FightersGift_G1","FI_FIGHTERSGIFT",StatEnum.POWER,StatEnum.HEALTH,"STR","VIT",103331,103332,103332,false,0),
        new(352,"f252f413a1b1536296217d844e7f577363f3e52d1c2f21d503b83badcc9cfc24","WI_WizardsGift_G1","WI_WIZARDSGIFT",StatEnum.KNOWLEDGE,StatEnum.WILL,"KNO","WIL",115481,115482,115483,false,0),
        new(361,"40ab8dce94a68c5ee77eae3234b61f1a6602e4c626e1473a8d19572b2d82371c","BA_BardsGift_G1","BA_BARDSGIFT",StatEnum.WILL,StatEnum.KNOWLEDGE,"WIL","KNO",100911,100912,100913,true,20),
        new(362,"7fe3bc66649360d3e596892092c1e9f69bb5f690cc226b33780440c7b1f1a275","PR_PriestsGift_G1","PR_PRIESTSGIFT",StatEnum.HEALTH,StatEnum.WILL,"VIT","WIL",111531,111532,111533,false,0),
        new(370,"1e3c3d98bef31a9ecb9257dfcdb9907075fbc5af262ab218e3dba91a5805ca72","KN_KnightGift_G1","KN_KNIGHTGIFT",StatEnum.HEALTH,StatEnum.ACCURACY,"VIT","DEX",104871,104872,104872,false,0),
        new(374,"4dc859233c0b579788e5eef5db1035ed47e7c93c25b64547e19d4636da7d708b","EL_ElementalsGift_G1","EL_ELEMENTALSGIFT",StatEnum.KNOWLEDGE,StatEnum.WILL,"KNO","WIL",115481,115482,115483,false,0)
    ];
    private static readonly StatEnum[] PrimaryStats=[StatEnum.HEALTH,StatEnum.WILL,StatEnum.ALLPARA,StatEnum.STRVIT,StatEnum.KNOWIL];

    internal static bool NativeFlightBonusApplies(uint state)=>(state&15) is 3 or 13 or 14;
    internal static void VerifySourceForOfflineFixture(ReadOnlySpan<byte> raw)=>ReadSource(raw);

    private static Definition ReadSource(ReadOnlySpan<byte> raw)
    {
        string hash=Convert.ToHexStringLower(SHA256.HashData(raw));
        var definition=Definitions.SingleOrDefault(d=>d.Hash==hash)??throw new InvalidDataException("Flight primary source hash differs");
        using var reader=XmlReader.Create(new StringReader(new UnicodeEncoding(false,false,true).GetString(raw)),
            new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null});
        var root=XElement.Load(reader);
        string? Field(string name)
        {
            var nodes=root.Elements(name).ToArray();
            return nodes is {Length:1} && !nodes[0].HasElements && !nodes[0].HasAttributes?nodes[0].Value:null;
        }
        string? firstTarget=definition.FirstDefaults?null:"Target_Only";
        string? firstNoResist=definition.FirstDefaults?null:"1";
        string? firstR1=definition.FirstDefaults?null:"0";
        string? thirdBasic=definition.ThirdBasicLevel==0?null:definition.ThirdBasicLevel.ToString();
        if(root.Name!="skill_base" ||Field("id")!=definition.Id.ToString() ||Field("name")!=definition.Name
            ||Field("activation_attribute")!="Passive" ||Field("first_target")!="Me"
            ||Enumerable.Range(1,3).Any(i=>Field($"effect{i}_type")!="StatBoost" ||Field($"effect{i}_reserved9")!="1")
            ||Field("effect1_noresist")!=firstNoResist ||Field("effect1_target_type")!=firstTarget
            ||Field("effect2_noresist")!="1" ||Field("effect2_target_type")!=firstTarget
            ||Field("effect3_noresist")!="1" ||Field("effect3_target_type")!="Target_Only"
            ||Field("effect1_effectid")!=definition.FirstEffect.ToString() ||Field("effect1_reserved1")!=firstR1 ||Field("effect1_reserved2")!="10" ||Field("effect1_reserved13")!=definition.FirstNative
            ||Field("effect2_effectid")!=definition.SecondEffect.ToString() ||Field("effect2_reserved1")!=firstR1 ||Field("effect2_reserved2")!="10" ||Field("effect2_reserved13")!=definition.SecondNative
            ||Field("effect3_effectid")!=definition.ThirdEffect.ToString() ||Field("effect3_reserved3")!="0" ||Field("effect3_reserved4")!="33" ||Field("effect3_reserved13")!="FlySpeed" ||Field("effect3_basiclv")!=thirdBasic
            ||Enumerable.Range(2,2).Any(i=>Field($"effect{i}_cond_preeffect")!="e1" ||Field($"effect{i}_cond_preeffect_prob1")!="0" ||Field($"effect{i}_cond_preeffect_prob2")!="100")
            ||Enumerable.Range(4,5).Any(i=>Field($"effect{i}_type") is not null))
            throw new InvalidDataException("Flight primary source differs");
        return definition;
    }

    internal sealed class Receipt(Player target,Effect effect,Definition source,StatFunctionProxy first,StatFunctionProxy second,
        long life,int hostFlyState,uint nativeState,Func<Player,uint?> state):IRetailNpcPlayerPrimaryReceipt
    {
        private bool Has(StatEnum stat)=>source.First==stat ||source.Second==stat;
        public RetailNpcCurseStatMath.PrimaryModifier Modifier
        {
            get
            {
                var attribute=Has(StatEnum.HEALTH) &&Has(StatEnum.WILL)?RetailNpcCurseStatMath.PrimaryAttribute.All
                    :Has(StatEnum.HEALTH)?RetailNpcCurseStatMath.PrimaryAttribute.Health:RetailNpcCurseStatMath.PrimaryAttribute.Will;
                return new(attribute,0,NativeFlightBonusApplies(nativeState)?10:0);
            }
        }
        public bool MatchesHostPrimaries(Player player,int health,int will)
        {
            int bonus=player.IsInFlyingState()?10:0;
            return health==player.GetPlayerClass().GetHealth()+(Has(StatEnum.HEALTH)?bonus:0)
                &&will==player.GetPlayerClass().GetWill()+(Has(StatEnum.WILL)?bonus:0);
        }
        public bool AllowsConditionalFunction(IStatFunction function)=>ReferenceEquals(function,first)||ReferenceEquals(function,second);
        public bool IsCurrentForOfflineFixture(Player player)
            =>ReferenceEquals(player,target) &&player.GetController().RetailLifeEpoch==life &&player.GetFlyState()==hostFlyState
                &&state(player)==nativeState &&TryRead(player,effect,source,out var f,out var s)
                &&ReferenceEquals(f,first) &&ReferenceEquals(s,second)
                &&state(player)==nativeState &&player.GetFlyState()==hostFlyState &&player.GetController().RetailLifeEpoch==life;
    }

    internal static bool TryCaptureForOfflineFixture(Player player,Effect effect,ReadOnlySpan<byte> source,
        Func<Player,uint?> nativeState,out Receipt? receipt)
    {
        ArgumentNullException.ThrowIfNull(nativeState);receipt=null;var definition=ReadSource(source);
        long life=player.GetController().RetailLifeEpoch;int hostFly=player.GetFlyState();uint? state=nativeState(player);
        if(state is null ||!TryRead(player,effect,definition,out var first,out var second) ||nativeState(player)!=state
            ||player.GetFlyState()!=hostFly ||player.GetController().RetailLifeEpoch!=life)return false;
        receipt=new(player,effect,definition,first!,second!,life,hostFly,state.Value,nativeState);return true;
    }

    private static bool IsOnFlyChange(Aion.GameServer.SkillEngine.Change.Change change,StatEnum stat,ChangeFunc func,int value)
        =>change.Stat==stat &&change.Func==func &&change.Value==value &&change.Delta==0
            &&change.Conditions?.conditions is [OnFlyCondition];

    private static bool TryRead(Player player,Effect effect,Definition source,out StatFunctionProxy? firstFunction,out StatFunctionProxy? secondFunction)
    {
        firstFunction=null;secondFunction=null;var stats=player.GetGameStats();
        if(!player.IsSpawned() ||player.IsDead() ||!stats.TryCaptureRetailStatMutation(out long stamp))return false;
        var active=new HashSet<Effect>(player.GetEffectController().GetAllEffects(),ReferenceEqualityComparer.Instance);
        if(!active.Contains(effect) ||effect.RetailHasEnded ||!ReferenceEquals(effect.GetEffector(),player)
            ||!ReferenceEquals(effect.GetEffected(),player) ||effect.GetSkillId()!=source.Id ||effect.GetSkillLevel()!=1
            ||effect.GetStack()!=source.Stack ||!ReferenceEquals(effect.GetSkillTemplate(),DataManager.SKILL_DATA.GetSkillTemplate(source.Id))
            ||effect.GetEffectTemplates() is not {Count:3} templates
            ||templates[0] is not StatboostEffect first ||templates[1] is not StatboostEffect second ||templates[2] is not StatboostEffect third
            ||Enumerable.Range(1,3).Any(i=>!effect.IsInSuccessEffects(i) ||!ReferenceEquals(effect.EffectInPos(i),templates[i-1]))
            ||first.PreEffects is not null ||second.PreEffects is not [1] ||third.PreEffects is not [1]
            ||first.Effectid!=source.FirstEffect ||second.Effectid!=source.SecondEffect ||third.Effectid!=source.ThirdEffect
            ||!first.NoResist ||!second.NoResist ||!third.NoResist ||third.BasicLvl!=source.ThirdBasicLevel
            ||templates.Any(t=>t.EffectConditions is not null ||t.EffectSubConditions is not null ||t.change is not {Count:1})
            ||!IsOnFlyChange(first.change![0],source.First,ChangeFunc.ADD,10)
            ||!IsOnFlyChange(second.change![0],source.Second,ChangeFunc.ADD,10)
            ||!IsOnFlyChange(third.change![0],StatEnum.FLY_SPEED,ChangeFunc.PERCENT,33))return false;
        StatFunctionProxy? FindOwned(StatEnum stat)
        {
            var found=stats.GetStatsSorted(stat).OfType<StatFunctionProxy>().Where(x=>ReferenceEquals(x.GetOwner(),effect)).ToArray();
            if(found is not [var proxy] ||proxy.GetProxiedFunction() is not StatAddFunction underlying
                ||!proxy.HasConditions() ||!proxy.IsBonus() ||proxy.GetValue()!=10 ||underlying._conditions?.conditions is not [OnFlyCondition])return null;
            return proxy;
        }
        firstFunction=FindOwned(source.First);secondFunction=FindOwned(source.Second);
        if(firstFunction is null ||secondFunction is null)return false;
        foreach(var stat in PrimaryStats)
        {
            var functions=stats.GetStatsSorted(stat);
            bool selected=stat==source.First ||stat==source.Second;
            if(!selected &&functions.Count!=0 ||selected &&(functions.Count!=1 ||!ReferenceEquals(functions[0],stat==source.First?firstFunction:secondFunction)))return false;
        }
        return stats.RetailStatMutationUnchanged(stamp) &&active.SetEquals(player.GetEffectController().GetAllEffects());
    }
}
