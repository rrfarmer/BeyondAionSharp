using System.Runtime.CompilerServices;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using F = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using ChangeFunc = Aion.GameServer.SkillEngine.Change.Func;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Source-bound NPC spell drain. Native uses the post-protection
/// attack record and schedules caster healing at the hit word plus1000ms.</summary>
internal static class RetailNpcStatSpellDrain
{
    private static readonly ILogger Log=AionLog.For(nameof(RetailNpcStatSpellDrain));
    internal sealed class NpcInitialRetiredException(string reason):InvalidOperationException(reason);
    internal sealed record MpProgram(int Value,int Interval,int Duration,int EffectId,SkillElement Element,int CriticalPercent,int PreEffectPercent=100);
    internal sealed record StunProgram(bool NoResist);
    internal sealed record PeriodicSpellProgram(int Value);
    internal sealed record BlindProgram(int Chance);
    internal sealed record ParalyzeProgram;
    // The source-bound admission check is also re-evaluated at the delayed
    // curse boundary, after the independently scheduled primary hit.
    internal sealed record CurseProgram(string SourceSha256,RetailNpcCurseResourceOwner.Plan? Resources=null,
        Func<int,int,int>? FixtureDraw=null,Func<Player,Effect,RetailNpcCurseResourceOwner.Plan?>? FixturePlan=null,
        Func<Player,CurseResistanceInput>? FixtureResistance=null,
        Func<Player,RetailNpcPlayerResistanceSnapshot.Receipt?>? FixtureResistanceReceipt=null,
        Func<Player,Effect,bool>? AdmissionCheck=null);
    // These are explicitly supplied native operands for the offline fixture.
    // A production player-state/absolute-effect mapping is not yet available.
    internal readonly record struct CurseAbsolute(ushort Id,bool HasCurse,ushort Value);
    internal readonly record struct CurseModifiers(int Percent,int Flat);
    internal readonly record struct CurseResistanceInput(ushort Base,CurseModifiers Specific,CurseModifiers General,
        CurseAbsolute Buff,CurseAbsolute Debuff);

    internal static int CalculateCurseResistance(CurseResistanceInput input)
    {
        // Native first chooses the nonempty debuff record, else the buff,
        // before inspecting the selected record's curse-presence bit. A
        // debuff with no curse entry therefore suppresses the buff override.
        var selected=input.Debuff.Id!=0?input.Debuff:input.Buff;
        if(selected.Id!=0 && selected.HasCurse)return selected.Value;
        // GetBonusAttrTo accumulates signed dword pairs before the native
        // first-level calculation performs its single integer division.
        int percent=unchecked(input.Specific.Percent+input.General.Percent);
        int flat=unchecked(input.Specific.Flat+input.General.Flat);
        return checked((int)RetailNpcStatDefenses.Calculate(input.Base,percent,flat,0,9999));
    }
    internal sealed record NpcPeriodicProgram(int SourceLevel,
        RetailNpcStatPeriodicCombat.DrainOperands? Operands=null,Func<Npc,bool>? IsNativeEligible=null,long? CasterLife=null,
        long? TargetLife=null,NpcLifeStats? TargetLifeStats=null,
        Aion.GameServer.Controllers.ObserveController.NpcDrainTickProtectionReceipt? InitialProtection=null,
        NpcLifeStats? CasterLifeStats=null);
    internal sealed record Program(int HpPercent,bool Area,MpProgram? Mp=null,StunProgram? Stun=null,PeriodicSpellProgram? PeriodicSpell=null,BlindProgram? Blind=null,ParalyzeProgram? Paralyze=null,NpcPeriodicProgram? NpcPeriodic=null,CurseProgram? Curse=null);

    internal static bool CurseHostMatches(EffectTemplate template)
    {
        if(template is not CurseEffect curse || curse.GetType()!=typeof(CurseEffect) || curse.Position!=2
            ||curse.NoResist || curse.Duration1!=120 || curse.Duration2!=8500 || curse.Effectid!=20102
            ||curse.Element!=SkillElement.EARTH || curse.RandomTime!=0 || curse.BasicLvl!=0
            ||curse.PreEffects is not [1] || curse.PreEffectProb!=100 || curse.CritProbMod1!=0 || curse.CritProbMod2!=100
            ||curse.AccMod1!=0 || curse.AccMod2!=0 || curse.CritAddDmg1!=0 || curse.CritAddDmg2!=0
            ||curse.Hittype!=HitType.EVERYHIT || curse.HitTypeProb!=100 || curse.SkillEfficiency!=0
            ||curse.MaxDamageChance!=0 || curse.MaxDamageDelta!=0 || curse.Hoptype!=HopType.SKILLLV
            ||curse.HopA!=60 || curse.HopB!=60 || curse.maxstat || curse.EffectConditions is not null
            ||curse.EffectSubConditions is not null ||curse.SubEffectField is not null ||curse.Modifiers is not null
            ||curse.change is not {Count:2})return false;
        return curse.change[0] is {Stat:StatEnum.MAXHP,Func:ChangeFunc.PERCENT,Value:-35,Delta:0,Conditions:null}
            && curse.change[1] is {Stat:StatEnum.MAXMP,Func:ChangeFunc.PERCENT,Value:-35,Delta:0,Conditions:null};
    }

    internal static bool HasPreparedCurse(Effect effect)
        => States.TryGetValue(effect,out var state) && state.Program.Curse is not null
            && Volatile.Read(ref state.CurseAccepted)!=0 && effect.IsInSuccessEffects(2)
            && ReferenceEquals(effect.GetEffector(),state.Caster) && ReferenceEquals(effect.GetEffected(),state.Target);

    internal static bool TryGetCurseProgram(Effect effect,out CurseProgram program)
    {
        program=null!;
        if(!States.TryGetValue(effect,out var state)||state.Program.Curse is not { } selected)return false;
        program=selected;return true;
    }

    // The reached retail manager checks e1 and its probability before the
    // User immunity, magic-resistance and status-resistance calls. Selected
    // vampire sources carry e1 and the 0/100 probability operands. Actor
    // admission and other immunity contexts remain separate requirements.
    // For native User type 1 with the ordinary User vtable and the debug bypass
    // clear, both inherited immunity getters return zero. The selected three
    // sources also omit the skill-exclusive status attribute initialized to
    // zero; this does not establish target replacement or live actor identity.
    // Replacement and resource updates remain required before live admission.
    internal static bool TryCalculateCurse(Effect effect,EffectTemplate template,Func<int,int,int>? draw=null)
    {
        if(!States.TryGetValue(effect,out var state)||state.Program.Curse is not { } curse)return false;
        if(!SecondaryContextMatches(effect)||!CurseHostMatches(template)||effect.GetEffected() is not Player target
            ||!ReferenceEquals(target,state.Target))
            throw new InvalidOperationException("Selected vampire curse target or template changed");
        if(!effect.IsInSuccessEffects(1))return true;
        if(Interlocked.Exchange(ref state.CurseCalculated,1)!=0)return true;
        string? expectedSource=effect.GetSkillId() switch
        {
            16596=>"05fb066d3345c3c881a852ab89af252994a2b779752f70aeb935e92fc5b862a5",
            18459=>"46416d8ce4bb33386248b2ac7e2601408a2da11efa5a3cca2d8f2e9ed6ca9e49",
            18460=>"b9f134dc21f88bed08db52512c62ad4415a8965136ce6cf074e2b9f906d08a98",
            _=>null
        };
        if(expectedSource is null || !RetailNpcStatCatalog.TryGet(state.Caster,out var profile)
            ||!RetailNpcStatCatalog.TryGetSkill(profile,effect.GetSkillId(),effect.GetSkillLevel(),out var source)
            ||source.Sha256!=expectedSource || source.Sha256!=curse.SourceSha256)
            throw new InvalidOperationException("Selected vampire curse source changed");
        var roll=draw??curse.FixtureDraw??SkyTempleNpcCombat.Draw;
        if(!SecondaryPreEffectChance(100,roll))return true;
        var context=RetailNpcNormalStatusMath.Context.Create(SkyTempleCombatInputs.Scope(profile.NpcId),
            RetailNpcNormalScope.MainBinarySha256,"statistics-only prepared vampire curse",false);
        int magical=RetailNpcNormalStatusMath.CalculateMagicalResistThreshold(context,new(
            target.GetGameStats().GetMResist().GetCurrent(),state.Caster.GetGameStats().GetMAccuracy().GetCurrent(),
            (uint)profile.StatRatio,profile.Level,target.GetLevel(),SkyTempleCombatInputs.Pose(target)));
        if(target.GetObserveController().CheckAttackStatus(Aion.GameServer.Controllers.Attack.AttackStatus.RESIST)
            ||roll(1,1000)<=magical)return true;
        // Legacy fixtures without an explicit native input still exercise the
        // host's historical resistance; they do not establish retail parity.
        if(curse.FixtureResistance is not null && curse.FixtureResistanceReceipt is not null)
            throw new InvalidOperationException("Ambiguous vampire resistance fixture");
        var receipt=curse.FixtureResistanceReceipt?.Invoke(target);
        if(curse.FixtureResistanceReceipt is not null && receipt is null)
            throw new InvalidOperationException("Player resistance input retired");
        int resistance=receipt is not null ? CalculateCurseResistance(receipt.Input)
            : curse.FixtureResistance is { } inputs ? CalculateCurseResistance(inputs(target))
            : checked(target.GetGameStats().GetResistance(StatEnum.CURSE_RESISTANCE).GetCurrent()
                +target.GetGameStats().GetAbnormalResistance().GetCurrent());
        int penetration=state.Caster.GetGameStats().GetStat(StatEnum.CURSE_RESISTANCE_PENETRATION,0).GetCurrent();
        int threshold=SkyTempleNpcAbnormalEffects.StatusThreshold(resistance,penetration,SkyTempleCombatInputs.Pose(target));
        int statusRoll=roll(1,1000);
        if(receipt is not null && !receipt.IsCurrent(target))
            throw new InvalidOperationException("Player resistance input retired during decision");
        if(statusRoll<=threshold)return true;
        effect.AddSuccessEffect(template);template.CalculateDamage(effect);
        Volatile.Write(ref state.CurseAccepted,1);
        return true;
    }

    internal static bool ParalyzeHostMatches(EffectTemplate template)
        => template is ParalyzeEffect status && status.GetType()==typeof(ParalyzeEffect) && status.Position==3
            && !status.NoResist && status.Duration1==0 && status.Duration2==3000 && status.Effectid==20002
            && status.Element==SkillElement.FIRE && status.RandomTime==0 && status.BasicLvl==0
            && status.Value==0 && status.Delta==0 && status.PreEffects is [2] && status.PreEffectProb==50
            && status.Hittype==HitType.EVERYHIT && status.HitTypeProb==100
            && status.CritProbMod1==0 && status.CritProbMod2==100 && status.AccMod1==0 && status.AccMod2==0
            && status.CritAddDmg1==0 && status.CritAddDmg2==0 && status.SkillEfficiency==0
            && status.MaxDamageChance==0 && status.MaxDamageDelta==0
            && status.Hoptype==HopType.SKILLLV && status.HopA==50 && status.HopB==50
            && status.EffectConditions is null && status.EffectSubConditions is null
            && status.SubEffectField is null && status.Modifiers is null && status.change is not {Count:>0};

    internal static bool ParalyzeChecks(bool preceding,Func<int,int,int> draw,Action copyCritical,Func<bool> magical,Func<bool> status)
    {
        if(!preceding || !SecondaryPreEffectChance(50,draw))return false;
        copyCritical();
        return magical() && status();
    }

    internal static bool TryCalculateParalyze(Effect effect,EffectTemplate template,Func<int,int,int>? draw=null)
    {
        if(!States.TryGetValue(effect,out var state)||state.Program.Paralyze is null)return false;
        if(!SecondaryContextMatches(effect)||!ParalyzeHostMatches(template)||effect.GetEffected() is not Player target||!ReferenceEquals(target,state.Target))
            throw new InvalidOperationException("Selected drain paralyze target or template changed after preflight");
        if(!effect.IsInSuccessEffects(2))return true;
        if(Interlocked.Exchange(ref state.ParalyzeCalculated,1)!=0)return true;
        if(!effect.TryGetCalculatedMagicalCritical(out _))throw new InvalidOperationException("Selected drain paralyze lost primary critical cache");
        var roll=draw??SkyTempleNpcCombat.Draw;
        bool Magical()
        {
            if(!RetailNpcStatCatalog.TryGet(state.Caster,out var profile))throw new InvalidOperationException("Selected drain profile retired before paralyze");
            var context=RetailNpcNormalStatusMath.Context.Create(SkyTempleCombatInputs.Scope(profile.NpcId),
                RetailNpcNormalScope.MainBinarySha256,"statistics-only drain paralyze player status",false);
            int threshold=RetailNpcNormalStatusMath.CalculateMagicalResistThreshold(context,new(
                target.GetGameStats().GetMResist().GetCurrent(),state.Caster.GetGameStats().GetMAccuracy().GetCurrent(),
                (uint)profile.StatRatio,profile.Level,target.GetLevel(),SkyTempleCombatInputs.Pose(target)));
            return !target.GetObserveController().CheckAttackStatus(Aion.GameServer.Controllers.Attack.AttackStatus.RESIST)
                && roll(1,1000)>threshold;
        }
        bool Status()
        {
            int resistance=checked(target.GetGameStats().GetResistance(StatEnum.PARALYZE_RESISTANCE).GetCurrent()
                +target.GetGameStats().GetAbnormalResistance().GetCurrent());
            int penetration=state.Caster.GetGameStats().GetStat(StatEnum.PARALYZE_RESISTANCE_PENETRATION,0).GetCurrent();
            int threshold=SkyTempleNpcAbnormalEffects.StatusThreshold(resistance,penetration,SkyTempleCombatInputs.Pose(target));
            return roll(1,1000)>threshold;
        }
        if(ParalyzeChecks(true,roll,()=>effect.ReuseMagicalCritical(3),Magical,Status))
        {effect.AddSuccessEffect(template);template.CalculateDamage(effect);}
        return true;
    }

    internal static bool BlindHostMatches(BlindProgram source,EffectTemplate template)
        => template is BlindEffect blind && blind.GetType()==typeof(BlindEffect) && blind.Position==2
            && blind.NoResist && blind.Value==source.Chance && blind.Delta==0 && blind.Duration1==60
            && blind.Duration2==3000 && blind.Effectid==20107 && blind.Element==SkillElement.WIND
            && blind.RandomTime==0 && blind.BasicLvl==0 && blind.PreEffects is [1] && blind.PreEffectProb==100
            && blind.Hittype==HitType.EVERYHIT && blind.HitTypeProb==100
            && blind.CritProbMod1==0 && blind.CritProbMod2==100 && blind.CritAddDmg1==0 && blind.CritAddDmg2==0
            && blind.AccMod1==0 && blind.AccMod2==0 && blind.SkillEfficiency==0 && blind.MaxDamageChance==0 && blind.MaxDamageDelta==0
            && blind.Hoptype==HopType.SKILLLV && blind.HopA==60 && blind.HopB==60
            && blind.EffectConditions is null && blind.EffectSubConditions is null
            && blind.SubEffectField is null && blind.Modifiers is null && blind.change is not {Count:>0};

    internal static bool TryCheckBlindPreEffect(Effect effect,EffectTemplate template,out bool pass)
    {
        pass=false;
        if(!States.TryGetValue(effect,out var state)||state.Program.Blind is not { } blind||template.Position!=2)return false;
        if(!SecondaryContextMatches(effect)||!ReferenceEquals(effect.GetEffected(),state.Target)||!BlindHostMatches(blind,template))
            throw new InvalidOperationException("Selected drain blind target or template changed after preflight");
        pass=SecondaryPreEffectChance(SkyTempleNpcCombat.Draw);
        return true;
    }

    internal static bool IsSelectedBlind(Effect effect,EffectTemplate template)
    {
        if(!States.TryGetValue(effect,out var state)||state.Program.Blind is not { } blind)return false;
        if(!ReferenceEquals(effect.GetEffected(),state.Target)||!BlindHostMatches(blind,template))
            throw new InvalidOperationException("Selected drain blind target or template changed before activation");
        return true;
    }

    internal static bool BlindMiss(int chance,Func<int,int,int> draw)
    {
        if(chance is <0 or >100)throw new ArgumentOutOfRangeException(nameof(chance));
        int roll=draw(1,100);
        if(roll is <1 or >100)throw new ArgumentOutOfRangeException(nameof(draw));
        return roll<=chance;
    }

    internal static bool PeriodicSpellHostMatches(PeriodicSpellProgram source,EffectTemplate template)
        => template is SpellAttackEffect dot && dot.GetType()==typeof(SpellAttackEffect) && dot.Position==2
            && !dot.NoResist && dot.Value==source.Value && dot.Delta==0 && !dot.percent && !dot.shared
            && dot.checktime==1000 && dot.Duration1==0 && dot.Duration2==2000 && dot.Effectid==10190501
            && dot.Element==SkillElement.WIND && dot.RandomTime==0 && dot.BasicLvl==0
            && dot.Hittype==HitType.EVERYHIT && dot.HitTypeProb==100 && dot.PreEffects is [1]
            && dot.PreEffectProb==100 && dot.CritProbMod1==0 && dot.CritProbMod2==100
            && dot.CritAddDmg1==0 && dot.CritAddDmg2==0 && dot.AccMod1==0 && dot.AccMod2==0
            && dot.SkillEfficiency==0 && dot.MaxDamageChance==0 && dot.MaxDamageDelta==0
            && dot.Hoptype==HopType.SKILLLV && dot.HopA==60 && dot.HopB==60
            && dot.EffectConditions is null && dot.EffectSubConditions is null
            && dot.SubEffectField is null && dot.Modifiers is null && dot.change is not {Count:>0};

    internal static bool TryCalculatePeriodicSpell(Effect effect,SpellAttackEffect template)
    {
        if(!States.TryGetValue(effect,out var state)||state.Program.PeriodicSpell is not { } periodic)return false;
        if(!SecondaryContextMatches(effect)||!PeriodicSpellHostMatches(periodic,template)||effect.GetEffected() is not Player target||!ReferenceEquals(target,state.Target))
            throw new InvalidOperationException("Selected drain periodic target or template changed after preflight");
        if(!effect.IsInSuccessEffects(1))return true;
        if(Interlocked.Exchange(ref state.PeriodicCalculated,1)!=0)return true;
        if(!SecondaryPreEffectChance(SkyTempleNpcCombat.Draw))return true;
        // SpellAttack::CheckResist caches base and critical before magic resistance.
        // A failed source/host numeric preflight must not silently use Java damage.
        if(!RetailNpcStatPeriodicCombat.TryResolveCritical(effect,template))
            throw new InvalidOperationException("Selected drain periodic retail numeric owner unavailable");
        if(!RetailNpcStatCatalog.TryGet(state.Caster,out var profile))
            throw new InvalidOperationException("Selected drain profile retired before periodic resistance");
        var context=RetailNpcNormalStatusMath.Context.Create(SkyTempleCombatInputs.Scope(profile.NpcId),
            RetailNpcNormalScope.MainBinarySha256,"statistics-only drain periodic player resistance",false);
        int threshold=RetailNpcNormalStatusMath.CalculateMagicalResistThreshold(context,new(
            target.GetGameStats().GetMResist().GetCurrent(),state.Caster.GetGameStats().GetMAccuracy().GetCurrent(),
            (uint)profile.StatRatio,profile.Level,target.GetLevel(),SkyTempleCombatInputs.Pose(target)));
        if(!target.GetObserveController().CheckAttackStatus(Aion.GameServer.Controllers.Attack.AttackStatus.RESIST)
            && SkyTempleNpcCombat.Draw(1,1000)>threshold)
        { effect.AddSuccessEffect(template);template.CalculateDamage(effect); }
        return true;
    }

    internal static bool StunHostMatches(StunProgram source,EffectTemplate template)
        => template is StunEffect stun && stun.GetType()==typeof(StunEffect) && stun.Position==2
            && stun.NoResist==source.NoResist && stun.Value==0 && stun.Delta==0
            && stun.Duration1==0 && stun.Duration2==3000 && stun.Effectid==20000
            && stun.Element==SkillElement.EARTH && stun.RandomTime==0 && stun.PreEffects is [1]
            && stun.PreEffectProb==100 && stun.CritProbMod2==100
            && stun.Hoptype==HopType.SKILLLV && stun.HopA==60 && stun.HopB==60
            && stun.EffectConditions is null && stun.EffectSubConditions is null
            && stun.SubEffectField is null && stun.Modifiers is null && stun.change is not {Count:>0};

    // A0 -> magic CheckResist -> B0. The noresist flag bypasses the
    // latter two checks, not the successful-primary or preeffect draw.
    internal static bool StunChecks(bool primary,bool noResist,Func<int,int,int> draw,Action copyCritical,
        Func<bool> magical,Func<bool> status)
    {
        if(!primary)return false;
        if(!SecondaryPreEffectChance(draw))return false;
        copyCritical();
        return noResist || magical() && status();
    }

    internal static bool TryCalculateStun(Effect effect,EffectTemplate template)
    {
        if(!States.TryGetValue(effect,out var state)||state.Program.Stun is not { } stun)return false;
        if(!StunHostMatches(stun,template) || effect.GetEffected() is not Player target || !ReferenceEquals(target,state.Target))
            throw new InvalidOperationException("Selected drain stun changed after preflight");
        if(!effect.IsInSuccessEffects(1))return true;
        if(Interlocked.Exchange(ref state.StunCalculated,1)!=0)return true;
        if(!effect.TryGetCalculatedMagicalCritical(out _))throw new InvalidOperationException("Selected drain stun lost primary critical cache");
        bool Magical()
        {
            if(!RetailNpcStatCatalog.TryGet(state.Caster,out var profile))throw new InvalidOperationException("Selected drain profile retired before stun");
            var context=RetailNpcNormalStatusMath.Context.Create(SkyTempleCombatInputs.Scope(profile.NpcId),
                RetailNpcNormalScope.MainBinarySha256,"statistics-only drain stun player status",false);
            int threshold=RetailNpcNormalStatusMath.CalculateMagicalResistThreshold(context,new(
                target.GetGameStats().GetMResist().GetCurrent(),state.Caster.GetGameStats().GetMAccuracy().GetCurrent(),
                (uint)profile.StatRatio,profile.Level,target.GetLevel(),SkyTempleCombatInputs.Pose(target)));
            return !target.GetObserveController().CheckAttackStatus(Aion.GameServer.Controllers.Attack.AttackStatus.RESIST)
                && SkyTempleNpcCombat.Draw(1,1000)>threshold;
        }
        bool Status()
        {
            int resistance=checked(target.GetGameStats().GetResistance(StatEnum.STUN_RESISTANCE).GetCurrent()
                +target.GetGameStats().GetAbnormalResistance().GetCurrent());
            int penetration=state.Caster.GetGameStats().GetStat(StatEnum.STUN_RESISTANCE_PENETRATION,0).GetCurrent();
            int threshold=SkyTempleNpcAbnormalEffects.StatusThreshold(resistance,penetration,SkyTempleCombatInputs.Pose(target));
            return SkyTempleNpcCombat.Draw(1,1000)>threshold;
        }
        if(StunChecks(true,stun.NoResist,SkyTempleNpcCombat.Draw,()=>effect.ReuseMagicalCritical(2),Magical,Status))
        {effect.AddSuccessEffect(template);template.CalculateDamage(effect);}
        return true;
    }

    internal static bool MpHostMatches(MpProgram source,EffectTemplate template)
        => template is MpAttackEffect mp && mp.GetType()==typeof(MpAttackEffect) && mp.Position==2
            && mp.NoResist && mp.Value==source.Value && mp.Delta==0 && !mp.percent && !mp.shared
            && mp.checktime==source.Interval && mp.Duration1==0 && mp.Duration2==source.Duration
            && mp.Effectid==source.EffectId && mp.Element==source.Element && mp.RandomTime==0
            && mp.PreEffects is [1] && mp.PreEffectProb==source.PreEffectPercent && mp.CritProbMod2==source.CriticalPercent
            && mp.Hoptype==HopType.SKILLLV && mp.HopA==60 && mp.HopB==60
            && mp.EffectConditions is null && mp.EffectSubConditions is null
            && mp.SubEffectField is null && mp.Modifiers is null && mp.change is not {Count:>0};

    internal static bool SecondaryContextMatches(Effect effect)
        => !effect.IsForcedEffect() && effect.GetSkillTemplate().GetProperties()?.GetFirstTarget()
            == Aion.GameServer.SkillEngine.Properties.FirstTargetAttribute.TARGET;

    // Native SkillEffectMgr checks preceding success. An effective probability
    // of zero bypasses the draw; nonzero values draw inclusive 0..100.
    internal static bool SecondaryPreEffectChance(Func<int,int,int> draw) => draw(0,100)<=100;
    internal static bool SecondaryPreEffectChance(int probability,Func<int,int,int> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        if(probability is <0 or >100)throw new ArgumentOutOfRangeException(nameof(probability));
        if(probability==0)return true;
        int roll=draw(0,100);if(roll is <0 or >100)throw new ArgumentOutOfRangeException(nameof(draw));
        return roll<=probability;
    }
    internal static bool TryCheckMpPreEffect(Effect effect,EffectTemplate template,out bool pass)
    {
        pass=false;
        if(!States.TryGetValue(effect,out var state)||state.Program.Mp is not { } mp||template.Position!=2)return false;
        if(!ReferenceEquals(effect.GetEffected(),state.Target)||!MpHostMatches(mp,template))
            throw new InvalidOperationException("Selected drain MP target or template changed after preflight");
        pass=SecondaryPreEffectChance(mp.PreEffectPercent,SkyTempleNpcCombat.Draw);
        return true;
    }
    private sealed record State(EffectTemplate Template,Npc Caster,Creature Target,Program Program,int Damage,ushort HitWord,
        Aion.GameServer.Controllers.ObserveController.PlayerDrainProtectionReceipt? PlayerProtection)
    { internal int Scheduled,InitialCommitted,StunCalculated,PeriodicCalculated,ParalyzeCalculated,CurseCalculated,CurseAccepted; }
    private static readonly ConditionalWeakTable<Effect,State> States=new();

    // Only dispatches that carry an HP-commit receipt use this gate. Other
    // prepared fixtures can have a separately scheduled primary and must keep
    // their existing timeline. An accepted commit cannot later be aborted by
    // either guarded initial-dispatch branch.
    internal static bool HasGuardedInitial(Effect effect)
        => States.TryGetValue(effect,out var state)
            && (state.PlayerProtection is not null || state.Program.NpcPeriodic is not null);

    internal static bool InitialCommitPending(Effect effect)
        => States.TryGetValue(effect,out var state)
            && (state.PlayerProtection is not null || state.Program.NpcPeriodic is not null)
            && Volatile.Read(ref state.InitialCommitted)==0;

    // Shared timeline for source-pinned vampire casts. Production currently
    // admits only the reviewed single-target sources with selected inputs;
    // the area source and other actor contexts remain separately gated.
    private sealed class VampireTimeline(State state)
    {
        internal readonly State State=state;
        internal int CurseScheduled;
    }
    private static readonly ConditionalWeakTable<Effect,VampireTimeline> VampireTimelines=new();
    internal static bool HasDeferredVampireTimeline(Effect effect)=>VampireTimelines.TryGetValue(effect,out _);
    private sealed class KasikaTimeline(State state)
    {
        internal readonly State State=state;
        internal int DefenseScheduled;
    }
    private static readonly ConditionalWeakTable<Effect,KasikaTimeline> KasikaTimelines=new();

    internal static void AttachKasikaTimelineForOfflineFixture(Effect effect)
    {
        if(!States.TryGetValue(effect,out var state) ||effect.GetSkillId()!=17860 ||state.Program.HpPercent!=200
            ||state.Target is not Player ||!ReferenceEquals(effect.GetEffector(),state.Caster)
            ||!ReferenceEquals(effect.GetEffected(),state.Target) ||effect.RetailHasEnded
            ||!effect.IsInSuccessEffects(1) ||!effect.IsInSuccessEffects(2)
            ||!RetailNpc17860DefenseEffect.IsPreparedForOfflineFixture(effect))
            throw new InvalidOperationException("Kasika timeline requires a complete prepared fixture");
        KasikaTimelines.Add(effect,new(state));
    }

    internal static bool TryScheduleKasikaDefenseForOfflineFixture(Effect effect,StatdownEffect template,Action apply)
    {
        if(!KasikaTimelines.TryGetValue(effect,out var timeline))return false;
        var state=timeline.State;
        if(!ReferenceEquals(effect.GetEffected(),state.Target) ||!ReferenceEquals(effect.GetEffector(),state.Caster)
            ||!ReferenceEquals(effect.EffectInPos(2),template))
            throw new InvalidOperationException("Kasika timeline defense identity changed");
        if(Interlocked.Exchange(ref timeline.DefenseScheduled,1)!=0)return true;
        void Run()
        {
            if(effect.RetailHasEnded ||!ReferenceEquals(effect.GetEffected(),state.Target)
                ||!state.Target.IsSpawned() ||state.Target.IsDead())return;
            apply();
        }
        if(state.HitWord==0)Run();
        else ThreadPoolManager.GetInstance().Schedule(_=>{Run();return ValueTask.CompletedTask;},TimeSpan.FromMilliseconds(state.HitWord));
        return true;
    }

    internal static void AttachVampireTimelineForOfflineFixture(Effect effect)
    {
        if(!States.TryGetValue(effect,out var state) || state.Program.Curse is null
            || effect.GetSkillId() is not (16596 or 18459 or 18460)
            || state.Target is not Player || !ReferenceEquals(effect.GetEffector(),state.Caster)
            || !ReferenceEquals(effect.GetEffected(),state.Target) || effect.RetailHasEnded)
            throw new InvalidOperationException("Vampire timeline requires a prepared selected fixture");
        lock(VampireTimelines)
        {
            if(VampireTimelines.TryGetValue(effect,out _))
                throw new InvalidOperationException("Vampire timeline already attached");
            VampireTimelines.Add(effect,new(state));
        }
    }

    internal static bool TryScheduleVampireCurseForOfflineFixture(Effect effect,CurseEffect template,Action apply)
    {
        if(!VampireTimelines.TryGetValue(effect,out var timeline))return false;
        var state=timeline.State;
        if(!CurseHostMatches(template) || !ReferenceEquals(effect.GetEffector(),state.Caster)
            || !ReferenceEquals(effect.GetEffected(),state.Target))
            throw new InvalidOperationException("Vampire timeline curse identity changed");
        if(Interlocked.Exchange(ref timeline.CurseScheduled,1)!=0)return true;
        void Run()
        {
            if(effect.RetailHasEnded || effect.PreparedNpcDrainInitialAborted || !ReferenceEquals(effect.GetEffected(),state.Target)
                || !state.Target.IsSpawned() || state.Target.IsDead())return;
            if(InitialCommitPending(effect))
            {
                Log.LogWarning("RETAIL_NPC_DRAIN_CURSE_SKIPPED npc={Npc} skill={Skill} reason=initial-hit-pending",
                    state.Caster.GetNpcId(),effect.GetSkillId());
                return;
            }
            var check=state.Program.Curse?.AdmissionCheck;
            if(check is not null && (state.Target is not Player target ||!check(target,effect)))
            {
                Log.LogWarning("RETAIL_NPC_DRAIN_CURSE_SKIPPED npc={Npc} skill={Skill} reason=player-input-changed-before-curse",
                    state.Caster.GetNpcId(),effect.GetSkillId());
                return;
            }
            try {apply();}
            catch(RetailNpcCurseResourceOwner.PlayerInputChangedException ex) when(check is not null)
            {
                Log.LogWarning("RETAIL_NPC_DRAIN_CURSE_SKIPPED npc={Npc} skill={Skill} reason=player-input-changed-during-curse detail={Detail}",
                    state.Caster.GetNpcId(),effect.GetSkillId(),ex.Message);
            }
        }
        if(state.HitWord==0)Run();
        else ThreadPoolManager.GetInstance().Schedule(_=>{Run();return ValueTask.CompletedTask;},TimeSpan.FromMilliseconds(state.HitWord));
        return true;
    }

    // Only the explicitly prepared NPC pair may create this first-hit work.
    // It carries the calculated protection snapshot into the target HP owner.
    internal sealed class NpcInitialDispatch
    {
        private readonly Effect effect;private readonly Npc caster,target;private readonly int damage;
        private readonly NpcPeriodicProgram program;private int claimed,entered,committed;
        internal NpcInitialDispatch(Effect effect,Npc caster,Npc target,int damage,NpcPeriodicProgram program)
        {this.effect=effect;this.caster=caster;this.target=target;this.damage=damage;this.program=program;}
        internal Effect Effect=>effect;internal Npc Caster=>caster;internal int Damage=>damage;
        internal bool Committed=>Volatile.Read(ref committed)!=0;
        private void Validate(Creature current)
        {
            if(!ReferenceEquals(current,target)||!ReferenceEquals(effect.GetEffected(),target)
                ||!ReferenceEquals(effect.GetEffector(),caster)||effect.RetailHasEnded
                ||!target.IsSpawned()||!caster.IsSpawned()||target.IsDead()||caster.IsDead()
                ||target.GetLifeStats().GetCurrentHp()<=0||caster.GetLifeStats().GetCurrentHp()<=0
                ||target.GetController().RetailLifeEpoch!=program.TargetLife
                ||caster.GetController().RetailLifeEpoch!=program.CasterLife
                ||!ReferenceEquals(target.GetLifeStats(),program.TargetLifeStats)
                ||!ReferenceEquals(caster.GetLifeStats(),program.CasterLifeStats))
                throw new NpcInitialRetiredException("Prepared NPC drain initial actor retired");
        }
        internal void Claim(Creature current)
        { Validate(current);if(Interlocked.Exchange(ref claimed,1)!=0)throw new InvalidOperationException("NPC drain initial replay"); }
        internal IDisposable EnterHp(Creature current)
        {
            Validate(current);
            if(Volatile.Read(ref claimed)!=1||Interlocked.Exchange(ref entered,1)!=0
                ||program.InitialProtection is null||program.CasterLifeStats is not { } casterLife
                ||program.IsNativeEligible is null)throw new InvalidOperationException("Invalid NPC drain initial HP work");
            // Do not wait for the other NPC's HP lock while holding the target
            // HP lock: opposing drains may acquire the two actors in reverse.
            if(!Monitor.TryEnter(casterLife))throw new NpcInitialRetiredException("Prepared NPC drain caster life busy");
            try
            {
                Validate(current);
                if(!program.IsNativeEligible(caster))throw new NpcInitialRetiredException("Prepared NPC drain initial caster retired");
                var protection=target.GetObserveController().EnterNpcDrainTickHpForOfflineFixture(program.InitialProtection,target);
                return new NpcInitialHpPermit(casterLife,protection);
            }
            catch { Monitor.Exit(casterLife);throw; }
        }
        private sealed class NpcInitialHpPermit(NpcLifeStats casterLife,IDisposable protection):IDisposable
        {
            public void Dispose() { try {protection.Dispose();} finally {Monitor.Exit(casterLife);} }
        }
        internal void MarkCommitted(Creature current)
        {
            if(!ReferenceEquals(current,target)||!Monitor.IsEntered(target.GetLifeStats())||Volatile.Read(ref entered)!=1
                ||Interlocked.Exchange(ref committed,1)!=0)throw new InvalidOperationException("Invalid NPC drain initial HP commit");
        }
    }

    internal static int HealAmount(int protectedDamage,int percent)
        => HealAmount(protectedDamage,0,0,percent);

    // Both drain callbacks multiply the signed 16-bit source level and rate,
    // add the constant, and multiply the damage in 32-bit arithmetic before
    // converting the product to float and dividing by 100.
    internal static int HealAmount(int damage,int level,int rate,int percent)
    {
        if(damage<=0)return 0;
        if(level is <0 or >short.MaxValue)throw new ArgumentOutOfRangeException(nameof(level));
        int factor=unchecked((short)level*rate+percent);
        int product=unchecked(damage*factor); // Native IMUL32 before FLOAT conversion.
        return F.Cvtt(F.Div(F.FromInt(product),100f));
    }

    // IntervalEffect squares the authored integer radius in 32-bit arithmetic,
    // then compares binary32 (dy*dy + dx*dx) + dz*dz in the same world.
    internal static bool PeriodicHealWithinRange(bool sameWorld,int radius,
        float casterX,float casterY,float casterZ,float targetX,float targetY,float targetZ)
    {
        if(!sameWorld)return false;
        float x=F.Sub(casterX,targetX),y=F.Sub(casterY,targetY),z=F.Sub(casterZ,targetZ);
        float distance=F.Add(F.Add(F.Mul(y,y),F.Mul(x,x)),F.Mul(z,z));
        float bound=F.FromInt(unchecked(radius*radius));
        return bound>=distance;
    }

    // Called after the HP owner's atomic receipt. The native eligibility
    // flag still needs a proven host mapping before production admission.
    internal static bool ApplyNpcPeriodicHpHeal(Effect effect,Npc caster,Npc target,
        RetailNpcStatPeriodicCombat.DrainOperands source,int level,int? committedDamage,bool nativeCasterEligible)
        => ApplyNpcPeriodicHpHeal(effect,caster,target,source,level,committedDamage,()=>nativeCasterEligible);

    internal static bool ApplyNpcPeriodicHpHeal(Effect effect,Npc caster,Npc target,
        RetailNpcStatPeriodicCombat.DrainOperands source,int level,int? committedDamage,Func<bool> nativeCasterEligible)
    {
        ArgumentNullException.ThrowIfNull(nativeCasterEligible);
        if(!ReferenceEquals(effect.GetEffector(),caster)||!ReferenceEquals(effect.GetEffected(),target))
            throw new InvalidOperationException("NPC drain heal actor identity differs");
        if(source.Radius is not { } radius || source.HpRate is not { } rate || source.HpPercent is not { } percent
            || source.MpRate.GetValueOrDefault()!=0 || source.MpPercent.GetValueOrDefault()!=0)
            throw new InvalidOperationException("Unreviewed NPC periodic drain heal operands");
        if(committedDamage is not >0 || !caster.IsSpawned() || caster.IsDead())return false;
        if(!PeriodicHealWithinRange(ReferenceEquals(caster.GetWorldMapInstance(),target.GetWorldMapInstance()),radius,
            caster.GetX(),caster.GetY(),caster.GetZ(),target.GetX(),target.GetY(),target.GetZ()))return false;
        int heal=HealAmount(committedDamage.Value,level,rate,percent);
        // Negative native Heal2 amounts need a separately reviewed resource owner.
        if(heal<0)throw new InvalidOperationException("Unreviewed negative NPC periodic drain heal");
        if(heal==0)return false;
        return caster.GetLifeStats().IncreaseRetailNpcDrainHp(heal,effect,
            ()=>caster.IsSpawned() && nativeCasterEligible());
    }

    internal static void Prepare(Effect effect,EffectTemplate template,Program program,
        Aion.GameServer.Controllers.ObserveController.PlayerDrainProtectionReceipt? playerProtection=null)
    {
        int damage=effect.GetReserveds(template.Position).GetValue();
        var state=new State(template,(Npc)effect.GetEffector(),effect.GetEffected(),program,damage,checked((ushort)effect.GetSkill().GetHitTime()),playerProtection);
        var selected=States.GetValue(effect,_=>state);
        if(!ReferenceEquals(selected.Template,template)||!ReferenceEquals(selected.Caster,state.Caster)
            ||!ReferenceEquals(selected.Target,state.Target)||selected.Damage!=state.Damage||selected.HitWord!=state.HitWord||selected.Program!=program
            ||selected.PlayerProtection!=playerProtection)
            throw new InvalidOperationException("Selected drain inputs changed after preparation");
    }

    internal sealed class PlayerInitialDispatch(Effect effect,Npc caster,Player target,int damage,
        Aion.GameServer.Controllers.ObserveController.PlayerDrainProtectionReceipt protection)
    {
        private int claimed,entered,committed;
        internal Effect Effect=>effect;
        internal Npc Caster=>caster;
        internal int Damage=>damage;
        internal bool Committed=>Volatile.Read(ref committed)!=0;
        private void Validate(Creature current)
        {
            if(!ReferenceEquals(current,target)||!ReferenceEquals(effect.GetEffected(),target)
                ||!ReferenceEquals(effect.GetEffector(),caster)||effect.RetailHasEnded
                ||!target.IsSpawned()||target.IsDead()||!ReferenceEquals(target.GetLifeStats(),protection.Life)
                ||target.GetController().RetailLifeEpoch!=protection.LifeEpoch)
                throw new Aion.GameServer.Controllers.ObserveController.PlayerDrainProtectionChangedException();
        }
        internal void Claim(Creature current)
        {
            Validate(current);
            if(Interlocked.Exchange(ref claimed,1)!=0)throw new InvalidOperationException("Player drain initial replay");
        }
        internal IDisposable EnterHp(Creature current)
        {
            Validate(current);
            if(Volatile.Read(ref claimed)!=1||Interlocked.Exchange(ref entered,1)!=0)
                throw new InvalidOperationException("Player drain HP entry replay");
            return protection.Owner.EnterPlayerDrainHp(protection,target);
        }
        internal void MarkCommitted(Creature current)
        {
            if(!ReferenceEquals(current,target)||Volatile.Read(ref entered)!=1||Interlocked.Exchange(ref committed,1)!=0)
                throw new InvalidOperationException("Invalid player drain HP commit");
        }
    }

    internal static bool TryGetPreparedNpcPeriodic(Effect effect,out int sourceLevel)
    {
        sourceLevel=0;
        if(!TryGetPreparedNpcPeriodicProgram(effect,out var periodic))return false;
        sourceLevel=periodic.SourceLevel;
        return true;
    }

    internal static bool TryGetPreparedNpcPeriodicProgram(Effect effect,out NpcPeriodicProgram periodic)
    {
        periodic=null!;
        if(!States.TryGetValue(effect,out var state) || state.Program.NpcPeriodic is not { } selected)return false;
        if(state.Target is not Npc || !ReferenceEquals(effect.GetEffector(),state.Caster)
            ||!ReferenceEquals(effect.GetEffected(),state.Target))
            throw new InvalidOperationException("Selected NPC drain actors changed after preparation");
        periodic=selected;
        return true;
    }

    internal static NpcPeriodicProgram RequireNpcPeriodicProgram(Effect effect,EffectTemplate template)
    {
        if(!TryGetPreparedNpcPeriodicProgram(effect,out var periodic)
            || periodic.Operands is not { } source || periodic.IsNativeEligible is null || periodic.CasterLife is null
            || template.GetType()!=typeof(SpellAtkDrainEffect) || template.Position!=2
            || source.PreEffect!="e1" || source.PreEffectProbability1!=0 || source.PreEffectProbability2!=100)
            throw new InvalidOperationException("Incomplete selected NPC periodic drain program");
        return periodic;
    }

    internal static bool TryCheckNpcPeriodicPreEffect(Effect effect,EffectTemplate template,out bool pass)
    {
        pass=false;
        if(!TryGetPreparedNpcPeriodicProgram(effect,out _) || template.Position!=2)return false;
        var program=RequireNpcPeriodicProgram(effect,template);
        pass=SecondaryPreEffectChance(program.Operands!.PreEffectProbability2!.Value,SkyTempleNpcCombat.Draw);
        return true;
    }

    internal static bool TryApply(Effect effect,EffectTemplate template,Action applyDamage)
    {
        if(!States.TryGetValue(effect,out var state))return false;
        if(!ReferenceEquals(state.Template,template)||!ReferenceEquals(state.Target,effect.GetEffected()))
            throw new InvalidOperationException("Selected drain target or template changed");
        if(Interlocked.Exchange(ref state.Scheduled,1)!=0)return true;
        bool CommitPlayer(Aion.GameServer.Controllers.ObserveController.PlayerDrainProtectionReceipt protection)
        {
            if(state.Target is not Player player)throw new InvalidOperationException("Player drain target changed");
            var initial=new PlayerInitialDispatch(effect,state.Caster,player,state.Damage,protection);
            try { player.GetController().OnRetailPlayerDrainAttack(initial,template.Hoptype); }
            catch(Aion.GameServer.Controllers.ObserveController.PlayerDrainProtectionChangedException) when(!initial.Committed)
            {
                effect.AbortPreparedNpcDrainInitial();
                Log.LogWarning("RETAIL_NPC_DRAIN_INITIAL_ABORT npc={Npc} skill={Skill} reason=player-protection-or-life-changed",
                    state.Caster.GetNpcId(),effect.GetSkillId());
                return false;
            }
            if(!initial.Committed)
            {
                effect.AbortPreparedNpcDrainInitial();
                Log.LogWarning("RETAIL_NPC_DRAIN_INITIAL_ABORT npc={Npc} skill={Skill} reason=player-hit-not-committed",
                    state.Caster.GetNpcId(),effect.GetSkillId());
                return false;
            }
            Volatile.Write(ref state.InitialCommitted,1);
            if(effect.GetSkillTemplate().GetActivationAttribute()!=ActivationAttribute.PROVOKED)
                state.Caster.GetObserveController().NotifyAttackObservers(player,effect.GetSkillId());
            return true;
        }
        void ScheduleHeal(int delay)
        {
            int heal=HealAmount(state.Damage,state.Program.HpPercent);
            if(heal<=0)return;
            ThreadPoolManager.GetInstance().Schedule(_=>
            {
                if(state.Program.NpcPeriodic is { } prepared)
                {
                    if(prepared.CasterLifeStats is not { } originalLife)
                        throw new InvalidOperationException("Prepared NPC drain heal has no captured life owner");
                    originalLife.IncreasePreparedNpcInstantDrainHp(heal,effect);
                }
                else state.Caster.GetLifeStats().IncreaseRetailNpcInstantDrainHp(heal,effect);
                return ValueTask.CompletedTask;
            },TimeSpan.FromMilliseconds(delay));
        }
        if(HasDeferredVampireTimeline(effect) && state.PlayerProtection is { } deferredProtection)
        {
            int impact=SkyTempleNpcSkillTiming.PrimaryDelay(state.HitWord);
            // Retail BeginEffect schedules DelayedDrain from the already
            // calculated (post-protection) amount, independently of whether
            // DelayedSkillDamage later reaches its target. See native
            // SkillMagicalAttack 140bb8e50 and BeginEffect 140bc2670.
            ThreadPoolManager.GetInstance().Schedule(_=>
            {
                CommitPlayer(deferredProtection);
                return ValueTask.CompletedTask;
            },TimeSpan.FromMilliseconds(impact));
            ScheduleHeal(state.HitWord+1000);
            return true;
        }
        if(state.Program.NpcPeriodic is { } npcProgram)
        {
            if(state.Target is not Npc npcTarget||npcProgram.TargetLife is null||npcProgram.TargetLifeStats is null
                ||npcProgram.CasterLife is null||npcProgram.CasterLifeStats is null||npcProgram.InitialProtection is null)
                throw new InvalidOperationException("Incomplete prepared NPC drain initial owner");
            var initial=new NpcInitialDispatch(effect,state.Caster,npcTarget,state.Damage,npcProgram);
            try
            {
                npcTarget.GetController().OnPreparedNpcDrainInitialAttack(initial,template.Hoptype);
            }
            catch(Exception ex) when(!initial.Committed && (ex is NpcInitialRetiredException
                or Aion.GameServer.Controllers.ObserveController.NpcDrainProtectionChangedException))
            {
                effect.AbortPreparedNpcDrainInitial();
                Log.LogWarning("RETAIL_NPC_DRAIN_INITIAL_ABORT npc={Npc} skill={Skill} reason={Reason}",
                    state.Caster.GetNpcId(),effect.GetSkillId(),ex.Message);
                return true;
            }
            if(!initial.Committed)
            {
                effect.AbortPreparedNpcDrainInitial();
                Log.LogWarning("RETAIL_NPC_DRAIN_INITIAL_ABORT npc={Npc} skill={Skill} reason=hit-not-committed",
                    state.Caster.GetNpcId(),effect.GetSkillId());
                return true;
            }
            Volatile.Write(ref state.InitialCommitted,1);
            state.Caster.GetObserveController().NotifyAttackObservers(npcTarget,effect.GetSkillId());
        }
        else if(state.PlayerProtection is { } protection)
        {
            if(!CommitPlayer(protection))return true;
        }
        else if(VampireTimelines.TryGetValue(effect,out _) ||KasikaTimelines.TryGetValue(effect,out _))
        {
            // Retail's ordinary primary path schedules the hit separately from
            // the synchronized curse and the later caster-heal callback.
            ThreadPoolManager.GetInstance().Schedule(_=>
            {
                if(ReferenceEquals(effect.GetEffector(),state.Caster) && ReferenceEquals(effect.GetEffected(),state.Target)
                    && state.Target.IsSpawned() && !state.Target.IsDead())applyDamage();
                return ValueTask.CompletedTask;
            },TimeSpan.FromMilliseconds(SkyTempleNpcSkillTiming.PrimaryDelay(state.HitWord)));
        }
        else applyDamage();
        ScheduleHeal(state.HitWord+1000);
        return true;
    }
}
