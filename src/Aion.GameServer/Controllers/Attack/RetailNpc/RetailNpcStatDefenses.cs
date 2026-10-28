using System.Runtime.CompilerServices;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Selected NPC HP, defensive and attack scalar getters. Native CalcFirst/SecondLevelAttr
/// combines signed32 modifier pairs against the pre-final-cap seed with signed64
/// integer division, then clamps. This includes supported attack-stat buffs and
/// ordinary 25-slot status resistance and penetration channels. See statup-integration-review/native-caps.json
/// and the retained status-channel producer evidence.</summary>
internal static class RetailNpcStatDefenses
{
    private static readonly ILogger Log=AionLog.For(nameof(RetailNpcStatDefenses));
    private static readonly ConditionalWeakTable<Npc,HashSet<string>> Warnings=new();
    private static readonly ISet<CalculationType> NoCalculationTypes=new HashSet<CalculationType>();

    internal static void WarnIfAiOverrides(Npc npc, Stat2 stat, int beforeAi)
    {
        int current = stat.GetCurrent();
        if (current == beforeAi) return;
        var warnings = Warnings.GetValue(npc, _ => []);
        lock (warnings) if (warnings.Add(stat.GetStat() + ":ai-override"))
            Log.LogWarning("RETAIL_STAT_DEFENSE_COMPAT npc={Npc} stat={Stat} reason=ai-override before={Before} after={After}; existing4.8 AI stat owner retained; native script semantics unverified",
                npc.GetNpcId(), stat.GetStat(), beforeAi, current);
    }

    internal static string? Field(StatEnum stat)=>(stat switch
    {
        StatEnum.MAXHP=>"max_hp",StatEnum.PHYSICAL_DEFENSE=>"physical_defend",StatEnum.MAGICAL_DEFEND=>"magical_defend",
        StatEnum.MAGICAL_RESIST=>"magical_resist",StatEnum.EVASION=>"dodge",
        StatEnum.PARRY=>"parry",StatEnum.BLOCK=>"block",
        StatEnum.PHYSICAL_ACCURACY=>"hit_accuracy",StatEnum.MAGICAL_ACCURACY=>"magical_hit_accuracy",
        StatEnum.PHYSICAL_CRITICAL=>"critical",StatEnum.BOOST_MAGICAL_SKILL=>"magical_skill_boost",
        StatEnum.WATER_RESISTANCE=>"elemental_defend_water",StatEnum.WIND_RESISTANCE=>"elemental_defend_air",
        StatEnum.EARTH_RESISTANCE=>"elemental_defend_earth",StatEnum.FIRE_RESISTANCE=>"elemental_defend_fire",
        StatEnum.LIGHT_RESISTANCE or StatEnum.ELEMENTAL_RESISTANCE_LIGHT=>"elemental_defend_light",
        StatEnum.DARK_RESISTANCE or StatEnum.ELEMENTAL_RESISTANCE_DARK=>"elemental_defend_dark",_=>null
    }) ?? SkyTempleNpcProfile.AbnormalField(stat);

    internal static long Calculate(long basis,int percent,int flat,long minimum,long maximum)
        =>Math.Clamp(checked(basis+basis*(long)percent/100+flat),minimum,maximum);

    internal static bool TryApply(Npc npc,StatEnum name,Stat2 stat)
    {
        if(!RetailNpcStatCatalog.TryGet(npc,out var profile)||Field(name) is not { } field)return false;
        bool Compatibility(string reason)
        {
            var warnings=Warnings.GetValue(npc,_=>[]);
            lock(warnings)if(warnings.Add(name+":"+reason))
                Log.LogWarning("RETAIL_STAT_DEFENSE_COMPAT npc={Npc} stat={Stat} reason={Reason}; host getter retained",npc.GetNpcId(),name,reason);
            return false;
        }
        // Arbitrary caller-supplied baselines/reverse stats are not native actor getters.
        if(stat.GetType()!=typeof(AdditionStat)||stat.GetExactBaseWithoutBaseRate()!=profile.Values[field]
            ||stat.GetBaseRate()!=1||stat.GetExactBonus()!=0||stat.GetBonusRate()!=1
            ||stat.GetFixedBonusRate()!=0||stat.GetFinalRate()!=1)return Compatibility("nonstandard-getter-input");
        var functions=npc.GetGameStats().GetStatsSorted(name).ToArray();
        foreach(var function in functions)
        {
            var inner=function is StatFunctionProxy proxy?proxy.GetProxiedFunction():function;
            var type=inner.GetType();
            if(function.GetName()!=name||type!=typeof(StatFunction)&&type!=typeof(StatRateFunction)
                &&type!=typeof(StatAddFunction)&&type!=typeof(StatSubFunction))return Compatibility("operator-"+type.Name);
        }
        int percent=0,flat=0;
        AdditionStat? legacyHpBase=null;
        foreach(var function in functions)
        {
            // Validate the original proxy, preserving its real effect owner.
            if(!function.Validate(stat))continue;
            var inner=function is StatFunctionProxy proxy?proxy.GetProxiedFunction():function;
            var type=inner.GetType();
            if(name==StatEnum.MAXHP && !inner.IsBonus())
            {
                // Some legacy skills read GetBase separately from current HP.
                // Preserve that representation until their native owner is reviewed.
                legacyHpBase??=new AdditionStat(name,stat.GetExactBaseWithoutBaseRate(),npc);
                inner.Apply(legacyHpBase,NoCalculationTypes);
            }
            if(type==typeof(StatRateFunction))percent=unchecked(percent+inner.GetValue());
            else if(type==typeof(StatAddFunction))flat=unchecked(flat+inner.GetValue());
            else if(type==typeof(StatSubFunction))flat=unchecked(flat-inner.GetValue());
        }
        var source=profile.ScalarInputs.Fields[field];var spec=source.Specification;
        long value=Calculate(source.Value,percent,flat,spec.Minimum!.Value,spec.Maximum!.Value);
        // Preserve the native integer even where MAXHP cannot fit in binary32.
        // Keep its host base separate for legacy GetBase consumers; AI's existing
        // final hook can still change the stat afterwards.
        float hpBase=legacyHpBase?.GetExactBaseWithoutBaseRate()??stat.GetExactBaseWithoutBaseRate();
        stat.SetBase(name==StatEnum.MAXHP?hpBase:value);stat.SetBaseRate(1);
        stat.SetBonus(name==StatEnum.MAXHP?(float)value-hpBase:0);stat.SetBonusRate(1);
        stat.SetFixedBonusRate(0);stat.SetFinalRate(1);
        if(name==StatEnum.MAXHP)stat.SetExactIntegerCurrent(checked((int)value));
        return true;
    }
}
