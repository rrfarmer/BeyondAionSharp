using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Retail;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Selected NPC defense consumers for normal player attacks. Player
/// item/stat damage, weapon critical coefficients, additional hits, script ownership
/// and outer protections retain their existing 4.8 owners. No skills or PvP.</summary>
internal static class RetailNpcPlayerNormal
{
    internal static bool TryMatch(Creature source,Creature target,out SkyTempleNpcProfile profile)
    {
        profile=null!;
        return source is Player && target is Npc npc && source.GetWorldId()==target.GetWorldId()
            && source.GetInstanceId()==target.GetInstanceId()
            && RetailNpcStatCatalog.TryGetCombatProfile(npc,out profile) && profile.StatisticsOnly;
    }

    internal static RetailNpcNormalScope Scope(SkyTempleNpcProfile profile) => RetailNpcNormalScope.Create(
        RetailNpcNormalScope.MainBinarySha256,RetailNpcNormalActorKind.Player,
        RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,0,profile.NpcId);

    internal static bool TryCalculate(Player source,Npc target,SkyTempleNpcProfile profile,ISet<CalculationType>? types,
        out List<AttackResult> results,out SkillElement element)
    {
        results=null!;
        bool physical=source.GetAttackType()==Aion.GameServer.Model.Templates.Items.ItemAttackType.PHYSICAL;
        element=physical?SkillElement.NONE:source.GetAttackType().GetMagicalElement();
        if(!SkyTempleNpcCombat.HasSupportedIncomingNormalOwner(target,out var script))
        {
            SkyTempleNpcCombat.WarnNormalCompatibility(target,"player-normal-incoming-script-unverified:"+script?.FullName);
            return false;
        }
        int critical=physical?source.GetGameStats().GetMainHandPCritical().GetCurrent():source.GetGameStats().GetMCritical().GetCurrent();
        if(critical is <0 or >ushort.MaxValue)
        {
            SkyTempleNpcCombat.WarnNormalCompatibility(target,"player-normal-critical-input-outside-ordinary-word");
            return false;
        }
        var scope=Scope(profile);
        var context=RetailNpcNormalStatusMath.Context.Create(scope,RetailNpcNormalScope.MainBinarySha256,
            "existing4.8 player source stats; selected NPC defense words; ordinary source flags",false);
        AttackStatus status;
        if(physical)
            status=SkyTempleNpcCombat.SelectNpcNormalDefense(source,target,scope,source.GetLevel(),1000);
        else if(target.GetObserveController().CheckAttackStatus(AttackStatus.RESIST)
            || SkyTempleNpcCombat.Draw(1,1000)<=RetailNpcNormalStatusMath.CalculateMagicalResistThreshold(context,
                new(target.GetGameStats().GetMResist().GetCurrent(),source.GetGameStats().GetMAccuracy().GetCurrent(),
                    1000,source.GetLevel(),profile.Level,SkyTempleCombatInputs.Pose(target))))
            status=AttackStatus.RESIST;
        else status=AttackStatus.NORMALHIT;

        bool terminal=status is AttackStatus.DODGE or AttackStatus.RESIST;
        if(!terminal && Critical(source,target,profile,context,physical,critical))
            status=AttackStatusExtensions.GetCriticalStatusFor(status);
        results=StatFunctions.CalculateAttackDamage(source,element,status,types??new HashSet<CalculationType>());
        if(!terminal)
        {
            ApplyDefenseStages(source,target,profile,status,element,results);
            AttackUtil.AmplifyDamageByAdditionalHitCount(source,status,results);
            AttackUtil.ModifyDamageByNpcAi(source,target,results);
            SkyTempleNpcCombat.ApplyNpcDamageTrim(profile,physical,results);
        }
        // A terminal miss remains zero even for a one-damage script. Protections
        // retain the existing list owner and execute once, without diagnostic replay.
        target.GetObserveController().CheckShieldStatus(results,null!,source);
        return true;
    }

    private static bool Critical(Player source,Npc target,SkyTempleNpcProfile profile,
        RetailNpcNormalStatusMath.Context context,bool physical,int critical)
    {
        var forced=source.GetObserveController().CheckAttackerCriticalStatus(AttackStatus.CRITICAL,false);
        int draw=SkyTempleNpcCombat.Draw(1,1000);
        if(forced.IsResult())
        {
            if(!forced.IsPercent())return draw<=forced.GetValue();
            critical=checked((int)(critical*(1f+forced.GetValue()/100f)));
        }
        int moved=SkyTempleCombatInputs.Moved(source,physical?StatEnum.PHYSICAL_CRITICAL:StatEnum.MAGICAL_CRITICAL,critical)-critical;
        int threshold=RetailNpcNormalStatusMath.CalculateMainCriticalThreshold(context,new(critical,
            physical?target.GetGameStats().GetPCR().GetCurrent():target.GetGameStats().GetMCR().GetCurrent(),
            moved,(uint)profile.StatRatio,SkyTempleCombatInputs.Pose(target)));
        return draw<=threshold;
    }

    internal static void ApplyDefenseStages(Player source,Npc target,SkyTempleNpcProfile profile,AttackStatus status,
        SkillElement element,List<AttackResult> results)
    {
        var scope=Scope(profile);
        bool physical=element==SkillElement.NONE;
        for(int hand=0;hand<results.Count;hand++)
        {
            // Preserve the actual player's fractional source damage through these
            // stages; the existing script/protection/final delivery owns narrowing.
            float amount=StatFunctions.AdjustStatByMovementModifier(source,
                physical?StatEnum.PHYSICAL_ATTACK:StatEnum.MAGICAL_ATTACK,results[hand].GetExactDamage());
            int defense=SkyTempleCombatInputs.Moved(target,physical?StatEnum.PHYSICAL_DEFENSE:StatEnum.MAGICAL_DEFEND,
                physical?target.GetGameStats().GetPDef().GetCurrent():target.GetGameStats().GetMDef().GetCurrent());
            amount=physical
                ?RetailNpcNormalDamageStages.CalculatePhysicalDefense(scope,amount,source.GetLevel(),profile.Level,false,
                    SkyTempleCombatInputs.Unsigned(defense,"NPC armor"),0,(uint)profile.StatRatio).Damage
                :RetailNpcNormalDamageStages.CalculateElementalDefense(scope,amount,source.GetLevel(),profile.Level,false,
                    SkyTempleCombatInputs.Unsigned(defense,"NPC magic defense"),0,
                    SkyTempleCombatInputs.SignedWord(SkyTempleCombatInputs.ElementalDefense(target,element),"NPC element"),
                    0,1000,-1300).Damage;
            if(AttackStatusExtensions.GetBaseStatus(status) is AttackStatus.BLOCK or AttackStatus.PARRY)amount*=.9f;
            if(status.IsCritical())
            {
                var group=AttackUtil.GetWeaponGroup(source,hand==0);
                float coefficient=physical&&group.HasValue?AttackUtil.GetWeaponMultiplier(group.Value):1.5f;
                int reduction=SkyTempleCombatInputs.Word(SkyTempleCombatInputs.CriticalReduction(target,physical),"NPC critical damage reduction");
                // Native User main subtracts reduction. Physical User offhand adds
                // it (sign flip0x14036b16d, subtraction0x14036b185); do not reuse the NPC attacker's coefficient.
                amount*=Math.Max(1f,coefficient+(physical&&hand!=0?reduction:-reduction)/1000f);
            }
            var pve=SkyTempleCombatInputs.Pve(source,target,physical);
            amount*=RetailNpcNormalDamageStages.CalculatePveRatio(scope,pve.Attack,pve.Defense);
            results[hand].SetDamage(Math.Max(1f,amount));
        }
    }
}
