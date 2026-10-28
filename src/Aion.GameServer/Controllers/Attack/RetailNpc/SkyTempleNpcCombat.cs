using System.Security.Cryptography;
using Aion.GameServer.Commons.Utils.RetailRandom;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.Utils.Stats;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Utils;
using System.Globalization;
using System.Runtime.CompilerServices;
using Aion.GameServer.Ai.Lifecycle;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Live, narrowly scoped retail calculation adapter. HP, observer dispatch, shields, death,
/// packets and player gear remain owned by the existing4.8 event path. No raw-memory replay.
/// </summary>
internal static partial class SkyTempleNpcCombat
{
    private sealed class UnsupportedTarget { internal int Target; internal long Life=-1; }
    private static readonly ConditionalWeakTable<Npc,UnsupportedTarget> UnsupportedTargets=new();
    // The new admission domain covers ordinary NPC <-> Player interactions.
    // A non-player target must not fall through to legacy Attack=0/Matk=0 bases.
    // No extra task is created; the existing bounded chase owner retires the attempt.
    internal static bool RejectUnverifiedTarget(Creature source,Creature? target)
    {
        if(target is null||source is not Npc npc||target is Player||!SkyTempleNpcProfileCatalog.TryGet(npc,out var p)||p.Cohort is null)return false;
        var state=UnsupportedTargets.GetValue(npc,_=>new());
        lock(state)
        {
            long life=npc.GetController().RetailLifeEpoch;
            if(state.Target!=target.GetObjectId()||state.Life!=life)
            {
                state.Target=target.GetObjectId();state.Life=life;
                Log.LogWarning("RETAIL_COHORT_UNSUPPORTED_TARGET npc={Npc} template={Template} target={Target} targetType={Type} cohort={Cohort}; attack rejected before legacy fallback",
                    npc.GetObjectId(),p.NpcId,target.GetObjectId(),target.GetType().Name,p.Cohort.Id);
            }
        }
        return true;
    }

    internal readonly record struct NormalTiming(int ImpactDelayMs,int CadenceMs,int MotionVariant);
    // Source reconstruction, not an observed native animation cache. Cohort+16 marker
    // identities and selector binding: pilot25/motion-binding/motion-binding.json405a47c4.
    internal static (ushort First,ushort Second) NormalMotion(int id) => id switch
    {
        212761 => (1266,960),212762 or 212763 => (1266,900),212764 or 212833 => (833,866),
        212765 or 212766 or 212767 or 212774 or 212775 => (666,666),212768 or 212835 => (733,733),
        212769 => (566,933),212770 or 212771 or 212772 => (566,1030),212773 => (700,733),
        212776 or 212781 => (933,933),212777 or 212778 => (633,633),212779 or 212782 => (833,800),
        212780 or 212876 => (600,433),_=>throw new InvalidDataException("Unbound SkyTemple normal motion")
    };
    internal static (ushort First,ushort Second) NormalMotion(SkyTempleNpcProfile profile) =>
        // A one-variant source contributes one word to max/recovery. Reusing that
        // value here does not add a second authored variant or a selector draw.
        profile.Cohort is { } cohort ? (cohort.NormalMotion.FirstHit,cohort.NormalMotion.SecondHit??cohort.NormalMotion.FirstHit) : NormalMotion(profile.NpcId);
    internal static bool TryGetNormalTiming(Npc npc,Creature target,out NormalTiming timing)
    {
        timing=default;
        if(!SkyTempleNpcProfileCatalog.TryGet(npc,out var p))return false;
        var motion=NormalMotion(p);
        SkyTempleNpcSkillState.BeforeNormalMotion(npc,p);
        int variant=DrawNpc(0,(p.Cohort?.NormalMotion.VariantCount??2)-1);
        var ammo=p.Source.Source.GetRawRootField("ammo_speed");
        float speed=0;
        if(ammo.RawValues.Length!=0 && (ammo.RawValues.Length!=1 || !float.TryParse(ammo.RawValues[0],
            NumberStyles.Float,CultureInfo.InvariantCulture,out speed)||!float.IsFinite(speed)))
            throw new InvalidDataException("Unbound SkyTemple projectile speed");
        var skill=new RetailNpcSkillTimeline.Skill(0,0,0,0,variant==0?motion.First:motion.Second,speed);
        ushort word=RetailNpcSkillTimeline.ImpactDelay(skill,0,new(npc.GetX(),npc.GetY(),npc.GetZ()),
            new(target.GetX(),target.GetY(),target.GetZ()));
        int effective=npc.GetGameStats().GetAttackSpeed().GetCurrent();
        if(effective is <500 or >10000)throw new InvalidOperationException("Unsupported effective NPC attack delay");
        int motionDeadline=checked((int)(Math.Max(motion.First,motion.Second)*(effective/(float)p.AttackDelay)));
        timing=new(word>50?word-50:word,Math.Max(effective,motionDeadline),variant);
        return true;
    }
    internal static readonly int[] SelectedSkills = [16522,16556,16561,16576,16588,16589,16590,16592,16593,16594,
        16595,16677,16699,16710,16723,16724,16725,16794,16836,17039];

    internal static void ValidateProfiles(IReadOnlyCollection<SkyTempleNpcProfile> profiles)
        =>SkyTempleNpcCombatReadiness.Validate(profiles);

    internal static void LogHp(Creature source,Creature target,int skill,int requested,int before,int after)
    {
        if (!IsCohortCombat(source,target)) return;
        Log.LogInformation("RETAIL_SKYTEMPLE_HP source={Source} target={Target} skill={Skill} requested={Requested} before={Before} after={After} actualLoss={Loss}",
            source.GetObjectId(),target.GetObjectId(),skill,requested,before,after,before-after);
    }

    internal static void LogOrphanPeriodicHp(Aion.GameServer.SkillEngine.Model.Effect effect,int requested,int before,int after)
    {
        Log.LogInformation("RETAIL_SKYTEMPLE_HP source=0 originalCaster={OriginalCaster} target={Target} skill={Skill} requested={Requested} before={Before} after={After} actualLoss={Loss}",
            effect.GetEffector().GetObjectId(),effect.GetEffected().GetObjectId(),effect.GetSkillId(),requested,before,after,before-after);
    }



    static partial void TryAdditionalNormal(Creature source, Creature target, ISet<CalculationType>? types,
        ref List<AttackResult> results, ref SkillElement element, ref bool handled)
    {
        if (SkyTemplePlayerCombat.Match(source, target, out _))
            handled = SkyTemplePlayerCombat.TryNormal(source, target, types ?? new HashSet<CalculationType>(), out results, out element);
    }

    static partial void PrepareAdditionalNormalResponse(SkyTempleNpcProfile profile, Player target,
        ref Func<float, string, float>? response, ref string? reason)
    {
        if (!profile.StatisticsOnly || !RequiresEquipmentResponse(profile)) return;
        if (RetailNpcStatEquipmentResponses.TryPrepare(profile, target, out var prepared, out string failure))
        { response = prepared.Normal; reason = null; }
        else reason = failure;
    }
}
