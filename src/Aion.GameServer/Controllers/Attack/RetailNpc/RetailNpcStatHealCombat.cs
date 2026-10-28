using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using TYPE=Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Source-bound NPC instant-heal numbers. The existing Effect owns
/// admission and Begin; CreatureLifeStats still owns the HP operation. Native
/// CheckResist/Begin/constructor bytes: external heal-stat-implementation-01.</summary>
internal static class RetailNpcStatHealCombat
{
    internal sealed record Part(int Position,int Delta,int Value,bool Percent,string? UnsupportedReason)
    { internal bool Supported=>UnsupportedReason is null; }
    internal sealed record Source(int Id,string Name,string Sha256,ImmutableArray<Part> Parts);
    private sealed record State(HealInstantEffect Template,Source Source,Part Part,
        SkyTempleNpcProfile CasterProfile,SkyTempleNpcProfile TargetProfile,int Level,
        int MaximumAtCheck,int Reserved,int HostReserved,EffectReserved Metadata);
    private static readonly ConditionalWeakTable<Effect,Dictionary<int,State>> States=new();
    private static readonly ConditionalWeakTable<Npc,HashSet<(int,int,string)>> Warnings=new();
    private static readonly ILogger Log=AionLog.For(nameof(RetailNpcStatHealCombat));

    internal static int? GetSelectedValue(Effect effect,int position)
    {
        if(!States.TryGetValue(effect,out var states))return null;
        lock(states)return states.TryGetValue(position,out var state)?state.Reserved:null;
    }

    internal static bool OwnsMetadata(Effect effect,EffectReserved metadata)
    {
        if(!States.TryGetValue(effect,out var states))return false;
        lock(states)return states.TryGetValue(metadata.GetPosition(),out var state)&&ReferenceEquals(state.Metadata,metadata);
    }

    internal static Source Parse(ReadOnlySpan<byte> bytes,string hash)
    {
        var identity=RetailNpcStatSkillCombat.ParseSource(bytes,hash);
        var root=XDocument.Parse(new UnicodeEncoding(false,false,true).GetString(bytes)).Root!;
        string? Leaf(string name)
        {
            var rows=root.Elements(name).ToArray();
            if(rows.Length==0)return null;
            if(rows.Length!=1||rows[0].HasElements||rows[0].HasAttributes)
                throw new InvalidDataException("Repeated/nonleaf heal operand: "+name);
            return rows[0].Value;
        }
        int Number(string name)
        {
            var text=Leaf(name);if(text is null)return 0;
            if(!int.TryParse(text,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int n))
                throw new InvalidDataException("Noninteger heal operand: "+name);
            return n;
        }
        var parts=ImmutableArray.CreateBuilder<Part>();
        for(int i=1;i<=8;i++)
        {
            string prefix="effect"+i+"_";
            if(!string.Equals(Leaf(prefix+"type"),"Heal_Instant",StringComparison.OrdinalIgnoreCase))continue;
            int delta=Number(prefix+"reserved1"),value=Number(prefix+"reserved2"),percent=Number(prefix+"reserved6");
            string? reason=Leaf("exclusive_attribute") is not (null or "0")?"unreviewed-heal-exclusive"
                :Number("apply_heal_boost_bonus")!=0?"unreviewed-heal-boost"
                :delta<0||value<0||percent is not (0 or 1)?"unreviewed-heal-base":null;
            parts.Add(new(i,delta,value,percent==1,reason));
        }
        return new(identity.Id,identity.Name,hash,parts.ToImmutable());
    }

    private static bool Compatibility(Npc caster,int skill,int position,string reason)
    {
        var warnings=Warnings.GetValue(caster,_=>new());
        lock(warnings)if(warnings.Add((skill,position,reason)))
            Log.LogWarning("RETAIL_STAT_HEAL_COMPAT npc={Npc} skill={Skill} effect={Position} reason={Reason}; host heal program retained",
                caster.GetNpcId(),skill,position,reason);
        return false;
    }

    // Called after ordinary EffectTemplate admission succeeds, before Java's
    // calculation-time missing-HP cap. Native clamps in the later HP operation.
    internal static bool TryCalculate(Effect effect,HealInstantEffect template)
    {
        if(template.GetType()!=typeof(HealInstantEffect)||effect.GetEffector() is not Npc caster
            ||!RetailNpcStatCatalog.TryGet(caster,out var casterProfile))return false;
        bool Boundary(string reason)=>Compatibility(caster,effect.GetSkillId(),template.Position,reason);
        if(effect.GetEffected() is not Npc target||!RetailNpcStatCatalog.TryGet(target,out var targetProfile))
            return Boundary("unreviewed-heal-target-context");
        // The no-boost Heal Check/Begin path has no NPC equipment-response
        // branch. It reads current maximum HP (NPC slot1e0) and target1050.
        // Do not copy the attack adapter's exclusive-counterparty gate here;
        // source-exclusive skills and unreviewed heal multipliers stay guarded.
        if(!RetailNpcStatCatalog.TryGetHeal(casterProfile,effect.GetSkillId(),effect.GetSkillLevel(),out var source))
            return Boundary("missing-exact-heal-binding");
        var part=source.Parts.SingleOrDefault(x=>x.Position==template.Position);
        if(part is null||!part.Supported)return Boundary(part?.UnsupportedReason??"missing-heal-part");
        if(template.Delta!=part.Delta||template.Value!=part.Value||template.percent!=part.Percent)
            return Boundary("host-heal-numeric-shape-differs");
        if(effect.GetSkillLevel() is <0 or >short.MaxValue)return Boundary("unreviewed-heal-level");
        // Constructor1050 is float1. Arbitrary modifier writers require their
        // own source proof; never infer a float multiplier from an int getter.
        if(target.GetGameStats().GetStatsSorted(StatEnum.HEAL_SKILL_DEBOOST).Any())
            return Boundary("unreviewed-current-heal-multiplier");
        int maximum=target.GetLifeStats().GetMaxHp();
        int reserved=RetailNpcInstantHealPlan.CalculateNoBoostReserved((short)effect.GetSkillLevel(),part.Delta,part.Value,part.Percent?1:0,maximum);
        if(reserved<0)return Boundary("unreviewed-negative-heal-conversion");
        int hostReserved=template.CalculateHealValue(effect,HealType.HP);
        // Native Check cache is separate from 4.8's packet-preview container.
        // Preview the bounded gain using native numbers; Begin uses the full
        // cached amount even if current HP changes before application.
        int preview=target.GetEffectController().IsAbnormalSet(AbnormalState.DISEASE)?0
            :Math.Min(reserved,Math.Max(0,maximum-target.GetLifeStats().GetCurrentHp()));
        var metadata=new EffectReserved(template.Position,preview,EffectReserved.ResourceType.HP,false);
        var state=new State(template,source,part,casterProfile,targetProfile,effect.GetSkillLevel(),maximum,reserved,hostReserved,metadata);
        var states=States.GetValue(effect,_=>new());lock(states)states[template.Position]=state;
        effect.SetReserveds(metadata,false);
        return true;
    }

    internal static bool TryApply(Effect effect,HealInstantEffect template)
    {
        if(!States.TryGetValue(effect,out var states))return false;
        State state;lock(states){if(!states.TryGetValue(template.Position,out state!))return false;}
        if(!ReferenceEquals(template,state.Template))throw new InvalidOperationException("Selected heal template changed after Check");
        var caster=(Npc)effect.GetEffector();var target=(Npc)effect.GetEffected();
        if(!RetailNpcStatCatalog.TryGet(caster,out var cp)||!ReferenceEquals(cp,state.CasterProfile)
            ||!RetailNpcStatCatalog.TryGet(target,out var tp)||!ReferenceEquals(tp,state.TargetProfile)
            ||target.GetGameStats().GetStatsSorted(StatEnum.HEAL_SKILL_DEBOOST).Any())
        {
            // Ownership/modifier changes retire this bounded numeric context.
            // Restore precisely the original host Check result, with a notice.
            effect.ReplaceStatisticsHealReservation(state.Metadata,new EffectReserved(template.Position,state.HostReserved,EffectReserved.ResourceType.HP,false));
            lock(states)states.Remove(template.Position);
            return Compatibility(caster,state.Source.Id,template.Position,"heal-context-changed-after-check");
        }
        int requested=RetailNpcInstantHealPlan.CalculateRequested(state.Reserved,1f);
        int before=target.GetLifeStats().GetCurrentHp(),maximum=target.GetLifeStats().GetMaxHp();
        bool dead=target.IsDead(),disease=target.GetEffectController().IsAbnormalSet(AbnormalState.DISEASE);
        // Retain the existing 4.8 HP/packet/callback owner and its disease/death
        // checks. No replacement life scheduler or unrelated player heal path.
        target.GetLifeStats().IncreaseHp(TYPE.REGULAR,requested,caster);
        Log.LogInformation("RETAIL_STAT_HEAL_INPUT {Inputs}",JsonSerializer.Serialize(new
        {
            npc=state.CasterProfile.NpcId,targetNpc=state.TargetProfile.NpcId,source=caster.GetObjectId(),target=target.GetObjectId(),
            world=target.GetWorldId(),sourceWorld=caster.GetWorldId(),skill=state.Source.Id,skillLevel=state.Level,position=state.Part.Position,
            profileHash=state.CasterProfile.SourceRecordSha256,targetProfileHash=state.TargetProfile.SourceRecordSha256,
            skillHash=state.Source.Sha256,generation=state.CasterProfile.Generation,delta=state.Part.Delta,value=state.Part.Value,
            percent=state.Part.Percent,maximumAtCheck=state.MaximumAtCheck,cachedBase=state.Reserved,targetMultiplier=1f,
            requested,before,maximumAtBegin=maximum,after=target.GetLifeStats().GetCurrentHp(),dead,disease,
            scope="source-bound-no-boost-NPC-heal; existing admission/HP owners; no arbitrary multiplier/protection/callback proof"
        }));
        return true;
    }
}
