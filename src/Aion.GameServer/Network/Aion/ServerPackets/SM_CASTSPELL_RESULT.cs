using System.Collections.Generic;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CASTSPELL_RESULT (alexa026, Sweetkr). Cast-spell result incl. hit time, targeting, per-effect results (effect result/hp%, spell status, reserved effects, shield/reflect). getType()->GetType_() collision; instanceof Player->is Player + cast; Skill.SkillMethod/SubEffectType/AttackStatus/EffectResult red-tolerated.</summary>
public class SM_CASTSPELL_RESULT : AionServerPacket
{
    private Creature effector;
    private Creature target;
    private Skill skill;
    private int cooldown;
    private int hitTime;
    private List<Effect> effects;
    private int dashStatus;
    private int targetType;
    private bool chainSuccess;

    public SM_CASTSPELL_RESULT(Skill skill, List<Effect> effects, int hitTime, bool chainSuccess, int dashStatus)
    {
        this.skill = skill;
        this.effector = skill.GetEffector();
        this.target = skill.GetFirstTarget();
        this.effects = effects;
        this.cooldown = skill.GetCooldown();
        this.chainSuccess = chainSuccess;
        this.targetType = 0;
        this.hitTime = hitTime;
        this.dashStatus = dashStatus;
    }

    public SM_CASTSPELL_RESULT(Skill skill, List<Effect> effects, int hitTime, bool chainSuccess, int dashStatus, int targetType)
        : this(skill, effects, hitTime, chainSuccess, dashStatus)
    {
        this.targetType = targetType;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(effector.GetObjectId());
        WriteC(targetType);
        switch (targetType)
        {
            case 0:
            case 3:
            case 4:
                WriteD(target.GetObjectId());
                break;
            case 1:
                WriteF(skill.GetX());
                WriteF(skill.GetY());
                WriteF(skill.GetZ());
                break;
            case 2:
                WriteF(skill.GetX());
                WriteF(skill.GetY());
                WriteF(skill.GetZ());
                WriteF(0);// unk1
                WriteF(0);// unk2
                WriteF(0);// unk3
                WriteF(0);// unk4
                WriteF(0);// unk5
                WriteF(0);// unk6
                WriteF(0);// unk7
                WriteF(0);// unk8
                break;
        }
        WriteH(skill.GetSkillTemplate().GetSkillId());
        WriteC(skill.GetSkillTemplate().GetLvl());
        WriteD(cooldown);
        WriteH(hitTime);
        WriteC(0); // unk

        // 0 : no chain skill 16 : no damage to all target like dodge, resist or effect size is 0 32 : regular
        // Seen: 0xA0 for skill 2723, 0x22 for skill 1169; Skill id doesn't fit to this structure: 2395
        if (effects.Count == 0)
            WriteC(16);
        else if (chainSuccess)
            WriteC(32);
        else
            WriteC(0);

        if (skill.GetItemTemplate() != null && skill.GetItemTemplate().IsCombatActivated())
        {
            WriteC(2);
            WriteD(skill.GetItemObjectId());
            WriteD(skill.GetItemTemplate().GetTemplateId());
            WriteC(0); // unk 0
        }
        else
        {
            if (skill.GetSkillMethod() == Skill.SkillMethod.PENALTY)
                WriteC(4);
            else
                WriteC(0);
            WriteC(this.dashStatus);
            switch (this.dashStatus)
            {
                case 1:
                case 2:
                case 3:
                case 4:
                case 6:
                    WriteC(skill.GetH());
                    WriteF(skill.GetX());
                    WriteF(skill.GetY());
                    WriteF(skill.GetZ());
                    break;
            }
        }

        WriteH(effects.Count);
        foreach (Effect effect in effects)
        {
            Creature effected = effect.GetOriginalEffected();

            if (effected != null)
            {
                WriteD(effected.GetObjectId());
                WriteC(effect.GetEffectResult().GetId());// 0 - NORMAL, 1 - ABSORBED, 2 - CONFLICT, 3 - DODGE, 4 - RESIST
                WriteC(effect.GetEffectedHp() == -1 ? effected.GetLifeStats().GetHpPercentage() : effect.GetEffectedHp()); // target %hp
            }
            else
            { // point point skills
                WriteD(effector.GetObjectId());
                WriteC(0);
                WriteC(100);
            }

            WriteC(effector.GetLifeStats().GetHpPercentage()); // attacker %hp

            // Spell Status 1 : stumble 2 : knockback 4 : open aerial 8 : close aerial 16 : spin 32 : block 64 : parry 128 : dodge 256 : resist
            WriteC(effect.GetSpellStatus().GetId());
            WriteC(effect.GetSuccessfulEffectsAsByte());
            WriteH(0);
            WriteC(effect.GetCarvedSignet()); // current carve signet count
            switch (effect.GetSpellStatus().GetId())
            {
                case 1:
                case 2:
                case 4:
                case 8:
                    WriteF(effect.GetTargetX());
                    WriteF(effect.GetTargetY());
                    WriteF(effect.GetTargetZ());
                    break;
                case 16:
                    WriteC(effect.GetEffector().GetHeading());
                    break;
                default:
                    switch (effect.GetSubEffectType())
                    {
                        case SubEffectType.PULL:
                        case SubEffectType.PULL_NPC:
                        case SubEffectType.SIMPLE_MOVE_BACK:
                            WriteF(effect.GetTargetX());
                            WriteF(effect.GetTargetY());
                            WriteF(effect.GetTargetZ());
                            break;
                    }
                    break;
            }

            ISet<EffectReserved> reservedEffects = effect.GetReservedEffectsToSend();
            WriteC(reservedEffects.Count); // it's success effect count (MP and HP heal, for example, always atleast 1)
            foreach (EffectReserved er in reservedEffects)
            {
                WriteC(er.GetType_().GetValue());// HP - 0 , MP - 1, FP - 2, DP - 3?
                WriteD(er.GetValueToSend());
                WriteC(effect.GetAttackStatus().GetId());
                bool isCounter = effect.GetAttackStatus().IsCounterSkill();
                if (effect.GetEffectResult() == EffectResult.RESIST)
                    isCounter = true;
                // setting counter skill from packet to have the best synchronization of time with client
                if (effect.GetEffected() is Player)
                {
                    if (isCounter)
                        ((Player)effect.GetEffected()).SetLastCounterSkill(effect.GetEffectResult() == EffectResult.RESIST ? AttackStatus.RESIST : effect
                            .GetAttackStatus());
                }

                // shield Type: 1: reflector 2: normal shield 8: protect effect (ex. skillId: 417 Bodyguard) 16: mp shield TODO find out 4
                WriteC(effect.GetShieldDefense());
                switch (effect.GetShieldDefense())
                {
                    case 0:
                    case 2:
                        break;
                    case 8:
                    case 10:
                        WriteD(effect.GetProtectorId()); // protectorId
                        WriteD(effect.GetProtectedDamage()); // protected damage
                        WriteD(effect.GetProtectedSkillId()); // skillId
                        break;
                    default:
                        WriteD(effect.GetProtectorId()); // protectorId
                        WriteD(effect.GetProtectedDamage()); // protected damage
                        WriteD(effect.GetProtectedSkillId()); // skillId
                        WriteD(effect.GetReflectedDamage()); // reflect damage
                        WriteD(effect.GetReflectedSkillId()); // skill id
                        WriteD(effect.GetMpAbsorbed());
                        WriteD(effect.GetMpShieldSkillId()); // skill id
                        break;
                }
            }
        }
    }
}
