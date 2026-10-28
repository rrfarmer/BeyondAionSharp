using System.Security.Cryptography;
using Aion.GameServer.Commons.Utils.RetailRandom;
using Aion.GameServer.Model;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.Utils.Stats;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Utils;
using System.Runtime.CompilerServices;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Ordinary source-profile normal attacks through the existing 4.8
/// result/protection path. Pilot timing and optional special-skill interop live
/// in separate partials. No actor-memory reconstruction or navigation dependency.</summary>
internal static partial class SkyTempleNpcCombat
{
    private static readonly ILogger Log = AionLog.For(nameof(SkyTempleNpcCombat));
    private static readonly LiveRandom Random = new();
    private static readonly LiveRandom NpcRandom = new();
    private static readonly ConditionalWeakTable<Npc,HashSet<string>> ResponseWarnings=new();
    internal static int Draw(int first,int second) => Random.Range(first,second);
    internal static int DrawNpc(int first,int second) => NpcRandom.Range(first,second);

    internal static bool Applies(Creature source, Creature target) =>
        source is Npc npc && target is Player && source.GetWorldId() == target.GetWorldId() && source.GetInstanceId()==target.GetInstanceId() &&
        RetailNpcStatCatalog.TryGetCombatProfile(npc, out _);

    internal static bool IsCohortCombat(Creature source, Creature target) => Applies(source,target) || Applies(target,source);

    internal static bool TryCalculateNormal(Creature source, Creature target, out List<AttackResult> results,
        out SkillElement element,ISet<CalculationType>? calculationTypes=null)
    {
        results = null!; element = SkillElement.NONE;
        bool handled = false;
        TryAdditionalNormal(source, target, calculationTypes, ref results, ref element, ref handled);
        if (handled) return true;
        if (!Applies(source,target)) return false;
        var npc = (Npc)source; var player = (Player)target;
        if (!RetailNpcStatCatalog.TryGetCombatProfile(npc,out var profile))
            throw new InvalidOperationException("Published SkyTemple profile disappeared");
        if (profile.StatisticsOnly && profile.Attacks.IsEmpty)
        { results = [new AttackResult(0,AttackStatus.NORMALHIT)]; return true; }
        if (profile.StatisticsOnly && !profile.HasNormalAttackCorrection)
        {
            var warnings = ResponseWarnings.GetValue(npc, _ => []);
            lock (warnings) if (warnings.Add("unsupported-normal-shape"))
                Log.LogWarning("RETAIL_STAT_NORMAL_COMPAT npc={Npc} reason=unsupported-normal-shape hitCount={HitCount}; existing4.8 normal calculation retained",
                    profile.NpcId, profile.HitCount);
            return false;
        }
        Func<float, string, float>? equipmentResponse = null;
        string? responseReason = profile.StatisticsOnly && RequiresEquipmentResponse(profile)
            ? "source-offensive-exclusive-attribute-unverified" : null;
        PrepareAdditionalNormalResponse(profile, player, ref equipmentResponse, ref responseReason);
        if (responseReason is not null)
        {
            var warnings = ResponseWarnings.GetValue(npc, _ => []);
            lock (warnings) if (warnings.Add(responseReason))
                Log.LogWarning("RETAIL_STAT_NORMAL_COMPAT npc={Npc} reason={Reason}; existing4.8 normal calculation retained", profile.NpcId, responseReason);
            return false;
        }
        var statNormalModifiers = profile.StatisticsOnly ? profile.Attacks
            .Select(a=>SkyTempleCombatInputs.Element(a.Type)==SkillElement.NONE).Distinct()
            .ToDictionary(physical=>physical,physical=>RetailNpcStatNormalInputs.CurrentModifiers(npc,physical)) : null;
        var selected = profile.Attacks[0];
        if (profile.StatisticsOnly && profile.Attacks.Length > 1)
        {
            var pool = RetailNpcMinimumReachPool.Prepare(SkyTempleCombatInputs.Scope(profile.NpcId),
                RetailNpcMinimumReachPool.NpcServerBinarySha256, "source-ordered statistics-only normal request",
                new(player.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide(), profile.Bounds.HorizontalRadius,
                    DistanceSquared(npc.GetX(),npc.GetY(),npc.GetZ(),player.GetX(),player.GetY(),player.GetZ()), false),
                profile.Attacks.Select(a => new RetailNpcMinimumReachPool.EligibleAttack(a.Ordinal,a.Range,a.Weight,profile.SourceRecordSha256)).ToArray());
            if (pool.TotalWeight == 0)
            {
                Log.LogWarning("RETAIL_STAT_NORMAL_OUT_OF_RANGE npc={Npc}; no source normal is eligible for this legacy attack request", profile.NpcId);
                results = [new AttackResult(0,AttackStatus.NORMALHIT)]; return true;
            }
            int draw = DrawNpc(1,pool.TotalWeight);
            selected = profile.Attacks[pool.Entries.First(e => draw <= e.CumulativeThreshold).CombinedIndex];
        }
        element = SkyTempleCombatInputs.Element(selected.Type);
        bool physical = element == SkillElement.NONE;
        var scope = SkyTempleCombatInputs.Scope(profile.NpcId);
        bool dual = profile.StatisticsOnly && profile.Source.Integers["dual"].ParsedValue is not (null or 0);
        var dualRanges = dual ? RetailNpcDualNormalPlan.Split(profile.MinDamage,profile.MaxDamage,profile.HitCount) : null;
        var range = RetailNpcNormalRange.Create(scope,dualRanges?.Main.Minimum ?? checked((ushort)profile.MinDamage),
            dualRanges?.Main.Maximum ?? checked((ushort)profile.MaxDamage));
        int sample = Random.Range(range.Minimum,range.Maximum);
        // Apply the actual current effect functions to the selected min/max base. In particular,
        // Bolcan's attack buff is not multiplied against raw physical_attack or another rank table.
        float damage = statNormalModifiers is not null
            ? RetailNpcStatNormalInputs.CalculateBase(sample,selected.Modifier,statNormalModifiers[physical])
            : npc.GetGameStats().GetStat(physical ? StatEnum.PHYSICAL_ATTACK : StatEnum.MAGICAL_ATTACK,
                sample * (selected.Modifier / 100f)).GetExactCurrent();
        damage = Math.Max(1f,damage);
        float baseDamage = damage;
        var defense = player.GetGameStats();
        uint consumedDefense = SkyTempleCombatInputs.Unsigned(physical
            ? SkyTempleCombatInputs.Moved(player,StatEnum.PHYSICAL_DEFENSE,defense.GetPDef().GetCurrent())
            : SkyTempleCombatInputs.Moved(player,StatEnum.MAGICAL_DEFEND,defense.GetMDef().GetCurrent()),"defense");
        short consumedElement = physical ? (short)0 : SkyTempleCombatInputs.SignedWord(SkyTempleCombatInputs.ElementalDefense(player,element),"element");
        int elementFloor = physical ? 0 : SkyTempleCombatInputs.ElementalFloor(player.GetLevel());
        if (physical)
            damage = RetailNpcNormalDamageStages.CalculatePhysicalDefense(scope,damage,profile.Level,player.GetLevel(),false,
                consumedDefense,
                0,SkyTempleCombatInputs.PlayerStatRatio).Damage;
        else
            damage = RetailNpcNormalDamageStages.CalculateElementalDefense(scope,damage,profile.Level,player.GetLevel(),false,
                consumedDefense,0,consumedElement,0,(uint)profile.StatRatio,elementFloor).Damage;

        float defenseDamage = damage;
        int blockRemainingPercent = 100, blockMaximum = 0, criticalReduction = 0;
        (int Attack,int Defense) consumedPve = default;
        float statusDamage = 0;
        AttackStatus status = SelectNormalDefense(npc,player,profile,scope);
        if (status == AttackStatus.DODGE) damage = 0;
        else
        {
            if (status == AttackStatus.BLOCK)
            {
                var shield = player.GetEquipment().GetEquippedShield();
                if (shield is null) throw new InvalidOperationException("Block selected after shield disappeared");
                int remainingPercent = defense.GetReverseStat(StatEnum.DAMAGE_REDUCE,100).GetCurrent();
                blockRemainingPercent = remainingPercent;
                blockMaximum = shield.GetItemTemplate().GetWeaponStats().GetReduceMax();
                damage = BlockDamage(damage,remainingPercent,blockMaximum);
            }
            else if (status == AttackStatus.PARRY) damage *= 0.6f;
            bool critical = SelectNormalCritical(npc,player,profile,scope,physical);
            if (critical)
            {
                int reduction = SkyTempleCombatInputs.CriticalReduction(player,physical);
                criticalReduction = reduction;
                // Player override width is32. Preserve it here rather than narrowing it to the
                // scalar helper's NPC unsigned-word input domain.
                float coefficient = Math.Max(1f,(physical ? 1.5f : 1f) - (int)(reduction / (profile.StatRatio / 1000f)) / 1000f);
                damage *= coefficient;
                status = AttackStatusExtensions.GetCriticalStatusFor(status);
            }
            consumedPve = SkyTempleCombatInputs.Pve(npc,player,physical);
            statusDamage = damage;
            damage *= RetailNpcNormalDamageStages.CalculatePveRatio(scope,consumedPve.Attack,consumedPve.Defense);
            damage = Math.Max(1f,damage);
        }
        if (status == AttackStatus.DODGE) statusDamage = damage;
        float pveDamage=damage;
        if(status!=AttackStatus.DODGE&&equipmentResponse is not null)damage=equipmentResponse(damage,"main");
        if (!float.IsFinite(damage) || damage < 0 || damage >= 2147483648f)
            throw new InvalidOperationException("SkyTemple normal damage is outside int32");
        results = [new AttackResult((int)damage,status,physical ? HitType.PHHIT : HitType.MAHIT)];
        int afterAi = profile.StatisticsOnly && npc.GetAi() is OneDmgAI ? ApplyOneDamageOwner(npc, player, results) : results[0].GetDamage();
        // Current host protection/effect owner executes exactly once; never run it for diagnostics.
        player.GetObserveController().CheckShieldStatus(results,null!,npc);
        Log.LogDebug("RETAIL_SKYTEMPLE_CALC npc={Npc} source={Source} target={Target} element={Element} sample={Sample} range={Min}..{Max} status={Status} damage={Damage} generation={Generation}",
            profile.NpcId,npc.GetObjectId(),player.GetObjectId(),element,sample,range.Minimum,range.Maximum,status,results[0].GetDamage(),profile.Generation);
        if (statNormalModifiers is not null)
            LogNormalInputs(profile,npc,player,selected.Ordinal,"main",sample,selected.Modifier,statNormalModifiers[physical],physical,
                consumedDefense,consumedElement,elementFloor,status,criticalReduction,blockRemainingPercent,blockMaximum,consumedPve,
                baseDamage,defenseDamage,statusDamage,pveDamage,damage,afterAi,results[0].GetDamage(),range.Minimum,range.Maximum);
        if (dualRanges is not null)
        {
            var shared = RetailNpcDualNormalPlan.Share(status);
            // Main protection has already run. Only a successful main record can
            // request its independent offhand sample; outcome decisions are shared.
            if (RetailNpcDualNormalPlan.ShouldCalculateOffhand(shared))
            {
                int offSample = Random.Range(dualRanges.Offhand.Minimum,dualRanges.Offhand.Maximum);
                float offDamage = Math.Max(1f,RetailNpcStatNormalInputs.CalculateBase(offSample,selected.Modifier,statNormalModifiers![physical]));
                float offBase = offDamage;
                uint offDefense = SkyTempleCombatInputs.Unsigned(physical
                    ? SkyTempleCombatInputs.Moved(player,StatEnum.PHYSICAL_DEFENSE,defense.GetPDef().GetCurrent())
                    : SkyTempleCombatInputs.Moved(player,StatEnum.MAGICAL_DEFEND,defense.GetMDef().GetCurrent()),"defense");
                short offElement = physical ? (short)0 : SkyTempleCombatInputs.SignedWord(SkyTempleCombatInputs.ElementalDefense(player,element),"element");
                if (physical)
                    offDamage = RetailNpcNormalDamageStages.CalculatePhysicalDefense(scope,offDamage,profile.Level,player.GetLevel(),false,
                        offDefense,
                        0,SkyTempleCombatInputs.PlayerStatRatio).Damage;
                else
                    offDamage = RetailNpcNormalDamageStages.CalculateElementalDefense(scope,offDamage,profile.Level,player.GetLevel(),false,
                        offDefense,0,offElement,0,
                        (uint)profile.StatRatio,SkyTempleCombatInputs.ElementalFloor(player.GetLevel())).Damage;
                float offAfterDefense = offDamage;
                int shieldPercent = 100, shieldMaximum = 0;
                if (shared.SharedActiveDefense == 3)
                {
                    var shield = player.GetEquipment().GetEquippedShield()
                        ?? throw new InvalidOperationException("Shared block selected after shield disappeared");
                    shieldPercent = defense.GetReverseStat(StatEnum.DAMAGE_REDUCE,100).GetCurrent();
                    shieldMaximum = shield.GetItemTemplate().GetWeaponStats().GetReduceMax();
                }
                int offCriticalReduction = SkyTempleCombatInputs.CriticalReduction(player,physical);
                offDamage = RetailNpcDualNormalPlan.ApplySharedOffhand(offDamage,shared,physical,(uint)profile.StatRatio,
                    offCriticalReduction,shieldPercent,shieldMaximum);
                float offAfterStatus = offDamage;
                var pve = SkyTempleCombatInputs.Pve(npc,player,physical);
                offDamage = Math.Max(1f,offDamage * RetailNpcNormalDamageStages.CalculatePveRatio(scope,pve.Attack,pve.Defense));
                float offPve=offDamage;
                if(equipmentResponse is not null)offDamage=equipmentResponse(offDamage,"off");
                if (!float.IsFinite(offDamage) || offDamage < 0 || offDamage >= 2147483648f)
                    throw new InvalidOperationException("Retail offhand damage is outside int32");
                var off = new List<AttackResult> { new((int)offDamage,shared.OffhandStatus!.Value,physical ? HitType.PHHIT : HitType.MAHIT) };
                int offAfterAi = profile.StatisticsOnly && npc.GetAi() is OneDmgAI ? ApplyOneDamageOwner(npc, player, off) : off[0].GetDamage();
                player.GetObserveController().CheckShieldStatus(off,null!,npc);
                results.AddRange(off);
                Log.LogDebug("RETAIL_STAT_DUAL_OFFHAND npc={Npc} source={Source} target={Target} element={Element} sample={Sample} range={Min}..{Max} status={Status} damage={Damage} generation={Generation}",
                    profile.NpcId,npc.GetObjectId(),player.GetObjectId(),element,offSample,dualRanges.Offhand.Minimum,dualRanges.Offhand.Maximum,
                    shared.OffhandStatus.Value,off[0].GetDamage(),profile.Generation);
                LogNormalInputs(profile,npc,player,selected.Ordinal,"off",offSample,selected.Modifier,statNormalModifiers![physical],physical,
                    offDefense,offElement,elementFloor,shared.OffhandStatus.Value,offCriticalReduction,shieldPercent,shieldMaximum,pve,
                    offBase,offAfterDefense,offAfterStatus,offPve,offDamage,offAfterAi,off[0].GetDamage(),dualRanges.Offhand.Minimum,dualRanges.Offhand.Maximum);
            }
        }
        return true;
    }

    // Preserve the existing one-damage mechanic after the retail numeric stages,
    // before protection. Other Java numeric/distance overrides must not replace
    // or multiply the verified source ranges and ordered attack modifiers.
    // This mechanic remains a visible script-compatibility boundary.
    private static int ApplyOneDamageOwner(Npc npc, Player target, List<AttackResult> attacks)
    {
        int before = attacks[0].GetDamage();
        AttackUtil.ModifyDamageByNpcAi(npc, target, attacks);
        int after = attacks[0].GetDamage();
        if (before != after)
        {
            var warnings = ResponseWarnings.GetValue(npc, _ => []);
            lock (warnings) if (warnings.Add("host-ai-normal-damage"))
                Log.LogWarning("RETAIL_STAT_NORMAL_COMPAT npc={Npc} reason=one-damage-mechanic owner={Owner}; existing4.8 one-damage owner retained; native script semantics unverified",
                    npc.GetNpcId(), npc.GetAi().GetType().FullName);
        }
        return after;
    }

    // Consumed operands only: never re-evaluate effects, protections or RNG for
    // diagnostics. This lets a separately implemented native arithmetic oracle
    // inspect the consumed operands, including independent dual-hand samples.
    private static void LogNormalInputs(SkyTempleNpcProfile profile,Npc source,Player target,int ordinal,string hand,
        int sample,int modifier,RetailNpcStatNormalInputs.Modifiers modifiers,bool physical,uint targetDefense,short elementDefense,
        int elementFloor,AttackStatus status,int criticalReduction,int blockRemainingPercent,int blockMax,
        (int Attack,int Defense) pve,float basis,float afterDefense,float afterStatus,float afterPve,float afterEquipment,int aiDamage,int protectedDamage,int rangeMin,int rangeMax)
    {
        if (!Log.IsEnabled(LogLevel.Debug)) return;
        var payload=new {npc=profile.NpcId,source=source.GetObjectId(),target=target.GetObjectId(),ordinal,hand,sample,modifier,
            percent=modifiers.Percent,flat=modifiers.Flat,physical,sourceLevel=profile.Level,targetLevel=(int)target.GetLevel(),
            profileRatio=profile.StatRatio,defenseRatio=SkyTempleCombatInputs.PlayerStatRatio,targetDefense,elementDefense,elementFloor,
            status=status.ToString(),criticalReduction,blockRemainingPercent,blockMax,pveAttack=pve.Attack,pveDefense=pve.Defense,
            baseBits=BitConverter.SingleToUInt32Bits(basis),defenseBits=BitConverter.SingleToUInt32Bits(afterDefense),
            statusBits=BitConverter.SingleToUInt32Bits(afterStatus),pveBits=BitConverter.SingleToUInt32Bits(afterPve),
            equipmentBits=BitConverter.SingleToUInt32Bits(afterEquipment),requestedDamage=(int)afterEquipment,aiDamage,protectedDamage,rangeMin,rangeMax,sourceHash=profile.SourceRecordSha256,generation=profile.Generation};
        Log.LogDebug("RETAIL_STAT_NORMAL_INPUT {Payload}",System.Text.Json.JsonSerializer.Serialize(payload));
    }

    private static AttackStatus SelectNormalDefense(Npc source,Player target,SkyTempleNpcProfile p,RetailNpcNormalScope scope)
    {
        if(SkyTempleCombatInputs.Pose(target)==5||source.GetEffectController().IsInAnyAbnormalState(
            Aion.GameServer.SkillEngine.Effects.AbnormalState.CANT_ATTACK_STATE))return AttackStatus.NORMALHIT;
        var context = RetailNpcNormalStatusMath.Context.Create(scope,RetailNpcNormalScope.MainBinarySha256,
            "managed4.8 current owner stat/effect mapping",false);
        int accuracy = source.GetGameStats().GetMainHandPAccuracy().GetCurrent();
        // Ordinary retail NPC weapon group0 uses this active-defense family even when
        // its selected normal record carries an elemental damage type.
        if (source.GetObserveController().CheckAttackerStatus(AttackStatus.DODGE) ||
            target.GetObserveController().CheckAttackStatus(AttackStatus.DODGE)) return AttackStatus.DODGE;
        int threshold = RetailNpcNormalStatusMath.CalculatePhysicalThreshold(context,RetailNpcNormalStatusMath.ChanceStage.Dodge,
            new(SkyTempleCombatInputs.Moved(target,StatEnum.EVASION,target.GetGameStats().GetEvasion().GetCurrent()),accuracy,0,(uint)p.StatRatio));
        if (Random.Range(1,1000) <= threshold) return AttackStatus.DODGE;
        if (target.GetEquipment().IsShieldEquipped())
        {
            if (target.GetObserveController().CheckAttackStatus(AttackStatus.BLOCK)) return AttackStatus.BLOCK;
            threshold = RetailNpcNormalStatusMath.CalculatePhysicalThreshold(context,RetailNpcNormalStatusMath.ChanceStage.Block,
                new(SkyTempleCombatInputs.Moved(target,StatEnum.BLOCK,target.GetGameStats().GetBlock().GetCurrent()),accuracy,0,(uint)p.StatRatio));
            if (Random.Range(1,1000) <= threshold) return AttackStatus.BLOCK;
        }
        if (target.GetObserveController().CheckAttackStatus(AttackStatus.PARRY)) return AttackStatus.PARRY;
        threshold = RetailNpcNormalStatusMath.CalculatePhysicalThreshold(context,RetailNpcNormalStatusMath.ChanceStage.Parry,
            new(SkyTempleCombatInputs.Moved(target,StatEnum.PARRY,target.GetGameStats().GetParry().GetCurrent()),accuracy,0,(uint)p.StatRatio));
        return Random.Range(1,1000) <= threshold ? AttackStatus.PARRY : AttackStatus.NORMALHIT;
    }

    private static bool SelectNormalCritical(Npc source,Player target,SkyTempleNpcProfile p,RetailNpcNormalScope scope,bool physical)
    {
        int critical = physical ? source.GetGameStats().GetMainHandPCritical().GetCurrent() : source.GetGameStats().GetMCritical().GetCurrent();
        var forced = source.GetObserveController().CheckAttackerCriticalStatus(AttackStatus.CRITICAL,false);
        if (forced.IsResult())
        {
            if (!forced.IsPercent()) return Random.Range(1,1000) <= forced.GetValue();
            critical = checked((int)(critical * (1f + forced.GetValue() / 100f)));
        }
        var context = RetailNpcNormalStatusMath.Context.Create(scope,RetailNpcNormalScope.MainBinarySha256,
            "managed4.8 current critical mapping",false);
        int threshold = RetailNpcNormalStatusMath.CalculateMainCriticalThreshold(context,new(critical,
            physical ? target.GetGameStats().GetPCR().GetCurrent() : target.GetGameStats().GetMCR().GetCurrent(),0,1000,SkyTempleCombatInputs.Pose(target)));
        return Random.Range(1,1000) <= threshold;
    }

    internal static float BlockDamage(float incoming,int remainingPercent,int maximumAbsorbed)
    {
        if(!float.IsFinite(incoming)||incoming<0||maximumAbsorbed<0)throw new ArgumentOutOfRangeException(nameof(incoming));
        float absorption=1f-Math.Clamp(remainingPercent/100f,0f,1f);
        return incoming-Math.Min(incoming*absorption,maximumAbsorbed);
    }


    static partial void TryAdditionalNormal(Creature source, Creature target, ISet<CalculationType>? types,
        ref List<AttackResult> results, ref SkillElement element, ref bool handled);
    static partial void PrepareAdditionalNormalResponse(SkyTempleNpcProfile profile, Player target,
        ref Func<float, string, float>? response, ref string? reason);

    internal static bool RequiresEquipmentResponse(SkyTempleNpcProfile profile)
    {
        var field = profile.Source.Source.GetRawRootField("exclusive_attribute");
        return field.RawValues.Length != 0 && (field.RawValues.Length != 1
            || !int.TryParse(field.RawValues[0], System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out int value) || value != 0);
    }

    private static float DistanceSquared(float ax, float ay, float az, float bx, float by, float bz)
    {
        float y = RetailNpcNormalDamageStages.Subtract(ay, by), x = RetailNpcNormalDamageStages.Subtract(ax, bx);
        float z = RetailNpcNormalDamageStages.Subtract(az, bz);
        return RetailNpcNormalDamageStages.Add(RetailNpcNormalDamageStages.Add(
            RetailNpcNormalDamageStages.Multiply(y, y), RetailNpcNormalDamageStages.Multiply(x, x)),
            RetailNpcNormalDamageStages.Multiply(z, z));
    }
    /// <summary>One shared managed retail engine; host cryptographic seed is an explicit
    /// infrastructure choice, not a claim to reproduce an unobserved retail global seed.</summary>
    internal sealed class LiveRandom
    {
        private readonly object gate = new();
        private readonly RetailRandomEngine.Mutable engine;
        internal LiveRandom() : this(BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4))) { }
        internal LiveRandom(uint seed)
        {
            engine = new(RetailRandomState.FromOwnedEngineBytes(new byte[RetailRandomState.ByteLength]));
            engine.ApplyNormalSeedReturn(seed);
        }
        internal int Range(int a,int b)
        {
            lock (gate)
            {
                if (a == b) return a;
                int lo = Math.Min(a,b),hi = Math.Max(a,b);
                ulong width = (ulong)((long)hi-lo)+1;
                if (width == 0x100000000UL) return unchecked(lo+(int)(uint)engine.Draw().Value);
                ulong quotient = ulong.MaxValue / width, remainder = ulong.MaxValue % width;
                ulong word;
                do { word = engine.Draw().Value; } while (remainder != width-1 && word / width >= quotient);
                return unchecked(lo+(int)(word % width));
            }
        }
    }
}
