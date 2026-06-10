using System.Collections.Generic;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ATTACK (-Nemesiss-, Sweetkr). Physical-attack result: attacker/target ids, animations, hp%, counter/status flag switch, per-hit AttackResult list (damage, status, shield/reflect switch). instanceof Player->is Player+cast; (byte) shieldType cast; AttackResult/AttackHandAnimation/AttackTypeAnimation/Effect red-tolerated.</summary>
public class SM_ATTACK : AionServerPacket
{
    private int attackno;
    private int time;
    private AttackHandAnimation attackHandAnimation;
    private AttackTypeAnimation attackTypeAnimation;
    private List<AttackResult> attackList;
    private Creature attacker;
    private Creature target;
    private Effect criticalEffect;

    public SM_ATTACK(Creature attacker, Creature target, int attackno, int time, AttackTypeAnimation attackTypeAnimation, AttackHandAnimation attackHandAnimation, List<AttackResult> attackList)
        : this(attacker, target, attackno, time, attackTypeAnimation, attackHandAnimation, attackList, null)
    {
    }

    public SM_ATTACK(Creature attacker, Creature target, int attackno, int time, AttackTypeAnimation attackTypeAnimation, AttackHandAnimation attackHandAnimation, List<AttackResult> attackList, Effect criticalEffect)
    {
        this.attacker = attacker;
        this.target = target;
        this.attackno = attackno;// empty
        this.time = time;// empty
        this.attackHandAnimation = attackHandAnimation;
        this.attackTypeAnimation = attackTypeAnimation;
        this.attackList = attackList;
        this.criticalEffect = criticalEffect;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(attacker.GetObjectId());
        WriteC(attackno);
        WriteH(time);
        WriteC(attackTypeAnimation.GetId());
        WriteC(attackHandAnimation.GetId());

        WriteD(target.GetObjectId());

        WriteC(target.GetLifeStats().GetHpPercentage());
        WriteC(attacker.GetLifeStats().GetHpPercentage());

        // TODO refactor attack controller
        switch (attackList[0].GetAttackStatus().GetId()) // Counter skills
        {
            case -60: // case CRITICAL_BLOCK
            case 4: // case BLOCK
                WriteH(32);
                break;
            case -62: // case CRITICAL_PARRY
            case 2: // case PARRY
                WriteH(64);
                break;
            case -64: // case CRITICAL_DODGE
            case 0: // case DODGE
                WriteH(128);
                break;
            case -58: // case PHYSICAL_CRITICAL_RESIST
            case 6: // case RESIST
                WriteH(256); // need more info becuz sometimes 0
                break;
            default:
                if (criticalEffect != null)
                {
                    if (target is Player)
                        WriteH(criticalEffect.GetSkillId() == 8218 ? 1 : 2);
                    else
                        WriteH(criticalEffect.GetSkillId() == 8218 ? 1025 : 1026);
                }
                else
                {
                    WriteH(0);
                }
                break;
        }
        // setting counter skill from packet to have the best synchronization of time with client
        if (target is Player)
        {
            if (attackList[0].GetAttackStatus().IsCounterSkill())
                ((Player)target).SetLastCounterSkill(attackList[0].GetAttackStatus());
        }

        WriteH(0);
        if (criticalEffect != null)
        {
            WriteF(criticalEffect.GetTargetX());
            WriteF(criticalEffect.GetTargetY());
            WriteF(criticalEffect.GetTargetZ());
        }
        // TODO! those 2h (== d) up is some kind of very weird flag...
        // writeD(attackFlag);
        /*
         * if(attackFlag & 0x10A0F != 0) { writeF(0); writeF(0); writeF(0); } if(attackFlag & 0x10010 != 0) { writeC(0); } if(attackFlag & 0x10000 != 0) {
         * writeD(0); writeD(0); }
         */

        WriteC(attackList.Count);
        foreach (AttackResult attack in attackList)
        {
            WriteD(attack.GetDamage());
            WriteC(attack.GetAttackStatus().GetId());

            byte shieldType = (byte)attack.GetShieldType();
            WriteC(shieldType);

            // shield Type: 1: reflector 2: normal shield 8: protect effect (ex. skillId: 417 Bodyguard) TODO find out 4
            switch (shieldType)
            {
                case 0:
                case 2:
                    break;
                case 8:
                case 10:
                    WriteD(attack.GetProtectorId()); // protectorId
                    WriteD(attack.GetProtectedDamage()); // protected damage
                    WriteD(attack.GetProtectedSkillId()); // skillId
                    break;
                case 16:
                    WriteD(0);
                    WriteD(0);
                    WriteD(0);
                    WriteD(0);
                    WriteD(0);
                    WriteD(attack.GetMpAbsorbed());
                    WriteD(attack.GetReflectedSkillId()); // skill id
                    break;
                default:
                    WriteD(attack.GetProtectorId()); // protectorId
                    WriteD(attack.GetProtectedDamage()); // protected damage
                    WriteD(attack.GetProtectedSkillId()); // skillId
                    WriteD(attack.GetReflectedDamage()); // reflect damage
                    WriteD(attack.GetReflectedSkillId()); // skill id
                    WriteD(0);
                    WriteD(0);
                    break;
            }
        }
        WriteC(0);
    }
}
