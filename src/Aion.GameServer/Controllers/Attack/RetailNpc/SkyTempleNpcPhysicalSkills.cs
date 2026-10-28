using System.Buffers.Binary;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using F=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Selected live physical min/max consumer; native bb8d90/bbd1d0/bb80c0/36d0f0.
/// Current managed function owners and the existing single HP/event owner are retained.</summary>
internal static class SkyTempleNpcPhysicalSkills
{
    internal readonly record struct Modifiers(float Rate,int Flat);
    internal static Modifiers CurrentModifiers(Npc owner)
    {
        var current=new AdditionStat(StatEnum.PHYSICAL_ATTACK,0,owner);float rate=0;
        foreach(var original in owner.GetGameStats().GetStatsSorted(StatEnum.PHYSICAL_ATTACK))
        {
            IStatFunction inner=original;while(inner is StatFunctionProxy proxy)inner=proxy.GetProxiedFunction();
            Type type=inner.GetType();
            if(type!=typeof(StatRateFunction)&&type!=typeof(StatAddFunction)&&type!=typeof(StatSubFunction)&&type!=typeof(StatFunction))
                throw new InvalidOperationException("Unsupported current NPC physical operator: "+type.Name);
            if(!original.Validate(current))continue;
            if(type==typeof(StatRateFunction))rate=F.Add(rate,F.Div(inner.GetValue(),100f));
            original.Apply(current,new HashSet<CalculationType>());
        }
        return new(rate,F.Cvtt(current.GetExactCurrent()));
    }
    internal static float Base(int sample,int hitPercent,Modifiers mods,int randomWord)
    {
        if(sample is <0 or >65535||randomWord is <0 or >99)throw new ArgumentOutOfRangeException(nameof(sample));
        float coefficient=F.Add(F.Add(mods.Rate,1f),F.Div(hitPercent,100f));
        float amount=F.Add(F.Mul(sample,coefficient),mods.Flat);
        return F.Mul(amount,F.Div(randomWord<35?90:randomWord<65?100:110,100f));
    }
    internal static bool TryCalculate(Effect effect,EffectTemplate template,SpellStatus? status,out bool success)
    {
        success=false;
        if(template is not SkillAttackInstantEffect||effect.GetSkillId() is not (16556 or 16794))return false;
        if(effect.GetEffector() is not Npc npc||!SkyTempleNpcProfileCatalog.TryGet(npc,out var p))return false;
        if(effect.GetEffected() is not Player target||template.GetPosition()!=1||template.GetElement()!=SkillElement.NONE)
            throw new InvalidOperationException("Selected physical skill requires Player/slot1/physical binding");
        var mods=CurrentModifiers(npc); // unsupported current operators reject before RNG
        var scope=SkyTempleCombatInputs.Scope(p.NpcId);
        var c=RetailNpcNormalStatusMath.Context.Create(scope,RetailNpcNormalScope.MainBinarySha256,"actual4.8 player physical inputs",false);
        var boost=OneTimeBoostSkillAttackEffect.GetActiveBoost(npc,template);
        int accuracy=npc.GetGameStats().GetMainHandPAccuracy().GetCurrent();
        if(boost is not null)accuracy=unchecked(accuracy+boost.CalculatePhysicalAccuracyBonus(npc.GetGameStats().GetMainHandPAccuracy()));
        accuracy=Math.Clamp(accuracy,0,9999);
        bool active=SkyTempleCombatInputs.Pose(target)!=5&&!npc.GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_ATTACK_STATE);
        if(active)
        {
            int threshold=Chance(c,p,target,accuracy,RetailNpcNormalStatusMath.ChanceStage.Dodge,StatEnum.EVASION,target.GetGameStats().GetEvasion().GetCurrent());
            if(npc.GetObserveController().CheckAttackerStatus(AttackStatus.DODGE)||target.GetObserveController().CheckAttackStatus(AttackStatus.DODGE)||SkyTempleNpcCombat.Draw(1,1000)<=threshold)
            {effect.ResetMagicalCritical();effect.SetAttackStatus(AttackStatus.DODGE);effect.SetEffectResult(EffectResult.DODGE);return true;}
        }
        _=SkyTempleNpcCombat.Draw(1,100); // Begin chance100 still invokes wrapper
        int sample=SkyTempleNpcCombat.Draw(p.MinDamage,p.MaxDamage);
        float amount=Base(sample,effect.GetSkillId()==16556?100:21,mods,SkyTempleNpcCombat.Draw(0,99));
        if(Direction(effect.GetSkillId(),target.GetHeading(),npc.GetX(),npc.GetY(),target.GetX(),target.GetY()))
        {_=SkyTempleNpcCombat.Draw(1,100);amount=F.Add(amount,effect.GetSkillId()==16556?21:60);}
        if(boost is not null){amount=boost.CalculateDamage(amount);boost.ConsumeCharge();}
        amount=SkyTempleCombatInputs.Moved(npc,StatEnum.PHYSICAL_ATTACK,F.Cvtt(amount));
        amount=RetailNpcNormalDamageStages.CalculatePhysicalDefense(scope,Math.Max(1,amount),p.Level,target.GetLevel(),false,
            SkyTempleCombatInputs.Unsigned(SkyTempleCombatInputs.Moved(target,StatEnum.PHYSICAL_DEFENSE,target.GetGameStats().GetPDef().GetCurrent()),"User PDef"),0,1000).Damage;
        AttackStatus attack=AttackStatus.NORMALHIT;
        if(active)
        {
            bool blocked=false;
            if(target.GetEquipment().IsShieldEquipped())
            {
                int threshold=Chance(c,p,target,accuracy,RetailNpcNormalStatusMath.ChanceStage.Block,StatEnum.BLOCK,target.GetGameStats().GetBlock().GetCurrent());
                blocked=target.GetObserveController().CheckAttackStatus(AttackStatus.BLOCK)||SkyTempleNpcCombat.Draw(1,1000)<=threshold;
                if(blocked)
                {
                    attack=AttackStatus.BLOCK;var shield=target.GetEquipment().GetEquippedShield();
                    amount=SkyTempleNpcCombat.BlockDamage(amount,target.GetGameStats().GetReverseStat(StatEnum.DAMAGE_REDUCE,100).GetCurrent(),shield.GetItemTemplate().GetWeaponStats().GetReduceMax());
                }
            }
            if(!blocked)
            {
                int threshold=Chance(c,p,target,accuracy,RetailNpcNormalStatusMath.ChanceStage.Parry,StatEnum.PARRY,target.GetGameStats().GetParry().GetCurrent());
                if(target.GetObserveController().CheckAttackStatus(AttackStatus.PARRY)||SkyTempleNpcCombat.Draw(1,1000)<=threshold)
                {attack=AttackStatus.PARRY;amount=F.Mul(amount,.6f);}
            }
        }
        int draw=SkyTempleNpcCombat.Draw(1,1000),critical=npc.GetGameStats().GetMainHandPCritical().GetCurrent();
        var forced=npc.GetObserveController().CheckAttackerCriticalStatus(AttackStatus.CRITICAL,true);bool crit;
        if(forced.IsResult()&&!forced.IsPercent())crit=draw<=forced.GetValue();
        else
        {
            if(forced.IsResult())critical=F.Cvtt(F.Mul(critical,F.Add(1,F.Div(forced.GetValue(),100f))));
            crit=draw<Math.Min(500,unchecked(critical+(SkyTempleCombatInputs.Pose(target)==5?500:0)-target.GetGameStats().GetPCR().GetCurrent()));
        }
        if(crit)
        {
            amount=F.Mul(amount,Math.Max(1f,F.Sub(1.5f,F.Div(SkyTempleCombatInputs.CriticalReduction(target,true),1000f))));
            attack=attack==AttackStatus.BLOCK?AttackStatus.CRITICAL_BLOCK:attack==AttackStatus.PARRY?AttackStatus.CRITICAL_PARRY:AttackStatus.CRITICAL;
        }
        effect.SetCalculatedMagicalCritical(1,crit);
        var ratios=SkyTempleCombatInputs.Pve(npc,target,true);
        int damage=Math.Max(1,F.Cvtt(F.Mul(amount,RetailNpcNormalDamageStages.CalculatePveRatio(scope,ratios.Attack,ratios.Defense))));
        effect.AddSuccessEffect(template);if(status.HasValue)effect.SetSpellStatus(status.Value);
        AttackUtil.CalculateEffectResult(effect,target,damage,attack,HitType.PHHIT,false,1,true);
        success=true;return true;
    }
    private static int Chance(RetailNpcNormalStatusMath.Context c,SkyTempleNpcProfile p,Player t,int acc,
        RetailNpcNormalStatusMath.ChanceStage stage,StatEnum stat,int value)=>RetailNpcNormalStatusMath.CalculatePhysicalThreshold(c,stage,
            new(SkyTempleCombatInputs.Moved(t,stat,value),acc,0,(uint)p.StatRatio));
    // Exact file-backed native120-word float32 tables Main14111ca60 and14111cc40.
    private static readonly byte[] A=Convert.FromHexString("00000000465e563dc712d63d3c30203ee2e6543eed83843e7a379e3e037cb73ed53fd03e6171e83e0000003f766d0b3f1479163f1e1b213f2b4c2b3ff704353fc03e3e3f0af3463fbd1b4f3f25b3563fd0b35d3f0919643f16de693f19ff6e3f7978733fed46773fe8677a3f1fd97c3ffe987e3f37a67f3f0000803f37a67f3ffe987e3f1fd97c3fe8677a3fed46773f7978733f19ff6e3f16de693f0919643fd0b35d3f25b3563fbd1b4f3f0af3463fc03e3e3ff704353f2b4c2b3f1e1b213f1479163f766d0b3f0000003f6171e83ed53fd03e037cb73e7a379e3eed83843ee2e6543e3c30203ec712d63d465e563d00000000465e56bdc712d6bd3c3020bee2e654beed8384be7a379ebe037cb7bed53fd0be6171e8be000000bf766d0bbf147916bf1e1b21bf2b4c2bbff70435bfc03e3ebf0af346bfbd1b4fbf25b356bfd0b35dbf091964bf16de69bf19ff6ebf797873bfed4677bfe8677abf1fd97cbffe987ebf37a67fbf000080bf37a67fbffe987ebf1fd97cbfe8677abfed4677bf797873bf19ff6ebf16de69bf091964bfd0b35dbf25b356bfbd1b4fbf0af346bfc03e3ebff70435bf2b4c2bbf1e1b21bf147916bf766d0bbf000000bf6171e8bed53fd0be037cb7be7a379ebeed8384bee2e654be3c3020bec712d6bd465e56bd");
    private static readonly byte[] B=Convert.FromHexString("0000803f37a67f3ffe987e3f1fd97c3fe8677a3fed46773f7978733f19ff6e3f16de693f0919643fd0b35d3f25b3563fbd1b4f3f0af3463fc03e3e3ff704353f2b4c2b3f1e1b213f1479163f766d0b3f0000003f6171e83ed53fd03e037cb73e7a379e3eed83843ee2e6543e3c30203ec712d63d465e563d00000000465e56bdc712d6bd3c3020bee2e654beed8384be7a379ebe037cb7bed53fd0be6171e8be000000bf766d0bbf147916bf1e1b21bf2b4c2bbff70435bfc03e3ebf0af346bfbd1b4fbf25b356bfd0b35dbf091964bf16de69bf19ff6ebf797873bfed4677bfe8677abf1fd97cbffe987ebf37a67fbf000080bf37a67fbffe987ebf1fd97cbfe8677abfed4677bf797873bf19ff6ebf16de69bf091964bfd0b35dbf25b356bfbd1b4fbf0af346bfc03e3ebff70435bf2b4c2bbf1e1b21bf147916bf766d0bbf000000bf6171e8bed53fd0be037cb7be7a379ebeed8384bee2e654be3c3020bec712d6bd465e56bd00000080465e563dc712d63d3c30203ee2e6543eed83843e7a379e3e037cb73ed53fd03e6171e83e0000003f766d0b3f1479163f1e1b213f2b4c2b3ff704353fc03e3e3f0af3463fbd1b4f3f25b3563fd0b35d3f0919643f16de693f19ff6e3f7978733fed46773fe8677a3f1fd97c3ffe987e3f37a67f3f");
    internal static bool Direction(int skill,int heading,float sx,float sy,float tx,float ty)
    {
        if(skill is not (16556 or 16794))throw new ArgumentOutOfRangeException(nameof(skill));
        float dot=DirectionDot(heading,sx,sy,tx,ty);
        return skill==16556?dot>0:dot<=0;
    }
    internal static bool Behind(int heading,float sx,float sy,float tx,float ty)=>DirectionDot(heading,sx,sy,tx,ty)>0;
    private static float DirectionDot(int heading,float sx,float sy,float tx,float ty)
    {
        if(heading is <0 or >=120)throw new ArgumentOutOfRangeException(nameof(heading));
        float a=BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(A.AsSpan(heading*4,4)));
        float b=BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(B.AsSpan(heading*4,4)));
        float dot=F.Add(F.Mul(F.Sub(ty,sy),a),F.Mul(F.Sub(tx,sx),b));
        if(!float.IsFinite(dot))throw new InvalidOperationException("Nonfinite current skill geometry");
        return dot;
    }
}
