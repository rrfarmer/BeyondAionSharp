using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Numeric StatUp descriptors only. Existing Effect owns admission,
/// target selection, registration, duration and removal. Native parameter/writer
/// proof: stats-only-20261025-01/combat-worker/statup-review/.</summary>
internal static class RetailNpcStatBuffs
{
    internal sealed record Part(StatEnum Stat,int Delta,int Value,bool Percent);
    internal sealed record Program(int Position,ImmutableArray<Part> Parts,string? UnsupportedReason)
    {
        internal bool Supported=>UnsupportedReason is null;
    }
    internal sealed record Source(int Id,string Name,string Sha256,ImmutableArray<Program> Programs);
    private static readonly ILogger Log=AionLog.For(nameof(RetailNpcStatBuffs));
    private static readonly ConditionalWeakTable<Npc,HashSet<(int,int,string)>> Warnings=new();
    private sealed class SelectedMarker
    {
        internal readonly Dictionary<int,(IStatFunction[] Host,IStatFunction[] Projected)> Programs=[];
        internal bool Restored;
    }
    private static readonly ConditionalWeakTable<Effect,SelectedMarker> Selected=new();
    internal static bool HasSelectedProgram(Effect effect)=>Selected.TryGetValue(effect,out _);
    internal static bool OwnsSelectedFunction(IStatFunction function)
    {
        if (function is not StatFunctionProxy proxy || proxy.GetOwner() is not Effect effect
            || !Selected.TryGetValue(effect, out var marker)) return false;
        lock (marker) return !marker.Restored && marker.Programs.Values.Any(p =>
            p.Projected.Any(f => ReferenceEquals(f, proxy.GetProxiedFunction())));
    }
    internal static void RestoreHostForRetiredTarget(Npc target)
    {
        // Ownership may be assigned before spawn creates an EffectController.
        if(target.GetEffectController() is not { } controller)return;
        foreach(var effect in controller.GetAllEffects())
        {
            if(!Selected.TryGetValue(effect,out var selected))continue;
            lock(selected)
            {
                if(selected.Restored)continue;
                var previous=new HashSet<IStatFunction>(selected.Programs.Values.SelectMany(p=>p.Projected),ReferenceEqualityComparer.Instance);
                target.GetGameStats().ReplaceOwnedFunctions(effect,previous,selected.Programs.Values.SelectMany(p=>p.Host));
                selected.Restored=true;
            }
        }
    }
    internal static readonly ImmutableArray<StatEnum> Elements=[StatEnum.WATER_RESISTANCE,StatEnum.WIND_RESISTANCE,
        StatEnum.EARTH_RESISTANCE,StatEnum.FIRE_RESISTANCE,StatEnum.LIGHT_RESISTANCE,StatEnum.DARK_RESISTANCE];
    internal static readonly ImmutableArray<StatEnum> AbnormalResistances = [StatEnum.POISON_RESISTANCE,
        StatEnum.BLEED_RESISTANCE, StatEnum.PARALYZE_RESISTANCE, StatEnum.SLEEP_RESISTANCE, StatEnum.ROOT_RESISTANCE,
        StatEnum.BLIND_RESISTANCE, StatEnum.CHARM_RESISTANCE, StatEnum.DISEASE_RESISTANCE, StatEnum.SILENCE_RESISTANCE,
        StatEnum.FEAR_RESISTANCE, StatEnum.CURSE_RESISTANCE, StatEnum.CONFUSE_RESISTANCE, StatEnum.STUN_RESISTANCE,
        StatEnum.PERIFICATION_RESISTANCE, StatEnum.STUMBLE_RESISTANCE, StatEnum.STAGGER_RESISTANCE,
        StatEnum.OPENAERIAL_RESISTANCE, StatEnum.SNARE_RESISTANCE, StatEnum.SLOW_RESISTANCE, StatEnum.SPIN_RESISTANCE,
        StatEnum.BIND_RESISTANCE, StatEnum.DEFORM_RESISTANCE, StatEnum.PULLED_RESISTANCE, StatEnum.NOFLY_RESISTANCE,
        StatEnum.SIMPLEROOT_RESISTANCE];
    internal static bool InScope(StatEnum stat)=>stat is StatEnum.MAXHP or StatEnum.PHYSICAL_ATTACK or StatEnum.MAGICAL_ATTACK
        or StatEnum.PHYSICAL_DEFENSE or StatEnum.MAGICAL_DEFEND or StatEnum.MAGICAL_RESIST or StatEnum.EVASION
        or StatEnum.PARRY or StatEnum.BLOCK or StatEnum.PHYSICAL_ACCURACY or StatEnum.MAGICAL_ACCURACY
        or StatEnum.PHYSICAL_CRITICAL or StatEnum.BOOST_MAGICAL_SKILL or StatEnum.ABNORMAL_RESISTANCE_ALL
        or StatEnum.SPEED or StatEnum.ATTACK_SPEED or StatEnum.CONCENTRATION or StatEnum.BOOST_CASTING_TIME
        || Elements.Contains(stat) || AbnormalResistances.Contains(stat);

    internal static Source Parse(ReadOnlySpan<byte> bytes,string sha) => ParseCore(bytes,sha,false);

    // Player resistance retains its separate aggregate channel. NPC ArAll
    // writes all 25 native pairs, including the internal SimpleRoot slot.
    internal static Source ParsePlayerResistance(ReadOnlySpan<byte> bytes,string sha) => ParseCore(bytes,sha,true);

    private static Source ParseCore(ReadOnlySpan<byte> bytes,string sha,bool playerResistance)
    {
        var record = RetailNpcSkillRecord.Read(bytes, sha);
        var root = record.Root; int id = record.Id; string name = record.Name;
        string? Leaf(string field)
        {
            var rows = root.Elements(field).ToArray();
            if (rows.Length == 0) return null;
            if (rows.Length != 1 || rows[0].HasElements || rows[0].HasAttributes)
                throw new InvalidDataException("Ambiguous StatUp leaf: " + field);
            return rows[0].Value;
        }
        var programs=ImmutableArray.CreateBuilder<Program>();
        for(int position=1;position<=8;position++)
        {
            string prefix="effect"+position+"_";
            if(!string.Equals(Leaf(prefix+"type"),"StatUp",StringComparison.OrdinalIgnoreCase))continue;
            var parts=ImmutableArray.CreateBuilder<Part>();string? reason=null;
            try
            {
                int Number(int n)
                {
                    string? text=Leaf(prefix+"reserved"+n);
                    if(text is null)return 0;
                    if(!int.TryParse(text,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int value))
                        throw new Boundary("Noninteger reserved"+n);
                    return value;
                }
                if(id is >=11907 and <=11916)throw new Boundary("Special MAXHP scalar context");
                // Complete ApplyEffect proof reads R9 plus the four descriptors.
                // R5/R8/R10/R11/R12/R23/R24 are not numeric operands here.
                if (Number(9) != 0) throw new Boundary("Unproved reserved9 target context");
                foreach(var (attr,delta,value,mode) in new[]{(13,1,2,6),(14,3,4,7),(18,15,16,17),(22,19,20,21)})
                {
                    string? attribute=Leaf(prefix+"reserved"+attr);
                    if(attribute is null or "0")continue;
                    var stats=playerResistance && attribute.Equals("arAll",StringComparison.OrdinalIgnoreCase)
                        ? ImmutableArray.Create(StatEnum.ABNORMAL_RESISTANCE_ALL) : Expand(attribute);
                    if(stats.IsDefaultOrEmpty)throw new Boundary("Unproved attribute: "+attribute);
                    int d=Number(delta),v=Number(value);bool percent=Number(mode)==1;
                    foreach(var stat in stats)
                        // Native attr29 subtracts; the managed delay getter is
                        // direct. Casting-time functions instead have the host
                        // reverse-stat owner and retain positive boost operands.
                        parts.Add(stat == StatEnum.ATTACK_SPEED
                            ? new(stat, unchecked(-d), unchecked(-v), percent) : new(stat,d,v,percent));
                }
            }
            catch(Boundary error){reason=error.Message;parts.Clear();}
            programs.Add(new(position,parts.ToImmutable(),reason));
        }
        return new(id,name,sha,programs.ToImmutable());
    }

    // These names/aliases and expansions were rechecked against the pinned
    // native name table and GetBonusAttrTo dispatch. Unknown names reject the
    // whole program; a partial projection must never silently omit a channel.
    internal static ImmutableArray<StatEnum> Expand(string name)=>name.ToLowerInvariant() switch
    {
        "pmdefend"=>[StatEnum.PHYSICAL_DEFENSE,..Elements],
        "pmattack"=>[StatEnum.PHYSICAL_ATTACK,StatEnum.MAGICAL_ATTACK],
        // Attr98 writer14035c19f updates both modifier words for31/32/33.
        "activedefend"=>[StatEnum.EVASION,StatEnum.PARRY,StatEnum.BLOCK],
        "arall"=>AbnormalResistances,
        "speed"=>[StatEnum.SPEED],"attackdelay"=>[StatEnum.ATTACK_SPEED],
        "concentration"=>[StatEnum.CONCENTRATION],"boostcastingtime"=>[StatEnum.BOOST_CASTING_TIME],
        "allresist" or "elementaldefendall" or "edall"=>Elements,
        "physicaldefend"=>[StatEnum.PHYSICAL_DEFENSE],"magicaldefend"=>[StatEnum.MAGICAL_DEFEND],
        "phyattack"=>[StatEnum.PHYSICAL_ATTACK],"magicalattack"=>[StatEnum.MAGICAL_ATTACK],
        "maxhp"=>[StatEnum.MAXHP],"magicalresist"=>[StatEnum.MAGICAL_RESIST],"dodge"=>[StatEnum.EVASION],
        "parry"=>[StatEnum.PARRY],"block"=>[StatEnum.BLOCK],"hitaccuracy"=>[StatEnum.PHYSICAL_ACCURACY],
        "magicalhitaccuracy"=>[StatEnum.MAGICAL_ACCURACY],"critical"=>[StatEnum.PHYSICAL_CRITICAL],
        "magicalskillboost"=>[StatEnum.BOOST_MAGICAL_SKILL],
        "edwater" or "elementaldefendwater"=>[StatEnum.WATER_RESISTANCE],
        "edair" or "elementaldefendair"=>[StatEnum.WIND_RESISTANCE],
        "edearth" or "elementaldefendearth"=>[StatEnum.EARTH_RESISTANCE],
        "edfire" or "elementaldefendfire"=>[StatEnum.FIRE_RESISTANCE],
        "edlight" or "elementaldefendlight"=>[StatEnum.LIGHT_RESISTANCE],
        "eddark" or "elementaldefenddark"=>[StatEnum.DARK_RESISTANCE],_=>[]
    };

    internal static int Value(Part part,int level)
    {
        if(level is <0 or >short.MaxValue)throw new ArgumentOutOfRangeException(nameof(level));
        return unchecked((short)level*part.Delta+part.Value);
    }

    internal static List<IStatFunction> Select(Effect effect,BufEffect template,List<IStatFunction> host)
    {
        if(template.GetType()!=typeof(StatupEffect)||effect.GetEffector() is not Npc caster||effect.GetEffected() is not Npc target
            ||caster.GetWorldId()!=target.GetWorldId()||caster.GetInstanceId()!=target.GetInstanceId()
            ||!RetailNpcStatCatalog.TryGet(caster,out var profile)||!RetailNpcStatCatalog.TryGet(target,out _))return host;
        List<IStatFunction> Compatibility(string reason)
        {
            var warnings=Warnings.GetValue(caster,_=>[]);
            lock(warnings)if(warnings.Add((effect.GetSkillId(),template.GetPosition(),reason)))
                Log.LogWarning("RETAIL_STAT_BUFF_COMPAT npc={Npc} skill={Skill} effect={Effect} reason={Reason}; host numeric program retained",
                    caster.GetNpcId(),effect.GetSkillId(),template.GetPosition(),reason);
            return host;
        }
        if(!ReferenceEquals(effect.GetSkillTemplate().GetEffectTemplate(template.GetPosition()),template))
            return Compatibility("host-effect-identity");
        int numericLevel = effect.GetSkillLevel();
        bool retainedHostAdmission = false;
        bool HostLink(RetailNpcStatTriggeredBuffs.Link link) => RetailNpcStatTriggeredBuffs.MatchesHost(link,
            Aion.GameServer.Dataholders.DataManager.SKILL_DATA.GetSkillTemplate(link.ParentId)?.GetEffectTemplate(link.Position));
        if (!RetailNpcStatCatalog.TryGetBuff(profile, effect.GetSkillId(), numericLevel, out var source, out var lineage, HostLink))
        {
            if (!RetailNpcStatCatalog.TryGetBuffNumericLevel(caster, profile, effect.GetSkillId(), numericLevel, out numericLevel)
                || !RetailNpcStatCatalog.TryGetBuff(profile, effect.GetSkillId(), numericLevel, out source, out lineage, HostLink))
            {
                if (!RetailNpcStatCatalog.TryGetRetainedHostBuff(caster, profile, effect.GetSkillId(), numericLevel, out source))
                    return Compatibility("missing-exact-source-binding");
                retainedHostAdmission = true;
            }
        }
        if(lineage.Length>0&&!RetailNpcStatCatalog.TriggeredBuffMatchesStack(source.Id,effect.GetSkillTemplate().GetStack()))
            return Compatibility("triggered-child-host-stack-drift");
        var program=source.Programs.SingleOrDefault(p=>p.Position==template.GetPosition());
        if(program is null)return Compatibility("source-effect-family-or-position");
        if(!program.Supported)return Compatibility(program.UnsupportedReason!);
        if(host.Any(f=>InScope(f.GetName())&&f.HasConditions()))return Compatibility("conditional-host-core-program");
        var selected=host.Where(f=>!InScope(f.GetName())).ToList();
        foreach(var part in program.Parts)
        {
            int value=Value(part,numericLevel);
            // Native speed's flat operand is tenths of a metre/second;
            // managed SPEED uses thousandths. Preserve native overflow guards.
            if (part.Stat == StatEnum.SPEED && !part.Percent)
            {
                long scaled = (long)value * 100;
                if (scaled is < int.MinValue or > int.MaxValue) return Compatibility("speed-flat-host-width");
                value = (int)scaled;
            }
            selected.Add(part.Percent?new StatRateFunction(part.Stat,value,true):new StatAddFunction(part.Stat,value,true));
        }
        var marker=Selected.GetValue(effect,_=>new());
        lock(marker)marker.Programs[program.Position]=(host.ToArray(),selected.ToArray());
        if (retainedHostAdmission)
        {
            var warnings = Warnings.GetValue(caster, _ => []);
            lock (warnings) if (warnings.Add((effect.GetSkillId(), template.GetPosition(), "retained-host-admission")))
                Log.LogWarning("RETAIL_STAT_BUFF_ADMISSION_COMPAT npc={Npc} skill={Skill} effect={Effect} source={Source}; constant5.8 numeric program selected; existing4.8 cast admission retained and unverified",
                    caster.GetNpcId(), effect.GetSkillId(), template.GetPosition(), source.Sha256);
        }
        Log.LogDebug("RETAIL_STAT_BUFF npc={Npc} target={Target} skill={Skill} level={Level} numericLevel={NumericLevel} effect={Effect} source={Source} channels={Channels} binding={Binding}",
            caster.GetNpcId(),target.GetNpcId(),effect.GetSkillId(),effect.GetSkillLevel(),numericLevel,program.Position,source.Sha256,program.Parts.Length,
            retainedHostAdmission ? "retained-host-constant" : lineage.Length==0?"declared":string.Join(",",lineage.Select(p=>$"{p.ParentId}/{p.Position}>{p.ChildId}")));
        return selected;
    }
    private sealed class Boundary(string message):Exception(message);
}
